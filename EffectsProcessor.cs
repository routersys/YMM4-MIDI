using System;
using System.Buffers;
using System.IO;
using System.Numerics;
using System.Reflection;
using ComputeSharp;
using System.Text;

namespace MIDI
{
    public partial class EffectsProcessor : IDisposable
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;
        private readonly GraphicsDevice? device;
        private float[]? impulseResponse;
        private int impulseResponseLength;
        private bool disposedValue;
        private const int GpuChunkSize = 1 << 18;
        private float lastIn, lastOut;

        private float[]? delayBuffer;
        private int delayPosition = 0;

        private readonly SchroederReverb reverb;
        private float bitCrusherLastSample = 0;
        private int bitCrusherCounter = 0;


        public EffectsProcessor(MidiConfiguration config, int sampleRate, GraphicsDevice? device = null)
        {
            this.config = config;
            this.sampleRate = sampleRate;
            this.device = device;
            LoadImpulseResponse();
            InitializeDelayBuffer();
            this.reverb = new SchroederReverb(sampleRate, config.Effects.AlgorithmicReverb);
        }

        private void InitializeDelayBuffer()
        {
            if (config.Effects.EnablePingPongDelay)
            {
                int delaySamples = (int)(sampleRate * 2);
                delayBuffer = ArrayPool<float>.Shared.Rent(delaySamples);
                Array.Clear(delayBuffer, 0, delaySamples);
            }
        }

        private void LoadImpulseResponse()
        {
            if (!config.Effects.EnableConvolutionReverb) return;

            string irPath = config.Effects.ImpulseResponseFilePath;
            if (!string.IsNullOrEmpty(irPath) && File.Exists(irPath))
            {
                try
                {
                    using (var stream = new FileStream(irPath, FileMode.Open, FileAccess.Read))
                    using (var reader = new BinaryReader(stream))
                    {
                        ReadWavHeader(reader, out int channels, out int bitsPerSample, out int dataLength);

                        var buffer = reader.ReadBytes(dataLength);
                        impulseResponseLength = dataLength / (bitsPerSample / 8);

                        if (impulseResponse != null) ArrayPool<float>.Shared.Return(impulseResponse);
                        impulseResponse = ArrayPool<float>.Shared.Rent(impulseResponseLength);

                        int count = 0;
                        if (bitsPerSample == 16)
                        {
                            for (int i = 0; i < buffer.Length; i += 2)
                            {
                                impulseResponse[count++] = BitConverter.ToInt16(buffer, i) / 32768.0f;
                            }
                        }
                        else if (bitsPerSample == 32)
                        {
                            for (int i = 0; i < buffer.Length; i += 4)
                            {
                                impulseResponse[count++] = BitConverter.ToSingle(buffer, i);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    GenerateDefaultImpulseResponse();
                }
            }
            else
            {
                GenerateDefaultImpulseResponse();
            }
        }

        private void ReadWavHeader(BinaryReader reader, out int channels, out int bitsPerSample, out int dataLength)
        {
            channels = 0;
            bitsPerSample = 0;
            dataLength = 0;

            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "RIFF") throw new InvalidDataException("Not a RIFF file");
            reader.ReadUInt32();
            if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "WAVE") throw new InvalidDataException("Not a WAVE file");

            string chunkId;
            uint chunkSize;

            while (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                chunkId = Encoding.ASCII.GetString(reader.ReadBytes(4));
                chunkSize = reader.ReadUInt32();
                if (chunkId == "fmt ")
                {
                    reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    reader.ReadUInt32();
                    reader.ReadUInt32();
                    reader.ReadUInt16();
                    bitsPerSample = reader.ReadUInt16();
                }
                else if (chunkId == "data")
                {
                    dataLength = (int)chunkSize;
                    return;
                }
                else
                {
                    reader.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                }
            }
            throw new InvalidDataException("WAV data chunk not found");
        }


        private void GenerateDefaultImpulseResponse()
        {
            impulseResponseLength = sampleRate;
            if (impulseResponse != null) ArrayPool<float>.Shared.Return(impulseResponse);
            impulseResponse = ArrayPool<float>.Shared.Rent(impulseResponseLength);
            var random = new Random();
            for (int i = 0; i < impulseResponseLength; i++)
            {
                impulseResponse[i] = (float)((random.NextDouble() * 2 - 1) * Math.Exp(-i / (sampleRate * 0.2)));
            }
        }


        public float ApplyChannelEffects(float input, ChannelState channelState, double time)
        {
            var output = input;

            if (config.Effects.EnableChorus && channelState.Chorus > 0)
            {
                var chorusDelay = config.Effects.ChorusDelay + config.Effects.ChorusDepth * Math.Sin(2 * Math.PI * config.Effects.ChorusRate * time);
                output += input * channelState.Chorus * config.Effects.ChorusStrength * (float)Math.Sin(chorusDelay);
            }

            if (config.Effects.EnablePhaser)
            {
                output = ApplyPhaser(output, time);
            }

            if (config.Effects.EnableFlanger)
            {
                output = ApplyFlanger(output, time);
            }

            return output;
        }

        public bool ApplyAudioEnhancements(Span<float> buffer)
        {
            if (!config.Effects.EnableEffects) return true;

            bool gpuUsed = false;
            bool gpuSucceeded = true;

            if (device == null || (!config.Performance.GPU.EnableGpuEffectsChain && !config.Performance.GPU.EnableGpuEqualizer && !config.Performance.GPU.EnableGpuConvolutionReverb))
            {
                ApplyCpuAudioEnhancements(buffer);
                return true;
            }

            if (config.Performance.GPU.EnableGpuEffectsChain)
            {
                gpuUsed = true;
                if (!ApplyGpuEffectsChain(buffer, device))
                {
                    gpuSucceeded = false;
                    ApplyCpuAudioEnhancements(buffer);
                }
            }
            else
            {
                if (config.Performance.GPU.EnableGpuConvolutionReverb)
                {
                    gpuUsed = true;
                    if (!ApplyConvolutionReverbGpu(buffer, device))
                    {
                        gpuSucceeded = false;
                        if (config.Effects.EnableConvolutionReverb)
                        {
                            ApplyConvolutionReverbCpu(buffer);
                        }
                    }
                }
                else if (config.Effects.EnableConvolutionReverb)
                {
                    ApplyConvolutionReverbCpu(buffer);
                }

                if (config.Effects.EnableDCOffsetRemoval) ApplyDCOffsetRemoval(buffer);
                if (config.Effects.EnableCompression) ApplyCompression(buffer);
                if (config.Effects.AlgorithmicReverb.Enable) ApplyAlgorithmicReverb(buffer);
                if (config.Effects.EnableLimiter) ApplyLimiter(buffer);
                if (config.Effects.EnablePingPongDelay) ApplyPingPongDelay(buffer);
                if (config.Effects.Distortion.Enable) ApplyDistortion(buffer);
                if (config.Effects.BitCrusher.Enable) ApplyBitCrusher(buffer);

            }

            if (config.Performance.GPU.EnableGpuEqualizer)
            {
                gpuUsed = true;
                if (!ApplyEqualizerGpu(buffer, device))
                {
                    gpuSucceeded = false;
                    if (config.Effects.EnableEqualizer) ApplyEqualizer(buffer);
                }
            }
            else if (config.Effects.EnableEqualizer)
            {
                ApplyEqualizer(buffer);
            }

            return !gpuUsed || gpuSucceeded;
        }

        private void ApplyCpuAudioEnhancements(Span<float> buffer)
        {
            if (config.Effects.EnableDCOffsetRemoval) ApplyDCOffsetRemoval(buffer);
            if (config.Effects.EnableCompression) ApplyCompression(buffer);
            if (config.Effects.AlgorithmicReverb.Enable) ApplyAlgorithmicReverb(buffer);
            if (config.Effects.EnableConvolutionReverb) ApplyConvolutionReverbCpu(buffer);
            if (config.Effects.EnableEqualizer) ApplyEqualizer(buffer);
            if (config.Effects.EnableLimiter) ApplyLimiter(buffer);
            if (config.Effects.EnablePingPongDelay) ApplyPingPongDelay(buffer);
            if (config.Effects.Distortion.Enable) ApplyDistortion(buffer);
            if (config.Effects.BitCrusher.Enable) ApplyBitCrusher(buffer);
        }

        public void ApplyConvolutionReverbCpu(Span<float> buffer)
        {
            if (impulseResponse == null || impulseResponseLength == 0) return;

            var outputBuffer = ArrayPool<float>.Shared.Rent(buffer.Length);
            var outputSpan = outputBuffer.AsSpan(0, buffer.Length);
            outputSpan.Clear();

            try
            {
                for (int i = 0; i < buffer.Length; i++)
                {
                    for (int j = 0; j < impulseResponseLength; j++)
                    {
                        if (i - j >= 0)
                        {
                            outputSpan[i] += buffer[i - j] * impulseResponse[j];
                        }
                    }
                }

                for (int i = 0; i < buffer.Length; i++)
                {
                    buffer[i] = Math.Clamp(outputSpan[i], -1.0f, 1.0f);
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(outputBuffer);
            }
        }


        public bool ApplyConvolutionReverbGpu(Span<float> buffer, GraphicsDevice device)
        {
            if (impulseResponse == null || impulseResponseLength == 0) return true;
            try
            {
                using var gpuBuffer = device.AllocateReadWriteBuffer<float>(buffer);
                using var gpuImpulseResponse = device.AllocateReadOnlyBuffer<float>(impulseResponse.AsSpan(0, impulseResponseLength));

                device.For(gpuBuffer.Length, new ConvolutionShader(gpuBuffer, gpuImpulseResponse, gpuImpulseResponse.Length));

                gpuBuffer.CopyTo(buffer);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is NotSupportedException)
            {
                return false;
            }
        }

        public bool ApplyGpuEffectsChain(Span<float> buffer, GraphicsDevice device)
        {
            try
            {
                using var gpuBuffer = device.AllocateReadWriteBuffer<float>(buffer);

                for (int offset = 0; offset < buffer.Length; offset += GpuChunkSize)
                {
                    int count = Math.Min(GpuChunkSize, buffer.Length - offset);
                    var shader = new EffectsChainShader(
                        gpuBuffer,
                        gpuBuffer.Length,
                        sampleRate,
                        offset,
                        config.Effects.AlgorithmicReverb.Enable,
                        config.Effects.AlgorithmicReverb.WetLevel,
                        config.Effects.AlgorithmicReverb.RoomSize,
                        config.Effects.EnableCompression,
                        config.Effects.CompressionThreshold,
                        config.Effects.CompressionRatio,
                        config.Effects.EnableLimiter,
                        config.Effects.LimiterThreshold,
                        config.Effects.EnableDCOffsetRemoval
                    );
                    device.For(count, shader);
                }

                gpuBuffer.CopyTo(buffer);
                return true;
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is NotSupportedException)
            {
                return false;
            }
        }

        public void ApplyDCOffsetRemoval(Span<float> buffer)
        {
            const float alpha = 0.999f;
            for (int i = 0; i < buffer.Length; i++)
            {
                float currentIn = buffer[i];
                float currentOut = currentIn - lastIn + alpha * lastOut;
                buffer[i] = currentOut;
                lastIn = currentIn;
                lastOut = currentOut;
            }
        }

        public void ApplyCompression(Span<float> buffer)
        {
            var threshold = config.Effects.CompressionThreshold;
            var ratio = config.Effects.CompressionRatio;

            int vectorSize = Vector<float>.Count;
            var thresholdVec = new Vector<float>(threshold);
            var ratioVec = new Vector<float>(ratio);

            for (int i = 0; i <= buffer.Length - vectorSize; i += vectorSize)
            {
                var inputVec = new Vector<float>(buffer.Slice(i, vectorSize));
                var absInputVec = Vector.Abs(inputVec);

                var condition = Vector.GreaterThan(absInputVec, thresholdVec);
                if (condition != Vector<int>.Zero)
                {
                    var excessVec = absInputVec - thresholdVec;
                    var compressedExcessVec = excessVec / ratioVec;
                    var gainVec = (thresholdVec + compressedExcessVec) / absInputVec;

                    var resultVec = Vector.ConditionalSelect(condition, inputVec * gainVec, inputVec);
                    resultVec.CopyTo(buffer.Slice(i, vectorSize));
                }
            }
        }

        private float ApplyPhaser(float input, double time)
        {
            float lfo = (float)Math.Sin(2 * Math.PI * config.Effects.PhaserRate * time);
            float processed = input;
            for (int i = 0; i < config.Effects.PhaserStages; i++)
            {
                processed = (processed + lfo) * 0.5f;
            }
            return input + processed * config.Effects.PhaserFeedback;
        }

        private float ApplyFlanger(float input, double time)
        {
            float delay = config.Effects.FlangerDelay * (1 + (float)Math.Sin(2 * Math.PI * config.Effects.FlangerRate * time));
            return input + input * delay * config.Effects.FlangerDepth;
        }

        public void ApplyLimiter(Span<float> buffer)
        {
            float threshold = config.Effects.LimiterThreshold;
            int vectorSize = Vector<float>.Count;
            var minVec = new Vector<float>(-threshold);
            var maxVec = new Vector<float>(threshold);

            for (int i = 0; i <= buffer.Length - vectorSize; i += vectorSize)
            {
                var vec = new Vector<float>(buffer.Slice(i, vectorSize));
                vec = Vector.Max(minVec, Vector.Min(maxVec, vec));
                vec.CopyTo(buffer.Slice(i, vectorSize));
            }
            for (int i = buffer.Length - (buffer.Length % vectorSize); i < buffer.Length; i++)
            {
                buffer[i] = Math.Max(-threshold, Math.Min(threshold, buffer[i]));
            }
        }

        public void ApplyAlgorithmicReverb(Span<float> buffer)
        {
            for (int i = 0; i < buffer.Length; i += 2)
            {
                reverb.Process(buffer[i], buffer[i + 1], out float outL, out float outR);
                buffer[i] = outL;
                buffer[i + 1] = outR;
            }
        }

        public void ApplyPingPongDelay(Span<float> buffer)
        {
            if (delayBuffer == null) InitializeDelayBuffer();
            if (delayBuffer == null) return;

            int delaySamples = (int)(sampleRate * config.Effects.DelayTime);
            float feedback = config.Effects.Feedback;
            float wet = config.Effects.WetDryMix;
            float dry = 1.0f - wet;

            for (int i = 0; i < buffer.Length; i += 2)
            {
                float leftSample = buffer[i];
                float rightSample = buffer[i + 1];

                int readPosLeft = (delayPosition - delaySamples + delayBuffer.Length) % delayBuffer.Length;
                if (readPosLeft % 2 != 0) readPosLeft = (readPosLeft + 1) % delayBuffer.Length;

                int readPosRight = (delayPosition - delaySamples / 2 + delayBuffer.Length) % delayBuffer.Length;
                if (readPosRight % 2 == 0) readPosRight = (readPosRight + 1) % delayBuffer.Length;

                float delayedLeft = delayBuffer[readPosRight];
                float delayedRight = delayBuffer[readPosLeft];

                buffer[i] = leftSample * dry + delayedLeft * wet;
                buffer[i + 1] = rightSample * dry + delayedRight * wet;

                delayBuffer[delayPosition] = leftSample + delayedRight * feedback;
                delayBuffer[delayPosition + 1] = rightSample + delayedLeft * feedback;

                delayPosition = (delayPosition + 2) % delayBuffer.Length;
            }
        }

        public void ApplyEqualizer(Span<float> buffer)
        {
            var bassGain = config.Effects.EQ.BassGain;
            var midGain = config.Effects.EQ.MidGain;
            var trebleGain = config.Effects.EQ.TrebleGain;

            int vectorSize = Vector<float>.Count;
            var bassGainVec = new Vector<float>(bassGain);

            for (int i = 0; i <= buffer.Length - vectorSize; i += vectorSize)
            {
                if (i % 2 == 0)
                {
                    var sampleVec = new Vector<float>(buffer.Slice(i, vectorSize));
                    (sampleVec * bassGainVec).CopyTo(buffer.Slice(i, vectorSize));
                }
            }
        }

        public void ApplyDistortion(Span<float> buffer)
        {
            float drive = 1.0f + config.Effects.Distortion.Drive * 10.0f;
            float mix = config.Effects.Distortion.Mix;

            for (int i = 0; i < buffer.Length; i++)
            {
                float cleanSample = buffer[i];
                float distortedSample = cleanSample * drive;

                switch (config.Effects.Distortion.Type)
                {
                    case DistortionType.HardClip:
                        distortedSample = Math.Max(-1.0f, Math.Min(1.0f, distortedSample));
                        break;
                    case DistortionType.SoftClip:
                        distortedSample = (float)Math.Tanh(distortedSample);
                        break;
                    case DistortionType.Saturation:
                        distortedSample = distortedSample - (distortedSample * distortedSample * distortedSample / 3.0f);
                        if (distortedSample > 1.0f) distortedSample = 1.0f;
                        if (distortedSample < -1.0f) distortedSample = -1.0f;
                        break;
                }

                buffer[i] = cleanSample * (1.0f - mix) + distortedSample * mix;
            }
        }

        public void ApplyBitCrusher(Span<float> buffer)
        {
            float rateReduction = config.Effects.BitCrusher.RateReduction;
            int bitDepth = config.Effects.BitCrusher.BitDepth;
            double step = Math.Pow(0.5, bitDepth);

            for (int i = 0; i < buffer.Length; i++)
            {
                bitCrusherCounter++;
                if (bitCrusherCounter >= rateReduction)
                {
                    bitCrusherCounter = 0;
                    bitCrusherLastSample = (float)(step * Math.Floor(buffer[i] / step));
                }
                buffer[i] = bitCrusherLastSample;
            }
        }

        private bool ApplyEqualizerGpu(Span<float> buffer, GraphicsDevice device)
        {
            float[]? paddedBuffer = null;
            try
            {
                int N = 1;
                while (N < buffer.Length) N <<= 1;

                paddedBuffer = ArrayPool<float>.Shared.Rent(N);
                buffer.CopyTo(paddedBuffer);
                if (buffer.Length < N)
                {
                    Array.Clear(paddedBuffer, buffer.Length, N - buffer.Length);
                }

                using var complexBuffer = device.AllocateReadWriteBuffer<Float2>(N);
                using var sourceBuffer = device.AllocateReadOnlyBuffer(paddedBuffer);

                device.For(N, new ToComplexShader(sourceBuffer, complexBuffer));

                ExecuteFft(device, complexBuffer, N, false);

                device.For(N, new ApplyEqShader(complexBuffer, sampleRate, config.Effects.EQ.BassGain, config.Effects.EQ.MidGain, config.Effects.EQ.TrebleGain));

                ExecuteFft(device, complexBuffer, N, true);

                using var realBuffer = device.AllocateReadWriteBuffer<float>(N);
                device.For(N, new ToRealShader(complexBuffer, realBuffer, N));

                realBuffer.CopyTo(paddedBuffer, 0, 0, buffer.Length);

                paddedBuffer.AsSpan(0, buffer.Length).CopyTo(buffer);

                return true;
            }
            catch (Exception ex) when (ex is ArgumentOutOfRangeException || ex is NotSupportedException || ex is OutOfMemoryException)
            {
                return false;
            }
            finally
            {
                if (paddedBuffer != null)
                {
                    ArrayPool<float>.Shared.Return(paddedBuffer);
                }
            }
        }

        private static void ExecuteFft(GraphicsDevice device, ReadWriteBuffer<Float2> buffer, int n, bool inverse)
        {
            device.For(n, new BitReverseShader(buffer, n));

            for (int size = 2; size <= n; size <<= 1)
            {
                device.For(n, new FftShader(buffer, n, size, inverse));
            }
        }

        public void NormalizeAudio(Span<float> buffer)
        {
            float maxAbs = 0;
            foreach (float sample in buffer)
            {
                maxAbs = Math.Max(maxAbs, Math.Abs(sample));
            }

            if (maxAbs > config.Audio.NormalizationThreshold)
            {
                var scale = config.Audio.NormalizationLevel / maxAbs;
                int vectorSize = Vector<float>.Count;
                var scaleVec = new Vector<float>(scale);
                for (int i = 0; i <= buffer.Length - vectorSize; i += vectorSize)
                {
                    var vec = new Vector<float>(buffer.Slice(i, vectorSize));
                    (vec * scaleVec).CopyTo(buffer.Slice(i, vectorSize));
                }
                for (int i = buffer.Length - (buffer.Length % vectorSize); i < buffer.Length; i++)
                {
                    buffer[i] *= scale;
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (delayBuffer != null)
                    {
                        ArrayPool<float>.Shared.Return(delayBuffer);
                        delayBuffer = null;
                    }
                }

                if (impulseResponse != null)
                {
                    ArrayPool<float>.Shared.Return(impulseResponse);
                    impulseResponse = null;
                }
                disposedValue = true;
            }
        }

        ~EffectsProcessor()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        private class SchroederReverb
        {
            private readonly int sampleRate;
            private readonly AlgorithmicReverbSettings settings;
            private readonly CombFilter[] combFilters;
            private readonly AllPassFilter[] allPassFilters;

            private readonly int[] combFilterTunings = { 1116, 1188, 1277, 1356, 1422, 1491, 1557, 1617 };
            private readonly int[] allPassFilterTunings = { 225, 556, 441, 341 };

            public SchroederReverb(int sr, AlgorithmicReverbSettings settings)
            {
                this.sampleRate = sr;
                this.settings = settings;

                combFilters = new CombFilter[combFilterTunings.Length];
                for (int i = 0; i < combFilters.Length; i++)
                {
                    combFilters[i] = new CombFilter(sampleRate, combFilterTunings[i], settings);
                }
                allPassFilters = new AllPassFilter[allPassFilterTunings.Length];
                for (int i = 0; i < allPassFilters.Length; i++)
                {
                    allPassFilters[i] = new AllPassFilter(sampleRate, allPassFilterTunings[i], settings);
                }
            }

            public void Process(float inL, float inR, out float outL, out float outR)
            {
                float monoIn = (inL + inR) * 0.5f;
                float reverbOut = 0;

                for (int i = 0; i < combFilters.Length; i++)
                {
                    reverbOut += combFilters[i].Process(monoIn);
                }
                reverbOut /= combFilters.Length;

                for (int i = 0; i < allPassFilters.Length; i++)
                {
                    reverbOut = allPassFilters[i].Process(reverbOut);
                }

                outL = inL * settings.DryLevel + reverbOut * settings.WetLevel * (1.0f - settings.Width);
                outR = inR * settings.DryLevel + reverbOut * settings.WetLevel * (1.0f + settings.Width);
            }

            private abstract class ReverbFilter
            {
                protected float[] buffer;
                protected int bufferPos;
                protected readonly AlgorithmicReverbSettings settings;
                public ReverbFilter(int sampleRate, int delayMs, AlgorithmicReverbSettings settings)
                {
                    this.settings = settings;
                    buffer = new float[(int)(sampleRate * delayMs / 1000.0f)];
                }
                public abstract float Process(float input);
            }

            private class CombFilter : ReverbFilter
            {
                private float feedback;
                public CombFilter(int sr, int delay, AlgorithmicReverbSettings s) : base(sr, delay, s) { Update(); }
                public void Update() => feedback = (float)Math.Pow(10.0, -3.0 * settings.RoomSize);
                public override float Process(float input)
                {
                    float output = buffer[bufferPos];
                    float delayed = output + input * feedback;
                    buffer[bufferPos] = delayed - (delayed * settings.Damping);
                    if (++bufferPos >= buffer.Length) bufferPos = 0;
                    return output;
                }
            }

            private class AllPassFilter : ReverbFilter
            {
                public AllPassFilter(int sr, int delay, AlgorithmicReverbSettings s) : base(sr, delay, s) { }
                public override float Process(float input)
                {
                    float delayed = buffer[bufferPos];
                    buffer[bufferPos] = input + delayed * 0.5f;
                    if (++bufferPos >= buffer.Length) bufferPos = 0;
                    return delayed - input;
                }
            }
        }
    }
}
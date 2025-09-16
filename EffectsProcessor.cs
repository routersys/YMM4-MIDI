using System;
using System.Buffers;
using System.Numerics;
using ComputeSharp;

namespace MIDI
{
    public partial class EffectsProcessor : IDisposable
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;
        private float[]? impulseResponse;
        private bool disposedValue;
        private const int GpuChunkSize = 1 << 18;
        private const float DcOffsetAlpha = 0.995f;

        public EffectsProcessor(MidiConfiguration config, int sampleRate)
        {
            this.config = config;
            this.sampleRate = sampleRate;
            LoadImpulseResponse();
        }

        private void LoadImpulseResponse()
        {
            if (!config.Effects.EnableConvolutionReverb) return;
            impulseResponse = ArrayPool<float>.Shared.Rent(sampleRate);
            var random = new Random();
            for (int i = 0; i < sampleRate; i++)
            {
                impulseResponse[i] = (float)((random.NextDouble() * 2 - 1) * Math.Exp(-i / (sampleRate * 0.2)));
            }
        }

        public float ApplyChannelEffects(float input, ChannelState channelState, double time)
        {
            var output = input;

            if (config.Effects.EnableReverb && channelState.Reverb > 0)
            {
                output += input * channelState.Reverb * config.Effects.ReverbStrength;
            }

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

            using var device = GraphicsDevice.GetDefault();
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
                    ApplyDCOffsetRemoval(buffer);
                    ApplyCompression(buffer);
                    ApplyGlobalReverb(buffer);
                    ApplyLimiter(buffer);
                }
            }
            else
            {
                if (config.Performance.GPU.EnableGpuConvolutionReverb)
                {
                    gpuUsed = true;
                    if (!ApplyConvolutionReverbGpu(buffer, device)) gpuSucceeded = false;
                }
                if (config.Effects.EnableDCOffsetRemoval) ApplyDCOffsetRemoval(buffer);
                if (config.Effects.EnableCompression) ApplyCompression(buffer);
                if (config.Effects.EnableReverb) ApplyGlobalReverb(buffer);
                if (config.Effects.EnableLimiter) ApplyLimiter(buffer);
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
            if (config.Effects.EnableReverb) ApplyGlobalReverb(buffer);
            if (config.Effects.EnableEqualizer) ApplyEqualizer(buffer);
            if (config.Effects.EnableLimiter) ApplyLimiter(buffer);
        }

        public bool ApplyConvolutionReverbGpu(Span<float> buffer, GraphicsDevice device)
        {
            if (impulseResponse == null) return true;
            try
            {
                using var gpuBuffer = device.AllocateReadWriteBuffer<float>(buffer);
                using var gpuImpulseResponse = device.AllocateReadOnlyBuffer<float>(impulseResponse.AsSpan(0, sampleRate));

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
                        config.Effects.EnableReverb,
                        config.Effects.ReverbDelay,
                        config.Effects.ReverbDecay,
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
            float lastIn = 0;
            float lastOut = 0;

            for (int i = 0; i < buffer.Length; i++)
            {
                float currentIn = buffer[i];
                float currentOut = currentIn - lastIn + DcOffsetAlpha * lastOut;
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

        public void ApplyGlobalReverb(Span<float> buffer)
        {
            var delayMs = config.Effects.ReverbDelay;
            var delaySamples = (int)(delayMs * sampleRate / 1000);
            var decay = config.Effects.ReverbDecay;

            if (delaySamples > 0 && delaySamples < buffer.Length)
            {
                int vectorSize = Vector<float>.Count;
                var decayVec = new Vector<float>(decay);

                for (int i = delaySamples; i <= buffer.Length - vectorSize; i += vectorSize)
                {
                    var currentVec = new Vector<float>(buffer.Slice(i, vectorSize));
                    var delayedVec = new Vector<float>(buffer.Slice(i - delaySamples, vectorSize));
                    (currentVec + delayedVec * decayVec).CopyTo(buffer.Slice(i, vectorSize));
                }
                for (int i = buffer.Length - (buffer.Length % vectorSize); i < buffer.Length; i++)
                {
                    buffer[i] += buffer[i - delaySamples] * decay;
                }
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
    }
}
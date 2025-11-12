using System;
using System.Buffers;
using System.IO;
using System.Numerics;
using System.Reflection;
using ComputeSharp;
using System.Text;
using MIDI.Configuration.Models;
using MIDI.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;

namespace MIDI
{
    public partial class EffectsProcessor : IEffectsProcessor
    {
        private class EffectState
        {
            public float lastInL, lastInR, lastOutL, lastOutR;
            public float compEnvelope;
            public float bitCrusherLastSample;
            public int bitCrusherCounter;
            public float[]? delayBuffer;
            public int delayPosition;
            public float[]? phaserBuffer;
            public float[]? flangerBuffer;

            public EffectState(int sampleRate, EffectsSettings config)
            {
                if (config.EnablePingPongDelay)
                {
                    int delaySamples = (int)(sampleRate * 2);
                    delayBuffer = ArrayPool<float>.Shared.Rent(delaySamples);
                    Array.Clear(delayBuffer, 0, delaySamples);
                }
                if (config.EnablePhaser)
                {
                    phaserBuffer = ArrayPool<float>.Shared.Rent(config.PhaserStages);
                    Array.Clear(phaserBuffer, 0, config.PhaserStages);
                }
                if (config.EnableFlanger)
                {
                    int flangerDelaySamples = (int)(sampleRate * 0.02);
                    flangerBuffer = ArrayPool<float>.Shared.Rent(flangerDelaySamples);
                    Array.Clear(flangerBuffer, 0, flangerDelaySamples);
                }
            }

            public void ReleaseBuffers()
            {
                if (delayBuffer != null) ArrayPool<float>.Shared.Return(delayBuffer);
                if (phaserBuffer != null) ArrayPool<float>.Shared.Return(phaserBuffer);
                if (flangerBuffer != null) ArrayPool<float>.Shared.Return(flangerBuffer);
            }
        }

        private readonly MidiConfiguration config;
        private readonly int sampleRate;
        private readonly GraphicsDevice? device;
        private float[]? impulseResponse;
        private int impulseResponseLength;
        private bool disposedValue;
        private const int GpuChunkSize = 1 << 18;
        private const int GpuConvolutionMaxLength = 8192;
        private readonly SchroederReverb reverb;
        private readonly BiquadFilter eqBass, eqMid, eqTreble;
        private readonly EffectState effectState;

        public EffectsProcessor(MidiConfiguration config, int sampleRate, GraphicsDevice? device = null)
        {
            this.config = config;
            this.sampleRate = sampleRate;
            this.device = device;
            this.effectState = new EffectState(sampleRate, config.Effects);

            LoadImpulseResponse();
            this.reverb = new SchroederReverb(sampleRate, config.Effects.AlgorithmicReverb);

            eqBass = new BiquadFilter(sampleRate);
            eqMid = new BiquadFilter(sampleRate);
            eqTreble = new BiquadFilter(sampleRate);
            UpdateEqCoefficients();
        }

        public void Reset()
        {
            effectState.lastInL = 0;
            effectState.lastInR = 0;
            effectState.lastOutL = 0;
            effectState.lastOutR = 0;
            effectState.compEnvelope = 0;
            effectState.bitCrusherLastSample = 0;
            effectState.bitCrusherCounter = 0;
            effectState.delayPosition = 0;
            if (effectState.delayBuffer != null) Array.Clear(effectState.delayBuffer, 0, effectState.delayBuffer.Length);
            if (effectState.phaserBuffer != null) Array.Clear(effectState.phaserBuffer, 0, effectState.phaserBuffer.Length);
            if (effectState.flangerBuffer != null) Array.Clear(effectState.flangerBuffer, 0, effectState.flangerBuffer.Length);

            reverb.Reset();
            eqBass.Reset();
            eqMid.Reset();
            eqTreble.Reset();
        }

        private void UpdateEqCoefficients()
        {
            eqBass.SetPeakingEq(150.0f, 1.0f, config.Effects.EQ.BassGain);
            eqMid.SetPeakingEq(1000.0f, 1.0f, config.Effects.EQ.MidGain);
            eqTreble.SetPeakingEq(6000.0f, 1.0f, config.Effects.EQ.TrebleGain);
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
                        ReadWavHeader(reader, out int channels, out int bitsPerSample, out int dataLength, out int irSampleRate);

                        var maxSamples = (int)(config.Effects.MaxImpulseResponseDurationSeconds * irSampleRate);
                        var samplesToRead = Math.Min(maxSamples, dataLength / (bitsPerSample / 8) / channels);

                        if (samplesToRead < dataLength / (bitsPerSample / 8) / channels)
                        {
                            Logger.Warn(LogMessages.IrFileTruncated, config.Effects.MaxImpulseResponseDurationSeconds);
                        }

                        var buffer = reader.ReadBytes(samplesToRead * channels * (bitsPerSample / 8));
                        var irList = new List<float>();

                        if (bitsPerSample == 16)
                        {
                            for (int i = 0; i < buffer.Length; i += 2 * channels)
                            {
                                irList.Add(BitConverter.ToInt16(buffer, i) / 32768.0f);
                            }
                        }
                        else if (bitsPerSample == 32)
                        {
                            for (int i = 0; i < buffer.Length; i += 4 * channels)
                            {
                                irList.Add(BitConverter.ToSingle(buffer, i));
                            }
                        }

                        if (impulseResponse != null) ArrayPool<float>.Shared.Return(impulseResponse);
                        impulseResponse = irList.ToArray();
                        impulseResponseLength = impulseResponse.Length;

                        if (irSampleRate != this.sampleRate)
                        {
                            Logger.Warn(LogMessages.IrResampling, irSampleRate, this.sampleRate);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.IrFileLoadFailed, ex, irPath);
                    GenerateDefaultImpulseResponse();
                }
            }
            else
            {
                GenerateDefaultImpulseResponse();
            }
        }

        private void ReadWavHeader(BinaryReader reader, out int channels, out int bitsPerSample, out int dataLength, out int sampleRate)
        {
            try
            {
                reader.BaseStream.Seek(22, SeekOrigin.Begin);
                channels = reader.ReadUInt16();
                sampleRate = (int)reader.ReadUInt32();
                reader.BaseStream.Seek(34, SeekOrigin.Begin);
                bitsPerSample = reader.ReadUInt16();
                reader.BaseStream.Seek(40, SeekOrigin.Begin);
                dataLength = (int)reader.ReadUInt32();
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.WavHeaderReadError, ex, reader.BaseStream is FileStream fs ? fs.Name : "N/A");
                channels = 0;
                bitsPerSample = 0;
                dataLength = 0;
                sampleRate = 0;
            }
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
            Logger.Info(LogMessages.DefaultIrGenerated);
        }

        public float ApplyChannelEffects(float input, ChannelState channelState, double time)
        {
            var output = input;
            if (config.Effects.EnableChorus && channelState.Chorus > 0)
            {
                output = ApplyChorus(output, time, channelState.Chorus);
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

            bool canUseGpu = device != null;
            bool gpuSucceeded = true;

            if (config.Effects.EnableDCOffsetRemoval) ApplyDCOffsetRemoval(buffer);

            if (config.Effects.EnableCompression) ApplyCompression(buffer);
            if (config.Effects.AlgorithmicReverb.Enable) ApplyAlgorithmicReverb(buffer);
            if (config.Effects.EnableLimiter) ApplyLimiter(buffer);
            if (config.Effects.EnablePingPongDelay) ApplyPingPongDelay(buffer);
            if (config.Effects.Distortion.Enable) ApplyDistortion(buffer);
            if (config.Effects.BitCrusher.Enable) ApplyBitCrusher(buffer);

            if (config.Effects.EnableConvolutionReverb)
            {
                if (canUseGpu && config.Performance.GPU.EnableGpuConvolutionReverb && impulseResponseLength <= GpuConvolutionMaxLength)
                {
                    if (!ApplyConvolutionReverbGpu(buffer, device!))
                    {
                        gpuSucceeded = false;
                        Logger.Warn(LogMessages.GpuIrFailed);
                        ApplyConvolutionReverbCpu(buffer);
                    }
                }
                else
                {
                    if (canUseGpu && config.Performance.GPU.EnableGpuConvolutionReverb && impulseResponseLength > GpuConvolutionMaxLength)
                    {
                        Logger.Warn(LogMessages.GpuIrTooLong, impulseResponseLength);
                    }
                    ApplyConvolutionReverbCpu(buffer);
                }
            }

            if (config.Effects.EnableEqualizer)
            {
                if (canUseGpu && config.Performance.GPU.EnableGpuEqualizer)
                {
                    if (device != null && !ApplyEqualizerGpu(buffer, device))
                    {
                        gpuSucceeded = false;
                        ApplyEqualizer(buffer);
                    }
                }
                else
                {
                    ApplyEqualizer(buffer);
                }
            }

            return gpuSucceeded;
        }

        public void ApplyConvolutionReverbCpu(Span<float> buffer)
        {
            if (impulseResponse == null || impulseResponseLength == 0) return;

            int fftSize;
            try
            {
                int segmentSize = 1024;
                fftSize = 1;
                while (fftSize < segmentSize + impulseResponseLength - 1)
                {
                    fftSize <<= 1;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.FftSizeCalculationError, ex);
                return;
            }

            var irPadded = ArrayPool<float>.Shared.Rent(fftSize);
            var segment = ArrayPool<float>.Shared.Rent(fftSize);
            var monoBuffer = ArrayPool<float>.Shared.Rent(buffer.Length / 2);
            var outputBuffer = ArrayPool<float>.Shared.Rent(buffer.Length);
            var overlap = ArrayPool<float>.Shared.Rent(fftSize);
            var irFft = ArrayPool<Complex>.Shared.Rent(fftSize);
            var segmentComplex = ArrayPool<Complex>.Shared.Rent(fftSize);
            var convolutionResult = ArrayPool<Complex>.Shared.Rent(fftSize);

            try
            {
                for (int i = 0; i < buffer.Length / 2; i++)
                {
                    monoBuffer[i] = (buffer[i * 2] + buffer[i * 2 + 1]) * 0.5f;
                }

                Array.Clear(irPadded, 0, fftSize);
                Array.Copy(impulseResponse, irPadded, impulseResponseLength);

                for (int i = 0; i < fftSize; i++)
                {
                    segmentComplex[i] = new Complex(irPadded[i], 0);
                }

                Fft(segmentComplex, false);
                Array.Copy(segmentComplex, irFft, fftSize);

                Array.Clear(outputBuffer, 0, buffer.Length);
                Array.Clear(overlap, 0, fftSize);

                for (int i = 0; i < monoBuffer.Length; i += 1024)
                {
                    int currentSegmentSize = Math.Min(1024, monoBuffer.Length - i);
                    Array.Clear(segment, 0, fftSize);
                    Array.Copy(monoBuffer, i, segment, 0, currentSegmentSize);

                    for (int j = 0; j < fftSize; j++)
                    {
                        segmentComplex[j] = new Complex(segment[j], 0);
                    }

                    Fft(segmentComplex, false);

                    for (int j = 0; j < fftSize; j++)
                    {
                        convolutionResult[j] = segmentComplex[j] * irFft[j];
                    }

                    Fft(convolutionResult, true);

                    for (int j = 0; j < fftSize; j++)
                    {
                        var sample = (float)convolutionResult[j].Real + overlap[j];
                        if (j < currentSegmentSize)
                        {
                            var outIndex = (i + j) * 2;
                            if (outIndex + 1 < outputBuffer.Length)
                            {
                                outputBuffer[outIndex] = sample;
                                outputBuffer[outIndex + 1] = sample;
                            }
                        }
                        if (j + currentSegmentSize < fftSize)
                        {
                            overlap[j] = (float)convolutionResult[j + currentSegmentSize].Real;
                        }
                        else
                        {
                            overlap[j] = 0;
                        }
                    }
                }

                outputBuffer.AsSpan().CopyTo(buffer);
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.ConvolutionCpuError, ex);
            }
            finally
            {
                ArrayPool<float>.Shared.Return(irPadded);
                ArrayPool<float>.Shared.Return(segment);
                ArrayPool<float>.Shared.Return(monoBuffer);
                ArrayPool<float>.Shared.Return(outputBuffer);
                ArrayPool<float>.Shared.Return(overlap);
                ArrayPool<Complex>.Shared.Return(irFft);
                ArrayPool<Complex>.Shared.Return(segmentComplex);
                ArrayPool<Complex>.Shared.Return(convolutionResult);
            }
        }

        private void Fft(Complex[] data, bool inverse)
        {
            int n = data.Length;
            if ((n & (n - 1)) != 0) return;

            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; (j & bit) != 0; bit >>= 1)
                {
                    j ^= bit;
                }
                j ^= bit;
                if (i < j)
                {
                    (data[i], data[j]) = (data[j], data[i]);
                }
            }

            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = 2 * Math.PI / len * (inverse ? -1 : 1);
                var wlen = new Complex(Math.Cos(angle), Math.Sin(angle));
                for (int i = 0; i < n; i += len)
                {
                    var w = new Complex(1, 0);
                    for (int j = 0; j < len / 2; j++)
                    {
                        var u = data[i + j];
                        var v = data[i + j + len / 2] * w;
                        data[i + j] = u + v;
                        data[i + j + len / 2] = u - v;
                        w *= wlen;
                    }
                }
            }
            if (inverse)
            {
                for (int i = 0; i < n; i++)
                {
                    data[i] /= n;
                }
            }
        }

        public bool ApplyConvolutionReverbGpu(Span<float> buffer, GraphicsDevice device)
        {
            if (impulseResponse == null || impulseResponseLength == 0) return true;
            if (device == null) return false;

            try
            {
                using var gpuBuffer = device.AllocateReadWriteBuffer<float>(buffer);
                using var gpuImpulseResponse = device.AllocateReadOnlyBuffer<float>(impulseResponse.AsSpan(0, impulseResponseLength));
                device.For(gpuBuffer.Length, new ConvolutionShader(gpuBuffer, gpuImpulseResponse, gpuImpulseResponse.Length));
                gpuBuffer.CopyTo(buffer);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.GpuIrGenericError, ex);
                return false;
            }
        }

        public bool ApplyGpuEffectsChain(Span<float> buffer, GraphicsDevice device)
        {
            if (device == null) return false;
            try
            {
                using var gpuBuffer = device.AllocateReadWriteBuffer<float>(buffer);
                for (int offset = 0; offset < buffer.Length; offset += GpuChunkSize)
                {
                    int count = Math.Min(GpuChunkSize, buffer.Length - offset);
                    device.For(count, new EffectsChainShader(gpuBuffer, gpuBuffer.Length, sampleRate, offset,
                                  config.Effects.AlgorithmicReverb.Enable, config.Effects.AlgorithmicReverb.WetLevel, config.Effects.AlgorithmicReverb.RoomSize,
                                  config.Effects.EnableCompression, config.Effects.CompressionThreshold, config.Effects.CompressionRatio,
                                  config.Effects.EnableLimiter, config.Effects.LimiterThreshold, config.Effects.EnableDCOffsetRemoval));
                }
                gpuBuffer.CopyTo(buffer);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.GpuEffectsChainFailed, ex);
                return false;
            }
        }

        public void ApplyDCOffsetRemoval(Span<float> buffer)
        {
            const float alpha = 0.999f;
            for (int i = 0; i < buffer.Length; i += 2)
            {
                float currentInL = buffer[i];
                float currentOutL = currentInL - effectState.lastInL + alpha * effectState.lastOutL;
                buffer[i] = currentOutL;
                effectState.lastInL = currentInL;
                effectState.lastOutL = currentOutL;

                float currentInR = buffer[i + 1];
                float currentOutR = currentInR - effectState.lastInR + alpha * effectState.lastOutR;
                buffer[i + 1] = currentOutR;
                effectState.lastInR = currentInR;
                effectState.lastOutR = currentOutR;
            }
        }

        public void ApplyCompression(Span<float> buffer)
        {
            float attack = 1.0f - (float)Math.Exp(-1.0 / (sampleRate * config.Effects.CompressionAttack));
            float release = 1.0f - (float)Math.Exp(-1.0 / (sampleRate * config.Effects.CompressionRelease));
            float threshold = config.Effects.CompressionThreshold;
            float ratio = config.Effects.CompressionRatio;

            for (int i = 0; i < buffer.Length; i++)
            {
                float inputAbs = Math.Abs(buffer[i]);
                float envelope;
                if (inputAbs > effectState.compEnvelope)
                {
                    envelope = attack * inputAbs + (1.0f - attack) * effectState.compEnvelope;
                }
                else
                {
                    envelope = release * inputAbs + (1.0f - release) * effectState.compEnvelope;
                }
                effectState.compEnvelope = envelope;

                if (envelope > threshold)
                {
                    float gain = threshold + (envelope - threshold) / ratio;
                    buffer[i] *= gain / envelope;
                }
            }
        }

        private float ApplyChorus(float input, double time, float strength)
        {
            var lfo = (float)Math.Sin(2 * Math.PI * config.Effects.ChorusRate * time);
            var delay = config.Effects.ChorusDelay + config.Effects.ChorusDepth * lfo;
            return input * (1 - strength) + input * delay * strength;
        }

        private float ApplyPhaser(float input, double time)
        {
            if (effectState.phaserBuffer == null) return input;
            var lfo = (float)Math.Abs(Math.Sin(2 * Math.PI * config.Effects.PhaserRate * time));
            var feedback = config.Effects.PhaserFeedback;
            for (int i = 0; i < config.Effects.PhaserStages; i++)
            {
                var filtered = effectState.phaserBuffer[i] + (input - effectState.phaserBuffer[i]) * lfo;
                effectState.phaserBuffer[i] = filtered;
                input = filtered;
            }
            return input * feedback;
        }

        private float ApplyFlanger(float input, double time)
        {
            if (effectState.flangerBuffer == null) return input;
            var lfo = (float)Math.Sin(2 * Math.PI * config.Effects.FlangerRate * time);
            var delaySamples = (int)((config.Effects.FlangerDelay + config.Effects.FlangerDepth * lfo) * sampleRate);
            var index = (effectState.delayPosition - delaySamples + effectState.flangerBuffer.Length) % effectState.flangerBuffer.Length;
            var delayedSample = effectState.flangerBuffer[index];
            effectState.flangerBuffer[effectState.delayPosition] = input;
            effectState.delayPosition = (effectState.delayPosition + 1) % effectState.flangerBuffer.Length;
            return (input + delayedSample) * 0.5f;
        }

        public void ApplyLimiter(Span<float> buffer)
        {
            float threshold = config.Effects.LimiterThreshold;
            for (int i = 0; i < buffer.Length; i++)
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
            if (effectState.delayBuffer == null) return;

            int delaySamples = (int)(sampleRate * config.Effects.DelayTime);
            float feedback = config.Effects.Feedback;
            float wet = config.Effects.WetDryMix;
            float dry = 1.0f - wet;

            for (int i = 0; i < buffer.Length; i += 2)
            {
                int readPos = (effectState.delayPosition - delaySamples * 2 + effectState.delayBuffer.Length) % effectState.delayBuffer.Length;

                float delayedLeft = effectState.delayBuffer[readPos + 1];
                float delayedRight = effectState.delayBuffer[readPos];

                float currentLeft = buffer[i];
                float currentRight = buffer[i + 1];

                buffer[i] = currentLeft * dry + delayedLeft * wet;
                buffer[i + 1] = currentRight * dry + delayedRight * wet;

                effectState.delayBuffer[effectState.delayPosition] = currentLeft + delayedRight * feedback;
                effectState.delayBuffer[effectState.delayPosition + 1] = currentRight + delayedLeft * feedback;

                effectState.delayPosition = (effectState.delayPosition + 2) % effectState.delayBuffer.Length;
            }
        }

        public void ApplyEqualizer(Span<float> buffer)
        {
            UpdateEqCoefficients();
            for (int i = 0; i < buffer.Length; i += 2)
            {
                float left = buffer[i];
                float right = buffer[i + 1];
                eqBass.Process(ref left, ref right);
                eqMid.Process(ref left, ref right);
                eqTreble.Process(ref left, ref right);
                buffer[i] = left;
                buffer[i + 1] = right;
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
                        distortedSample = (float)(distortedSample > 0 ? 1.0 - Math.Exp(-distortedSample) : -1.0 + Math.Exp(distortedSample));
                        break;
                    case DistortionType.Saturation:
                        distortedSample = (float)(Math.Atan(distortedSample) * (2.0 / Math.PI));
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
                if (effectState.bitCrusherCounter >= rateReduction)
                {
                    effectState.bitCrusherCounter = 0;
                    effectState.bitCrusherLastSample = (float)(step * Math.Floor(buffer[i] / step + 0.5));
                }
                buffer[i] = effectState.bitCrusherLastSample;
                effectState.bitCrusherCounter++;
            }
        }

        private bool ApplyEqualizerGpu(Span<float> buffer, GraphicsDevice device)
        {
            if (device == null) return false;
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
            catch (Exception ex)
            {
                Logger.Error(LogMessages.GpuEqualizerFailed, ex);
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
                device.For(n / size, new FftShader(buffer, n, size, inverse));
            }
        }

        public void NormalizeAudio(Span<float> buffer)
        {
            float maxAbs = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                maxAbs = Math.Max(maxAbs, Math.Abs(buffer[i]));
            }
            if (maxAbs > config.Audio.NormalizationThreshold)
            {
                var scale = config.Audio.NormalizationLevel / maxAbs;
                for (int i = 0; i < buffer.Length; i++)
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
                    effectState.ReleaseBuffers();
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

        private class BiquadFilter
        {
            private readonly int sampleRate;
            private double a0, a1, a2, b1, b2;
            private float x1L, x2L, y1L, y2L;
            private float x1R, x2R, y1R, y2R;

            public BiquadFilter(int sr) { this.sampleRate = sr; }

            public void Reset()
            {
                x1L = x2L = y1L = y2L = 0;
                x1R = x2R = y1R = y2R = 0;
            }

            public void SetPeakingEq(float freq, float q, float gainDb)
            {
                double w0 = 2 * Math.PI * freq / sampleRate;
                double alpha = Math.Sin(w0) / (2 * q);
                double A = Math.Pow(10, gainDb / 40);

                double b0_ = 1 + alpha * A;
                double b1_ = -2 * Math.Cos(w0);
                double b2_ = 1 - alpha * A;
                double a0_ = 1 + alpha / A;
                double a1_ = -2 * Math.Cos(w0);
                double a2_ = 1 - alpha / A;

                a0 = b0_ / a0_; a1 = b1_ / a0_; a2 = b2_ / a0_;
                b1 = a1_ / a0_; b2 = a2_ / a0_;
            }

            public void Process(ref float left, ref float right)
            {
                float outL = (float)(a0 * left + a1 * x1L + a2 * x2L - b1 * y1L - b2 * y2L);
                x2L = x1L; x1L = left; y2L = y1L; y1L = outL;
                left = outL;

                float outR = (float)(a0 * right + a1 * x1R + a2 * x2R - b1 * y1R - b2 * y2R);
                x2R = x1R; x1R = right; y2R = y1R; y1R = outR;
                right = outR;
            }
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

            public void Reset()
            {
                foreach (var filter in combFilters) filter.Reset();
                foreach (var filter in allPassFilters) filter.Reset();
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

                public void Reset()
                {
                    Array.Clear(buffer, 0, buffer.Length);
                    bufferPos = 0;
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
                    float delayed = output * feedback + input;
                    buffer[bufferPos] = delayed * (1.0f - settings.Damping);
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
                    float output = delayed - input;
                    buffer[bufferPos] = input + delayed * 0.5f;
                    if (++bufferPos >= buffer.Length) bufferPos = 0;
                    return output;
                }
            }
        }
    }
}
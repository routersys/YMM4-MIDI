using System;
using System.Numerics;
using ComputeSharp;

namespace MIDI
{
    public partial class EffectsProcessor
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;
        private float[] impulseResponse = null!;

        public EffectsProcessor(MidiConfiguration config, int sampleRate)
        {
            this.config = config;
            this.sampleRate = sampleRate;
            LoadImpulseResponse();
        }

        private void LoadImpulseResponse()
        {
            impulseResponse = new float[sampleRate];
            var random = new Random();
            for (int i = 0; i < impulseResponse.Length; i++)
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

        public void ApplyAudioEnhancements(float[] buffer)
        {
            if (config.Effects.EnableConvolutionReverb && GraphicsDevice.GetDefault() != null)
            {
                ApplyConvolutionReverbGpu(buffer);
            }
            if (config.Effects.EnableDCOffsetRemoval) ApplyDCOffsetRemoval(buffer);
            if (config.Effects.EnableCompression) ApplyCompression(buffer);
            if (config.Effects.EnableReverb) ApplyGlobalReverb(buffer);
            if (config.Effects.EnableEqualizer) ApplyEqualizer(buffer);
            if (config.Effects.EnableLimiter) ApplyLimiter(buffer);
        }

        public void ApplyConvolutionReverbGpu(float[] buffer)
        {
            using var device = GraphicsDevice.GetDefault();
            using var gpuBuffer = device.AllocateReadWriteBuffer<float>(buffer);
            using var gpuImpulseResponse = device.AllocateReadOnlyBuffer<float>(impulseResponse);

            device.For(gpuBuffer.Length, new ConvolutionShader(gpuBuffer, gpuImpulseResponse, gpuImpulseResponse.Length));

            gpuBuffer.CopyTo(buffer);
        }

        [ThreadGroupSize(DefaultThreadGroupSizes.X)]
        [GeneratedComputeShaderDescriptor]
        public readonly partial struct ConvolutionShader : IComputeShader
        {
            private readonly ReadWriteBuffer<float> audioBuffer;
            private readonly ReadOnlyBuffer<float> impulseResponse;
            private readonly int irLength;

            public ConvolutionShader(ReadWriteBuffer<float> audioBuffer, ReadOnlyBuffer<float> impulseResponse, int irLength)
            {
                this.audioBuffer = audioBuffer;
                this.impulseResponse = impulseResponse;
                this.irLength = irLength;
            }

            public void Execute()
            {
                int i = ThreadIds.X;
                float result = 0;
                for (int j = 0; j < irLength; j++)
                {
                    int sampleIndex = i - j;
                    if (sampleIndex >= 0)
                    {
                        result += audioBuffer[sampleIndex] * impulseResponse[j];
                    }
                }
                audioBuffer[i] = Hlsl.Clamp(result, -1.0f, 1.0f);
            }
        }

        public void ApplyDCOffsetRemoval(float[] buffer)
        {
            float lastIn = 0;
            float lastOut = 0;
            const float alpha = 0.995f;

            for (int i = 0; i < buffer.Length; i++)
            {
                float currentIn = buffer[i];
                float currentOut = currentIn - lastIn + alpha * lastOut;
                buffer[i] = currentOut;
                lastIn = currentIn;
                lastOut = currentOut;
            }
        }

        public void ApplyCompression(float[] buffer)
        {
            var threshold = config.Effects.CompressionThreshold;
            var ratio = config.Effects.CompressionRatio;
            var attack = config.Effects.CompressionAttack;
            var release = config.Effects.CompressionRelease;

            var attackCoeff = (float)Math.Exp(-1.0 / (attack * sampleRate));
            var releaseCoeff = (float)Math.Exp(-1.0 / (release * sampleRate));

            int vectorSize = Vector<float>.Count;
            var thresholdVec = new Vector<float>(threshold);

            for (int i = 0; i <= buffer.Length - vectorSize; i += vectorSize)
            {
                var inputVec = new Vector<float>(buffer, i);
                var absInputVec = Vector.Abs(inputVec);

                if (Vector.GreaterThanAll(absInputVec, thresholdVec))
                {
                    var excessVec = absInputVec - thresholdVec;
                    var compressedExcessVec = excessVec / ratio;
                    var gainVec = (thresholdVec + compressedExcessVec) / absInputVec;
                    (inputVec * gainVec).CopyTo(buffer, i);
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

        public void ApplyLimiter(float[] buffer)
        {
            float threshold = config.Effects.LimiterThreshold;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Math.Max(-threshold, Math.Min(threshold, buffer[i]));
            }
        }

        public void ApplyGlobalReverb(float[] buffer)
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
                    var currentVec = new Vector<float>(buffer, i);
                    var delayedVec = new Vector<float>(buffer, i - delaySamples);
                    (currentVec + delayedVec * decayVec).CopyTo(buffer, i);
                }
                for (int i = buffer.Length - (buffer.Length % vectorSize); i < buffer.Length; i++)
                {
                    buffer[i] += buffer[i - delaySamples] * decay;
                }
            }
        }

        public void ApplyEqualizer(float[] buffer)
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
                    var sampleVec = new Vector<float>(buffer, i);
                    (sampleVec * bassGainVec).CopyTo(buffer, i);
                }
            }
        }

        public void NormalizeAudio(float[] buffer)
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
                    var vec = new Vector<float>(buffer, i);
                    (vec * scaleVec).CopyTo(buffer, i);
                }
                for (int i = buffer.Length - (buffer.Length % vectorSize); i < buffer.Length; i++)
                {
                    buffer[i] *= scale;
                }
            }
        }
    }
}
using System;

namespace MIDI
{
    public class EffectsProcessor
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;

        public EffectsProcessor(MidiConfiguration config, int sampleRate)
        {
            this.config = config;
            this.sampleRate = sampleRate;
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

            return output;
        }

        public void ApplyAudioEnhancements(float[] buffer)
        {
            if (config.Effects.EnableCompression) ApplyCompression(buffer);
            if (config.Effects.EnableReverb) ApplyGlobalReverb(buffer);
            if (config.Effects.EnableEqualizer) ApplyEqualizer(buffer);
        }

        public void ApplyCompression(float[] buffer)
        {
            var threshold = config.Effects.CompressionThreshold;
            var ratio = config.Effects.CompressionRatio;
            var attack = config.Effects.CompressionAttack;
            var release = config.Effects.CompressionRelease;

            var envelope = 0.0f;
            var attackCoeff = (float)Math.Exp(-1.0 / (attack * sampleRate));
            var releaseCoeff = (float)Math.Exp(-1.0 / (release * sampleRate));

            for (int i = 0; i < buffer.Length; i++)
            {
                var input = Math.Abs(buffer[i]);
                var targetEnv = input > threshold ? input : envelope * releaseCoeff;

                if (targetEnv > envelope)
                    envelope += (targetEnv - envelope) * (1 - attackCoeff);
                else
                    envelope += (targetEnv - envelope) * (1 - releaseCoeff);

                if (envelope > threshold)
                {
                    var excess = envelope - threshold;
                    var compressedExcess = excess / ratio;
                    var gain = (threshold + compressedExcess) / envelope;
                    buffer[i] *= gain;
                }
            }
        }

        public void ApplyGlobalReverb(float[] buffer)
        {
            var delayMs = config.Effects.ReverbDelay;
            var delaySamples = (int)(delayMs * sampleRate / 1000);
            var decay = config.Effects.ReverbDecay;

            if (delaySamples > 0 && delaySamples < buffer.Length)
            {
                for (int i = delaySamples; i < buffer.Length; i++)
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

            for (int i = 0; i < buffer.Length; i += 2)
            {
                buffer[i] *= bassGain;
                buffer[i + 1] *= bassGain;
            }
        }

        public void NormalizeAudio(float[] buffer)
        {
            float maxAbs = 0;
            for (int i = 0; i < buffer.Length; i++)
            {
                var absVal = Math.Abs(buffer[i]);
                if (absVal > maxAbs) maxAbs = absVal;
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
    }
}
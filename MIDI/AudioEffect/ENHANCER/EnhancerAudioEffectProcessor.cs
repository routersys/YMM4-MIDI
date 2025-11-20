using System;
using YukkuriMovieMaker.Player.Audio.Effects;
using MIDI.AudioEffect.ENHANCER.Algorithms;

namespace MIDI.AudioEffect.ENHANCER
{
    public class EnhancerAudioEffectProcessor : AudioEffectProcessorBase
    {
        private readonly EnhancerAudioEffect item;
        private readonly TimeSpan duration;

        public override int Hz => Input?.Hz ?? 0;
        public override long Duration => (long)(duration.TotalSeconds * Input?.Hz ?? 0) * 2;

        private IEnhancerAlgorithm? enhancerLeft;
        private IEnhancerAlgorithm? enhancerRight;

        private int lastHz = 0;
        private double lastDrive = -1;
        private double lastFrequency = -1;

        public EnhancerAudioEffectProcessor(EnhancerAudioEffect item, TimeSpan duration)
        {
            this.item = item;
            this.duration = duration;
        }

        private void UpdateParameters(int sampleRate)
        {
            if (Input == null || sampleRate == 0) return;

            var drive = item.Drive;
            var freq = item.Frequency;

            if (sampleRate != lastHz || enhancerLeft == null || enhancerRight == null)
            {
                enhancerLeft = new ExciterAlgorithm(sampleRate);
                enhancerRight = new ExciterAlgorithm(sampleRate);
                lastHz = sampleRate;
                lastDrive = -1;
                lastFrequency = -1;
            }

            if (drive != lastDrive)
            {
                enhancerLeft.Drive = drive;
                enhancerRight.Drive = drive;
                lastDrive = drive;
            }

            if (freq != lastFrequency)
            {
                enhancerLeft.Frequency = freq;
                enhancerRight.Frequency = freq;
                lastFrequency = freq;
            }
        }

        protected override void seek(long position)
        {
            Input?.Seek(position);
            enhancerLeft?.Reset();
            enhancerRight?.Reset();
        }

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input == null) return 0;

            int samplesRead = Input.Read(destBuffer, offset, count);
            if (samplesRead == 0) return 0;

            int sampleRate = Hz;
            UpdateParameters(sampleRate);

            if (enhancerLeft == null || enhancerRight == null) return samplesRead;

            var mix = (float)Math.Clamp(item.Mix / 100.0, 0.0, 1.0);
            var gainDb = item.Gain;
            var postGain = (float)Math.Pow(10.0, gainDb / 20.0);

            float inputPeak = 0;
            float outputPeak = 0;
            float wetL, wetR, dryL, dryR;

            for (int i = 0; i < samplesRead; i += 2)
            {
                dryL = destBuffer[offset + i];
                dryR = destBuffer[offset + i + 1];

                inputPeak = Math.Max(inputPeak, Math.Max(Math.Abs(dryL), Math.Abs(dryR)));

                wetL = enhancerLeft.Process(dryL);
                wetR = enhancerRight.Process(dryR);

                float outL = (dryL + wetL * mix) * postGain;
                float outR = (dryR + wetR * mix) * postGain;

                outputPeak = Math.Max(outputPeak, Math.Max(Math.Abs(outL), Math.Abs(outR)));

                destBuffer[offset + i] = outL;
                destBuffer[offset + i + 1] = outR;
            }

            item.InputLevel = (inputPeak == 0) ? -60.0 : Math.Max(-60.0, 20.0 * Math.Log10(inputPeak));
            item.OutputLevel = (outputPeak == 0) ? -60.0 : Math.Max(-60.0, 20.0 * Math.Log10(outputPeak));

            return samplesRead;
        }
    }
}
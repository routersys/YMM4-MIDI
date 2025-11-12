using System;
using YukkuriMovieMaker.Player.Audio.Effects;
using MIDI.AudioEffect.DELAY.Algorithms;

namespace MIDI.AudioEffect.DELAY
{
    public class DelayAudioEffectProcessor : AudioEffectProcessorBase
    {
        private readonly DelayAudioEffect item;
        private readonly TimeSpan duration;

        public override int Hz => Input?.Hz ?? 0;
        public override long Duration => (long)(duration.TotalSeconds * Input?.Hz ?? 0) * 2;

        private IDelayProcessor? delayLeft;
        private IDelayProcessor? delayRight;

        private int lastHz = 0;
        private double lastDelayTime = -1;
        private double lastFeedback = -1;

        public DelayAudioEffectProcessor(DelayAudioEffect item, TimeSpan duration)
        {
            this.item = item;
            this.duration = duration;
        }

        private void UpdateParameters(long frame, long length, int sampleRate)
        {
            if (Input == null || sampleRate == 0) return;

            var delayTime = item.DelayTime;
            var feedback = item.Feedback;

            if (sampleRate != lastHz || delayLeft == null || delayRight == null)
            {
                delayLeft = new SimpleDelayProcessor(sampleRate, 2000);
                delayRight = new SimpleDelayProcessor(sampleRate, 2000);
                lastHz = sampleRate;
                lastDelayTime = -1;
                lastFeedback = -1;
            }

            if (delayTime != lastDelayTime)
            {
                delayLeft.DelayTimeMs = delayTime;
                delayRight.DelayTimeMs = delayTime;
                lastDelayTime = delayTime;
            }

            if (feedback != lastFeedback)
            {
                delayLeft.Feedback = feedback / 100.0;
                delayRight.Feedback = feedback / 100.0;
                lastFeedback = feedback;
            }
        }

        protected override void seek(long position)
        {
            Input?.Seek(position);
            delayLeft?.Reset();
            delayRight?.Reset();
        }

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input == null) return 0;

            int samplesRead = Input.Read(destBuffer, offset, count);
            if (samplesRead == 0) return 0;

            long currentFrame = Position / 2;
            long totalFrame = Duration / 2;
            int sampleRate = Hz;

            UpdateParameters(currentFrame, totalFrame, sampleRate);

            if (delayLeft == null || delayRight == null) return samplesRead;

            var mix = (float)item.Mix / 100.0f;

            float inputPeak = 0;
            float outputPeak = 0;
            float wetL, wetR, dryL, dryR;

            for (int i = 0; i < samplesRead; i += 2)
            {
                dryL = destBuffer[offset + i];
                dryR = destBuffer[offset + i + 1];

                inputPeak = Math.Max(inputPeak, Math.Max(Math.Abs(dryL), Math.Abs(dryR)));

                wetL = delayLeft.Process(dryL);
                wetR = delayRight.Process(dryR);

                float outL = (dryL * (1.0f - mix) + wetL * mix);
                float outR = (dryR * (1.0f - mix) + wetR * mix);

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
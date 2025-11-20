using System;
using YukkuriMovieMaker.Player.Audio.Effects;
using MIDI.AudioEffect.MULTIBAND_SATURATOR.Algorithms;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR
{
    public class MultibandSaturatorAudioEffectProcessor : AudioEffectProcessorBase
    {
        private readonly MultibandSaturatorAudioEffect item;
        private readonly TimeSpan duration;

        public override int Hz => Input?.Hz ?? 0;
        public override long Duration => (long)(duration.TotalSeconds * Input?.Hz ?? 0) * 2;

        private BiquadLr4Filter? crossLow;
        private BiquadLr4Filter? crossHigh;

        private SaturatorUnit? satLow;
        private SaturatorUnit? satMid;
        private SaturatorUnit? satHigh;

        private int lastHz = 0;
        private double lastFreqLM = -1;
        private double lastFreqMH = -1;

        public MultibandSaturatorAudioEffectProcessor(MultibandSaturatorAudioEffect item, TimeSpan duration)
        {
            this.item = item;
            this.duration = duration;
        }

        private void Initialize(int sampleRate)
        {
            if (sampleRate != lastHz || crossLow == null)
            {
                crossLow = new BiquadLr4Filter(sampleRate, false);
                crossHigh = new BiquadLr4Filter(sampleRate, true);

                satLow = new SaturatorUnit();
                satMid = new SaturatorUnit();
                satHigh = new SaturatorUnit();

                lastHz = sampleRate;
                lastFreqLM = -1;
                lastFreqMH = -1;
            }

            if (Math.Abs(item.FreqLowMid - lastFreqLM) > 0.1)
            {
                crossLow!.SetFrequency(item.FreqLowMid);
                lastFreqLM = item.FreqLowMid;
            }

            if (Math.Abs(item.FreqMidHigh - lastFreqMH) > 0.1)
            {
                crossHigh!.SetFrequency(item.FreqMidHigh);
                lastFreqMH = item.FreqMidHigh;
            }

            satLow!.Drive = item.LowDrive;
            satLow.LevelDb = item.LowLevel;

            satMid!.Drive = item.MidDrive;
            satMid.LevelDb = item.MidLevel;

            satHigh!.Drive = item.HighDrive;
            satHigh.LevelDb = item.HighLevel;
        }

        protected override void seek(long position)
        {
            Input?.Seek(position);
            if (crossLow != null)
            {
                crossLow.Reset();
                crossHigh?.Reset();
                satLow?.Reset();
                satMid?.Reset();
                satHigh?.Reset();
            }
        }

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input == null) return 0;
            int samplesRead = Input.Read(destBuffer, offset, count);
            if (samplesRead == 0) return 0;

            Initialize(Hz);

            if (satLow == null || crossLow == null || crossHigh == null) return samplesRead;

            float mix = (float)Math.Clamp(item.MasterMix / 100.0, 0.0, 1.0);
            float gain = (float)Math.Pow(10.0, item.MasterGain / 20.0);

            float inMax = 0;
            float outMax = 0;
            float lowMax = 0, midMax = 0, highMax = 0;

            for (int i = 0; i < samplesRead; i += 2)
            {
                float l = destBuffer[offset + i];
                float r = destBuffer[offset + i + 1];
                inMax = Math.Max(inMax, Math.Max(Math.Abs(l), Math.Abs(r)));

                float lowL, lowR;
                float midHighL, midHighR;

                crossLow.Process(l, r, out lowL, out lowR);
                midHighL = l - lowL;
                midHighR = r - lowR;

                float midL, midR;
                float highL, highR;

                crossHigh.Process(midHighL, midHighR, out highL, out highR);
                midL = midHighL - highL;
                midR = midHighR - highR;

                float sLowL = satLow.Process(lowL);
                float sLowR = satLow.Process(lowR);

                float sMidL = satMid!.Process(midL);
                float sMidR = satMid.Process(midR);

                float sHighL = satHigh!.Process(highL);
                float sHighR = satHigh.Process(highR);

                lowMax = Math.Max(lowMax, Math.Max(Math.Abs(sLowL), Math.Abs(sLowR)));
                midMax = Math.Max(midMax, Math.Max(Math.Abs(sMidL), Math.Abs(sMidR)));
                highMax = Math.Max(highMax, Math.Max(Math.Abs(sHighL), Math.Abs(sHighR)));

                float wetL = sLowL + sMidL + sHighL;
                float wetR = sLowR + sMidR + sHighR;

                float finalL = (l * (1.0f - mix) + wetL * mix) * gain;
                float finalR = (r * (1.0f - mix) + wetR * mix) * gain;

                outMax = Math.Max(outMax, Math.Max(Math.Abs(finalL), Math.Abs(finalR)));

                destBuffer[offset + i] = finalL;
                destBuffer[offset + i + 1] = finalR;
            }

            item.InputLevel = AmpToDb(inMax);
            item.OutputLevel = AmpToDb(outMax);
            item.LowMeter = AmpToDb(lowMax);
            item.MidMeter = AmpToDb(midMax);
            item.HighMeter = AmpToDb(highMax);

            return samplesRead;
        }

        private double AmpToDb(float amp) => amp <= 1e-5 ? -60 : Math.Max(-60, 20 * Math.Log10(amp));
    }
}
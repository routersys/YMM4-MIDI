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

        private LinkwitzRileyFilter? lpLow;
        private LinkwitzRileyFilter? hpLow;
        private LinkwitzRileyFilter? lpMid;
        private LinkwitzRileyFilter? hpHigh;

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

        private void UpdateParameters(int sampleRate)
        {
            if (sampleRate != lastHz || lpLow == null)
            {
                lpLow = new LinkwitzRileyFilter(sampleRate, false);
                hpLow = new LinkwitzRileyFilter(sampleRate, true);
                lpMid = new LinkwitzRileyFilter(sampleRate, false);
                hpHigh = new LinkwitzRileyFilter(sampleRate, true);

                satLow = new SaturatorUnit();
                satMid = new SaturatorUnit();
                satHigh = new SaturatorUnit();

                lastHz = sampleRate;
                lastFreqLM = -1;
                lastFreqMH = -1;
            }

            if (item.FreqLowMid != lastFreqLM)
            {
                lpLow!.SetFrequency(item.FreqLowMid);
                hpLow!.SetFrequency(item.FreqLowMid);
                lastFreqLM = item.FreqLowMid;
            }

            if (item.FreqMidHigh != lastFreqMH)
            {
                lpMid!.SetFrequency(item.FreqMidHigh);
                hpHigh!.SetFrequency(item.FreqMidHigh);
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
            lpLow?.Reset(); hpLow?.Reset(); lpMid?.Reset(); hpHigh?.Reset();
        }

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input == null) return 0;
            int samplesRead = Input.Read(destBuffer, offset, count);
            if (samplesRead == 0) return 0;

            int sampleRate = Hz;
            UpdateParameters(sampleRate);

            if (satLow == null) return samplesRead;

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

                float lowL = lpLow!.ProcessLeft(l);
                float lowR = lpLow!.ProcessRight(r);

                float midHighL = hpLow!.ProcessLeft(l);
                float midHighR = hpLow!.ProcessRight(r);

                float midL = lpMid!.ProcessLeft(midHighL);
                float midR = lpMid!.ProcessRight(midHighR);

                float highL = hpHigh!.ProcessLeft(midHighL);
                float highR = hpHigh!.ProcessRight(midHighR);

                float satLowL = satLow!.Process(lowL);
                float satLowR = satLow!.Process(lowR);

                float satMidL = satMid!.Process(midL);
                float satMidR = satMid!.Process(midR);

                float satHighL = satHigh!.Process(highL);
                float satHighR = satHigh!.Process(highR);

                lowMax = Math.Max(lowMax, Math.Max(Math.Abs(satLowL), Math.Abs(satLowR)));
                midMax = Math.Max(midMax, Math.Max(Math.Abs(satMidL), Math.Abs(satMidR)));
                highMax = Math.Max(highMax, Math.Max(Math.Abs(satHighL), Math.Abs(satHighR)));

                float wetL = satLowL + satMidL + satHighL;
                float wetR = satLowR + satMidR + satHighR;

                float finalL = (l * (1 - mix) + wetL * mix) * gain;
                float finalR = (r * (1 - mix) + wetR * mix) * gain;

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

        private double AmpToDb(float amp) => amp <= 0 ? -60 : Math.Max(-60, 20 * Math.Log10(amp));
    }
}
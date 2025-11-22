using System;
using YukkuriMovieMaker.Player.Audio.Effects;
using MIDI.AudioEffect.AMP_SIMULATOR.Algorithms;

namespace MIDI.AudioEffect.AMP_SIMULATOR
{
    public class AmpSimulatorAudioEffectProcessor : AudioEffectProcessorBase
    {
        private readonly AmpSimulatorAudioEffect item;
        private readonly TimeSpan duration;

        public override int Hz => Input?.Hz ?? 0;
        public override long Duration => (long)(duration.TotalSeconds * Input?.Hz ?? 0) * 2;

        private PolyphaseFirOversampler? preamp;
        private PolyphaseFirOversampler? poweramp;
        private TubeModel? tubes;
        private ToneStackFilter? toneStack;
        private CabinetSimulator? cab;
        private DynamicsProcessor? dynamics;
        private SvfFilter? preFilter;
        private SvfFilter? postFilter;
        private DcBlocker? dcBlocker;

        private int currentSr = 0;

        public AmpSimulatorAudioEffectProcessor(AmpSimulatorAudioEffect item, TimeSpan duration)
        {
            this.item = item;
            this.duration = duration;
        }

        private void Init(int sr)
        {
            if (sr < 10000) sr = 44100;

            if (sr != currentSr || preamp == null)
            {
                preamp = new PolyphaseFirOversampler();
                poweramp = new PolyphaseFirOversampler();
                tubes = new TubeModel();
                toneStack = new ToneStackFilter();
                cab = new CabinetSimulator();
                dynamics = new DynamicsProcessor();
                preFilter = new SvfFilter();
                postFilter = new SvfFilter();
                dcBlocker = new DcBlocker();
                currentSr = sr;
            }

            toneStack?.UpdateCoefficients(item.Bass, item.Middle, item.Treble, sr);
            cab?.UpdateCoefficients(item.CabinetResonance, item.CabinetBright, sr);

            preFilter?.SetHighPass(20.0, 0.707, sr);
            postFilter?.SetLowPass(18000.0, 0.707, sr);
        }

        protected override void seek(long position)
        {
            Input?.Seek(position);
            preamp?.Reset();
            poweramp?.Reset();
            tubes?.Reset();
            toneStack?.Reset();
            cab?.Reset();
            dynamics?.Reset();
            preFilter?.Reset();
            postFilter?.Reset();
            dcBlocker?.Reset();
        }

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input == null) return 0;
            int read = Input.Read(destBuffer, offset, count);
            if (read == 0) return 0;

            int sr = Hz;
            if (sr <= 0) sr = 44100;
            Init(sr);

            double inGain = Math.Pow(10.0, (item.InputGain - 50.0) * 0.8 / 20.0) * 4.0;
            double masterGain = Math.Pow(10.0, (item.MasterVolume - 50.0) * 0.6 / 20.0) * 1.0;
            double bias = (item.Bias - 50.0) * 0.05;
            double sag = item.Sag * 0.01;

            double maxIn = 0.0;
            double maxOut = 0.0;

            for (int i = 0; i < read; i += 2)
            {
                double L = destBuffer[offset + i];
                double R = destBuffer[offset + i + 1];
                double mono = (L + R) * 0.5;

                maxIn = Math.Max(maxIn, Math.Abs(mono));

                double s = mono;

                s = dynamics!.ProcessGate(s, sr);
                s = preFilter!.Process(s);

                s *= inGain;

                s = preamp!.ProcessHiRes((float)s, x => tubes!.ProcessTriodeADAA(x, bias));

                s = toneStack!.Process(s);

                s *= masterGain;

                s = dynamics!.ProcessSag(s, sag, sr);

                s = poweramp!.ProcessHiRes((float)s, x => tubes!.ProcessPentodeADAA(x, bias * 0.5, sag));

                s = cab!.Process(s);

                s = postFilter!.Process(s);
                s = dcBlocker!.Process(s);
                s = dynamics!.ProcessLimiter(s);

                maxOut = Math.Max(maxOut, Math.Abs(s));

                destBuffer[offset + i] = (float)s;
                destBuffer[offset + i + 1] = (float)s;
            }

            item.InputLevel = (maxIn < 1e-9) ? -60.0 : 20.0 * Math.Log10(maxIn);
            item.OutputLevel = (maxOut < 1e-9) ? -60.0 : 20.0 * Math.Log10(maxOut);

            return read;
        }
    }
}
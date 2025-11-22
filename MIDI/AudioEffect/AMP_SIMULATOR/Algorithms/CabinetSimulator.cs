using System;
using System.Collections.Generic;

namespace MIDI.AudioEffect.AMP_SIMULATOR.Algorithms
{
    public class CabinetSimulator
    {
        private readonly SvfFilter hpf = new();
        private readonly SvfFilter lpf = new();
        private readonly SvfFilter res1 = new();
        private readonly SvfFilter res2 = new();
        private readonly SvfFilter res3 = new();
        private readonly SvfFilter air = new();

        public void Reset()
        {
            hpf.Reset();
            lpf.Reset();
            res1.Reset();
            res2.Reset();
            res3.Reset();
            air.Reset();
        }

        public void UpdateCoefficients(double resonance, double bright, int sampleRate)
        {
            double r = resonance / 100.0;
            double b = bright / 100.0;

            hpf.SetHighPass(60.0, 0.707, sampleRate);
            lpf.SetLowPass(5000.0 + b * 4000.0, 0.707, sampleRate);

            res1.SetBandPass(110.0, 2.0 + r, sampleRate);
            res2.SetBandPass(350.0, 1.0, sampleRate);
            res3.SetBandPass(2500.0, 1.0 + b, sampleRate);

            air.SetHighShelf(8000.0, 0.707, -6.0 + b * 3.0, sampleRate);
        }

        public double Process(double input)
        {
            double s = input;

            s = hpf.Process(s);
            s = lpf.Process(s);

            double body = res1.Process(input) * 2.0;
            double box = res2.Process(input) * 0.8;
            double edge = res3.Process(input) * 0.6;

            double mixed = s + body + box + edge;

            return air.Process(mixed);
        }
    }
}
using System;

namespace MIDI.AudioEffect.AMP_SIMULATOR.Algorithms
{
    public class ToneStackFilter
    {
        private readonly SvfFilter bass = new();
        private readonly SvfFilter mid = new();
        private readonly SvfFilter treb = new();

        public void Reset()
        {
            bass.Reset();
            mid.Reset();
            treb.Reset();
        }

        public void UpdateCoefficients(double b, double m, double t, int sr)
        {
            double bDb = (b - 50.0) * 0.4;
            double mDb = (m - 50.0) * 0.3;
            double tDb = (t - 50.0) * 0.4;

            bass.SetLowShelf(100.0, 0.6, bDb, sr);
            mid.SetPeaking(500.0, 0.5, mDb, sr);
            treb.SetHighShelf(3000.0, 0.6, tDb, sr);
        }

        public double Process(double input)
        {
            double s = input;
            s = bass.Process(s);
            s = mid.Process(s);
            s = treb.Process(s);
            return s;
        }
    }
}
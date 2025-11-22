using System;

namespace MIDI.AudioEffect.AMP_SIMULATOR.Algorithms
{
    public class SvfFilter
    {
        private double ic1eq;
        private double ic2eq;
        private double g;
        private double k;
        private double a1;
        private double a2;
        private double a3;
        private double m0, m1, m2;

        public void Reset()
        {
            ic1eq = 0.0;
            ic2eq = 0.0;
        }

        public void SetLowPass(double frequency, double q, int sampleRate)
        {
            PrepareCoefficients(frequency, q, sampleRate);
            m0 = 0.0;
            m1 = 0.0;
            m2 = 1.0;
        }

        public void SetHighPass(double frequency, double q, int sampleRate)
        {
            PrepareCoefficients(frequency, q, sampleRate);
            m0 = 1.0;
            m1 = -k;
            m2 = -1.0;
        }

        public void SetBandPass(double frequency, double q, int sampleRate)
        {
            PrepareCoefficients(frequency, q, sampleRate);
            m0 = 0.0;
            m1 = 1.0;
            m2 = 0.0;
        }

        public void SetPeaking(double frequency, double q, double gainDb, int sampleRate)
        {
            PrepareCoefficients(frequency, q, sampleRate);
            double A = Math.Pow(10.0, gainDb / 20.0);
            m0 = 1.0;
            m1 = k * (A * A - 1.0);
            m2 = 0.0;
        }

        public void SetLowShelf(double frequency, double q, double gainDb, int sampleRate)
        {
            PrepareCoefficients(frequency, q, sampleRate);
            double A = Math.Pow(10.0, gainDb / 20.0);
            m0 = 1.0;
            m1 = k * (A - 1.0);
            m2 = A * A - 1.0;
        }

        public void SetHighShelf(double frequency, double q, double gainDb, int sampleRate)
        {
            PrepareCoefficients(frequency, q, sampleRate);
            double A = Math.Pow(10.0, gainDb / 20.0);
            m0 = A * A;
            m1 = k * (1.0 - A) * A;
            m2 = 1.0 - A * A;
        }

        private void PrepareCoefficients(double frequency, double q, int sampleRate)
        {
            if (frequency < 10.0) frequency = 10.0;
            if (frequency > sampleRate * 0.495) frequency = sampleRate * 0.495;
            if (q < 0.1) q = 0.1;

            g = Math.Tan(Math.PI * frequency / sampleRate);
            k = 1.0 / q;
            a1 = 1.0 / (1.0 + g * (g + k));
            a2 = g * a1;
            a3 = g * a2;
        }

        public double Process(double v0)
        {
            double v3 = v0 - ic2eq;
            double v1 = a1 * ic1eq + a2 * v3;
            double v2 = ic2eq + a2 * ic1eq + a3 * v3;

            ic1eq = 2.0 * v1 - ic1eq;
            ic2eq = 2.0 * v2 - ic2eq;

            if (double.IsNaN(ic1eq) || double.IsInfinity(ic1eq)) ic1eq = 0.0;
            if (double.IsNaN(ic2eq) || double.IsInfinity(ic2eq)) ic2eq = 0.0;

            return m0 * v0 + m1 * v1 + m2 * v2;
        }
    }
}
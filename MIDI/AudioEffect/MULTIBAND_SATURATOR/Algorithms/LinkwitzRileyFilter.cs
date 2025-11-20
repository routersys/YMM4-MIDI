using System;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.Algorithms
{
    public class LinkwitzRileyFilter
    {
        private readonly int sampleRate;
        private double frequency;

        private double b0, b1, b2, a1, a2;
        private double x1_l, x2_l, y1_l, y2_l;
        private double x1_r, x2_r, y1_r, y2_r;

        private double b0_2, b1_2, b2_2, a1_2, a2_2;
        private double x1_l_2, x2_l_2, y1_l_2, y2_l_2;
        private double x1_r_2, x2_r_2, y1_r_2, y2_r_2;

        private readonly bool isHighPass;

        public LinkwitzRileyFilter(int sampleRate, bool isHighPass)
        {
            this.sampleRate = sampleRate;
            this.isHighPass = isHighPass;
            SetFrequency(1000);
        }

        public void SetFrequency(double freq)
        {
            this.frequency = Math.Clamp(freq, 20, sampleRate / 2.0 - 100);
            CalculateCoefficients();
        }

        private void CalculateCoefficients()
        {
            double omega = Math.PI * frequency / sampleRate;
            double kappa = omega / Math.Tan(omega);
            double delta = kappa * kappa + Math.Sqrt(2) * kappa + 1;

            if (isHighPass)
            {
                b0 = kappa * kappa / delta;
                b1 = -2 * kappa * kappa / delta;
                b2 = kappa * kappa / delta;
            }
            else
            {
                b0 = 1 / delta;
                b1 = 2 / delta;
                b2 = 1 / delta;
            }

            a1 = 2 * (1 - kappa * kappa) / delta;
            a2 = (kappa * kappa - Math.Sqrt(2) * kappa + 1) / delta;

            b0_2 = b0; b1_2 = b1; b2_2 = b2;
            a1_2 = a1; a2_2 = a2;
        }

        public void Reset()
        {
            x1_l = x2_l = y1_l = y2_l = 0;
            x1_r = x2_r = y1_r = y2_r = 0;
            x1_l_2 = x2_l_2 = y1_l_2 = y2_l_2 = 0;
            x1_r_2 = x2_r_2 = y1_r_2 = y2_r_2 = 0;
        }

        public float ProcessLeft(float input)
        {
            double y1 = b0 * input + b1 * x1_l + b2 * x2_l - a1 * y1_l - a2 * y2_l;
            x2_l = x1_l; x1_l = input; y2_l = y1_l; y1_l = y1;

            double output = b0_2 * y1 + b1_2 * x1_l_2 + b2_2 * x2_l_2 - a1_2 * y1_l_2 - a2_2 * y2_l_2;
            x2_l_2 = x1_l_2; x1_l_2 = y1; y2_l_2 = y1_l_2; y1_l_2 = output;

            return (float)output;
        }

        public float ProcessRight(float input)
        {
            double y1 = b0 * input + b1 * x1_r + b2 * x2_r - a1 * y1_r - a2 * y2_r;
            x2_r = x1_r; x1_r = input; y2_r = y1_r; y1_r = y1;

            double output = b0_2 * y1 + b1_2 * x1_r_2 + b2_2 * x2_r_2 - a1_2 * y1_r_2 - a2_2 * y2_r_2;
            x2_r_2 = x1_r_2; x1_r_2 = y1; y2_r_2 = y1_r_2; y1_r_2 = output;

            return (float)output;
        }
    }
}
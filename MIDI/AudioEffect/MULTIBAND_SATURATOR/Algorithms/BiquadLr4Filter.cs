using System;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.Algorithms
{
    public class BiquadLr4Filter
    {
        private double b0, b1, b2, a1, a2;
        private double x1_l, x2_l, y1_l, y2_l;
        private double x1_r, x2_r, y1_r, y2_r;

        private double x1_l_2, x2_l_2, y1_l_2, y2_l_2;
        private double x1_r_2, x2_r_2, y1_r_2, y2_r_2;

        private readonly bool isHighPass;
        private readonly int sampleRate;

        public BiquadLr4Filter(int sampleRate, bool isHighPass)
        {
            this.sampleRate = sampleRate;
            this.isHighPass = isHighPass;
            SetFrequency(1000);
        }

        public void SetFrequency(double frequency)
        {
            double f = Math.Clamp(frequency, 20.0, sampleRate * 0.49);
            double w0 = 2.0 * Math.PI * f / sampleRate;
            double cosW0 = Math.Cos(w0);
            double sinW0 = Math.Sin(w0);
            double alpha = sinW0 / (2.0 * 0.70710678);

            if (isHighPass)
            {
                b0 = (1 + cosW0) / 2;
                b1 = -(1 + cosW0);
                b2 = (1 + cosW0) / 2;
                a1 = -2 * cosW0;
                a2 = 1 - alpha;
            }
            else
            {
                b0 = (1 - cosW0) / 2;
                b1 = 1 - cosW0;
                b2 = (1 - cosW0) / 2;
                a1 = -2 * cosW0;
                a2 = 1 - alpha;
            }

            double a0 = 1 + alpha;
            b0 /= a0;
            b1 /= a0;
            b2 /= a0;
            a1 /= a0;
            a2 /= a0;
        }

        public void Reset()
        {
            x1_l = x2_l = y1_l = y2_l = 0;
            x1_r = x2_r = y1_r = y2_r = 0;
            x1_l_2 = x2_l_2 = y1_l_2 = y2_l_2 = 0;
            x1_r_2 = x2_r_2 = y1_r_2 = y2_r_2 = 0;
        }

        public void Process(float inputL, float inputR, out float outL, out float outR)
        {
            double ol1 = ProcessBiquad(inputL, ref x1_l, ref x2_l, ref y1_l, ref y2_l);
            outL = (float)ProcessBiquad(ol1, ref x1_l_2, ref x2_l_2, ref y1_l_2, ref y2_l_2);

            double or1 = ProcessBiquad(inputR, ref x1_r, ref x2_r, ref y1_r, ref y2_r);
            outR = (float)ProcessBiquad(or1, ref x1_r_2, ref x2_r_2, ref y1_r_2, ref y2_r_2);
        }

        private double ProcessBiquad(double input, ref double x1, ref double x2, ref double y1, ref double y2)
        {
            double output = b0 * input + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;

            if (double.IsNaN(output) || double.IsInfinity(output)) output = 0;

            x2 = x1;
            x1 = input;
            y2 = y1;
            y1 = output;
            return output;
        }
    }
}
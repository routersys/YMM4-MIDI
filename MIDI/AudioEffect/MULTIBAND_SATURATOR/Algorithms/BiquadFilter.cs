using System;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.Algorithms
{
    public class BiquadFilter
    {
        private double b0, b1, b2, a1, a2;
        private double x1, x2, y1, y2;

        public void SetLowPass(double frequency, double sampleRate, double q)
        {
            double w0 = 2.0 * Math.PI * frequency / sampleRate;
            double alpha = Math.Sin(w0) / (2.0 * q);
            double cosW0 = Math.Cos(w0);

            b0 = (1.0 - cosW0) / 2.0;
            b1 = 1.0 - cosW0;
            b2 = (1.0 - cosW0) / 2.0;
            double a0 = 1.0 + alpha;
            a1 = -2.0 * cosW0;
            a2 = 1.0 - alpha;

            b0 /= a0;
            b1 /= a0;
            b2 /= a0;
            a1 /= a0;
            a2 /= a0;
        }

        public float Process(float input)
        {
            double output = b0 * input + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;

            if (double.IsNaN(output)) output = 0;

            x2 = x1;
            x1 = input;
            y2 = y1;
            y1 = output;
            return (float)output;
        }

        public void Reset()
        {
            x1 = x2 = y1 = y2 = 0;
        }
    }
}
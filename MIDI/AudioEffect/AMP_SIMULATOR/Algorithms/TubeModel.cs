using System;

namespace MIDI.AudioEffect.AMP_SIMULATOR.Algorithms
{
    public class TubeModel
    {
        private const double Epsilon = 1e-9;
        private double x1_triode = 0.0;
        private double x1_pentode = 0.0;

        public void Reset()
        {
            x1_triode = 0.0;
            x1_pentode = 0.0;
        }

        public double ProcessTriodeADAA(double x, double bias)
        {
            double input = x + bias;
            double y = 0.0;

            if (Math.Abs(input - x1_triode) < Epsilon)
            {
                y = HardClip(0.5 * (input + x1_triode));
            }
            else
            {
                y = (Antiderivative(input) - Antiderivative(x1_triode)) / (input - x1_triode);
            }

            x1_triode = input;
            return y;
        }

        public double ProcessPentodeADAA(double x, double bias, double sag)
        {
            double drive = 1.0 + sag;
            double input = (x * drive) + bias;

            double y = 0.0;

            if (Math.Abs(input - x1_pentode) < Epsilon)
            {
                y = AsymmetricClip(0.5 * (input + x1_pentode));
            }
            else
            {
                y = (AntiderivativeAsymmetric(input) - AntiderivativeAsymmetric(x1_pentode)) / (input - x1_pentode);
            }

            x1_pentode = input;
            return y * 0.9;
        }

        private static double Antiderivative(double x)
        {
            return Math.Log(Math.Cosh(x));
        }

        private static double HardClip(double x)
        {
            return Math.Tanh(x);
        }

        private static double AsymmetricClip(double x)
        {
            if (x > 0.5)
            {
                return Math.Tanh(x);
            }
            else
            {
                double t = Math.Tanh(x);
                return t + 0.2 * t * t;
            }
        }

        private static double AntiderivativeAsymmetric(double x)
        {
            if (x > 0.5)
            {
                return Math.Log(Math.Cosh(x));
            }
            else
            {
                double lnc = Math.Log(Math.Cosh(x));
                double th = Math.Tanh(x);
                return lnc + 0.2 * (x * th - lnc);
            }
        }
    }
}
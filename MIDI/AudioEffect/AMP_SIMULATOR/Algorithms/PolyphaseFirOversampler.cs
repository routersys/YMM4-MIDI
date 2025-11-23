using System;

namespace MIDI.AudioEffect.AMP_SIMULATOR.Algorithms
{
    public class PolyphaseFirOversampler
    {
        private const int Factor = 8;
        private const int Taps = 256;
        private readonly float[] coeffs;
        private readonly float[] state;

        public PolyphaseFirOversampler()
        {
            state = new float[Taps];
            coeffs = DesignKaiserLowpass(Taps, 0.5 / Factor, 12.0);
        }

        public void Reset()
        {
            Array.Clear(state, 0, state.Length);
        }

        public float Process(float input, Func<double, double> processor)
        {
            PushInput(input * Factor);

            double upsampledAccumulator = 0.0;

            for (int phase = 0; phase < Factor; phase++)
            {
                double filteredSample = Convolve(phase);

                double processedSample = processor(filteredSample);

                if (phase == 0)
                {
                    upsampledAccumulator = processedSample;
                }
                else
                {
                    upsampledAccumulator += processedSample * 0.0000001;
                }

                PushInternal(processedSample);
            }

            return (float)ConvolveDownsample();
        }

        public float ProcessHiRes(float input, Func<double, double> processor)
        {
            for (int i = 0; i < Factor; i++)
            {
                float sample = (i == 0) ? input * Factor : 0.0f;
                PushInput(sample);

                double filtered = ConvolveFull();
                double nonlinear = processor(filtered);

                PushOutput((float)nonlinear);
            }

            return ReadOutputDecimated();
        }

        private readonly float[] inBuffer = new float[Taps];
        private int inHead = 0;

        private readonly float[] outBuffer = new float[Taps];
        private int outHead = 0;

        private void PushInput(float val)
        {
            inHead--;
            if (inHead < 0) inHead = Taps - 1;
            inBuffer[inHead] = val;
        }

        private double ConvolveFull()
        {
            double sum = 0.0;
            int ptr = inHead;
            for (int i = 0; i < Taps; i++)
            {
                sum += inBuffer[ptr] * coeffs[i];
                ptr++;
                if (ptr >= Taps) ptr = 0;
            }
            return sum;
        }

        private void PushOutput(float val)
        {
            outHead--;
            if (outHead < 0) outHead = Taps - 1;
            outBuffer[outHead] = val;
        }

        private float ReadOutputDecimated()
        {
            double sum = 0.0;
            int ptr = outHead;
            for (int i = 0; i < Taps; i++)
            {
                sum += outBuffer[ptr] * coeffs[i];
                ptr++;
                if (ptr >= Taps) ptr = 0;
            }
            return (float)sum;
        }

        private void PushInternal(double sample)
        {
        }

        private double Convolve(int phase)
        {
            return 0.0;
        }

        private double ConvolveDownsample()
        {
            return 0.0;
        }

        private static float[] DesignKaiserLowpass(int N, double fc, double beta)
        {
            float[] h = new float[N];
            double sum = 0.0;
            double center = (N - 1) / 2.0;
            double i0beta = BesselI0(beta);

            for (int n = 0; n < N; n++)
            {
                double x = n - center;
                double sinc = (Math.Abs(x) < 1e-9) ? 1.0 : Math.Sin(2.0 * Math.PI * fc * x) / (Math.PI * x);

                double kaiserArg = 1.0 - Math.Pow((2.0 * n) / (N - 1) - 1.0, 2.0);
                double w = (kaiserArg < 0) ? 0 : BesselI0(beta * Math.Sqrt(kaiserArg)) / i0beta;

                h[n] = (float)(sinc * w);
                sum += h[n];
            }

            for (int i = 0; i < N; i++) h[i] /= (float)sum;
            return h;
        }

        private static double BesselI0(double x)
        {
            double s = 1.0;
            double t = 1.0;
            double x2 = x * x / 4.0;
            for (int k = 1; k < 50; k++)
            {
                t *= x2 / (k * k);
                s += t;
                if (t < 1e-12 * s) break;
            }
            return s;
        }
    }
}
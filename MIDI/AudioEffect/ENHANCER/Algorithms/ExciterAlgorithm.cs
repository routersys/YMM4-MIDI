using System;

namespace MIDI.AudioEffect.ENHANCER.Algorithms
{
    public class ExciterAlgorithm : IEnhancerAlgorithm
    {
        private readonly int sampleRate;
        private double drive;
        private double frequency;
        private float filterState;
        private double alpha;

        public ExciterAlgorithm(int sampleRate)
        {
            this.sampleRate = sampleRate;
            filterState = 0;
            frequency = 1000;
            UpdateCoefficients();
        }

        public double Drive
        {
            get => drive;
            set => drive = Math.Max(0, value);
        }

        public double Frequency
        {
            get => frequency;
            set
            {
                frequency = Math.Clamp(value, 100, 20000);
                UpdateCoefficients();
            }
        }

        private void UpdateCoefficients()
        {
            double rc = 1.0 / (2.0 * Math.PI * frequency);
            double dt = 1.0 / sampleRate;
            alpha = rc / (rc + dt);
        }

        public void Reset()
        {
            filterState = 0;
        }

        public float Process(float input)
        {
            float highPass = (float)(alpha * (filterState + input - filterState));
            float nextFilterState = highPass;

            highPass = input - filterState;
            filterState = (float)(filterState + alpha * (input - filterState));

            float processed = highPass;

            if (drive > 0)
            {
                float x = highPass * (float)(1.0 + drive / 10.0);
                processed = (float)Math.Tanh(x);
            }

            return processed;
        }
    }
}
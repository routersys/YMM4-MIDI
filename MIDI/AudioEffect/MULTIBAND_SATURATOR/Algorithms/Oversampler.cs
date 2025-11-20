using System;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.Algorithms
{
    public class Oversampler
    {
        private const int Factor = 8;
        private readonly BiquadFilter[] upFilters1;
        private readonly BiquadFilter[] upFilters2;
        private readonly BiquadFilter[] downFilters1;
        private readonly BiquadFilter[] downFilters2;
        private readonly float[] buffer;

        public Oversampler()
        {
            upFilters1 = [new BiquadFilter(), new BiquadFilter()];
            upFilters2 = [new BiquadFilter(), new BiquadFilter()];
            downFilters1 = [new BiquadFilter(), new BiquadFilter()];
            downFilters2 = [new BiquadFilter(), new BiquadFilter()];
            buffer = new float[Factor];
        }

        public void SetSampleRate(int sampleRate)
        {
            double targetFreq = sampleRate * Factor;
            double cutoff = sampleRate * 0.45;

            foreach (var f in upFilters1) f.SetLowPass(cutoff, targetFreq, 0.54);
            foreach (var f in upFilters2) f.SetLowPass(cutoff, targetFreq, 1.31);

            foreach (var f in downFilters1) f.SetLowPass(cutoff, targetFreq, 0.54);
            foreach (var f in downFilters2) f.SetLowPass(cutoff, targetFreq, 1.31);
        }

        public void Reset()
        {
            foreach (var f in upFilters1) f.Reset();
            foreach (var f in upFilters2) f.Reset();
            foreach (var f in downFilters1) f.Reset();
            foreach (var f in downFilters2) f.Reset();
            Array.Clear(buffer, 0, buffer.Length);
        }

        public float Process(float input, Func<float, float> processor)
        {
            float upSampled = input * Factor;

            for (int i = 0; i < Factor; i++)
            {
                float sample = (i == 0) ? upSampled : 0;

                foreach (var f in upFilters1) sample = f.Process(sample);
                foreach (var f in upFilters2) sample = f.Process(sample);

                sample = processor(sample);

                foreach (var f in downFilters1) sample = f.Process(sample);
                foreach (var f in downFilters2) sample = f.Process(sample);

                buffer[i] = sample;
            }

            return buffer[0];
        }
    }
}
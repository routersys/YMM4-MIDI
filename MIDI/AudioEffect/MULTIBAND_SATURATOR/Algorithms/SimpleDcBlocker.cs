using System;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.Algorithms
{
    public class SimpleDcBlocker
    {
        private double x1, y1;
        private const double R = 0.995;

        public float Process(float input)
        {
            double output = input - x1 + R * y1;
            x1 = input;
            y1 = output;
            return (float)output;
        }

        public void Reset()
        {
            x1 = y1 = 0;
        }
    }
}
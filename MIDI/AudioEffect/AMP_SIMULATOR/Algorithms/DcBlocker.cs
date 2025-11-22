using System;

namespace MIDI.AudioEffect.AMP_SIMULATOR.Algorithms
{
    public class DcBlocker
    {
        private double z1;

        public void Reset()
        {
            z1 = 0.0;
        }

        public double Process(double x)
        {
            double y = x - z1 + 0.999 * y_prev;
            z1 = x;
            y_prev = y;
            return y;
        }

        private double y_prev;
    }
}
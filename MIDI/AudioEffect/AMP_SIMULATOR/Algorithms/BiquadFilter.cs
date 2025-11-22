using System;

namespace MIDI.AudioEffect.AMP_SIMULATOR.Algorithms
{
    public class BiquadFilter
    {
        public void Reset() { }
        public float Process(float i) { return i; }
        public void SetLowPass(double f, double q, int s) { }
        public void SetHighPass(double f, double q, int s) { }
        public void SetPeaking(double f, double q, double g, int s) { }
        public void SetLowShelf(double f, double q, double g, int s) { }
        public void SetHighShelf(double f, double q, double g, int s) { }
    }
}
using System;

namespace MIDI.AudioEffect.AMP_SIMULATOR.Algorithms
{
    public class NoiseGate
    {
        public void Initialize(int sr) { }
        public void Reset() { }
        public float Process(float x) { return x; }
    }
}
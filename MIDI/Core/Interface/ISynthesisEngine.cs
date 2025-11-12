using System.Collections.Generic;

namespace MIDI
{
    public interface ISynthesisEngine
    {
        double GetFrequency(int noteNumber, double pitchBend, int centOffset, double pitchLfo = 0.0);
        float GenerateWaveform(WaveformType type, double frequency, double time, float amplitude, double envelope, int noteNumber, string? wavetableFile = null);
        double GetLfoValue(LfoSettings lfo, double time);
        double GenerateBasicWaveform(string waveType, double frequency, double time);
        InstrumentSettings GetInstrumentSettings(int channel, int program, Dictionary<int, InstrumentSettings> instrumentSettings);
        Dictionary<int, InstrumentSettings> InitializeInstrumentSettings();
    }
}
using System;
using System.Collections.Generic;
using System.Linq;

namespace MIDI
{
    public class SynthesisEngine
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;

        public SynthesisEngine(MidiConfiguration config, int sampleRate)
        {
            this.config = config;
            this.sampleRate = sampleRate;
        }

        public double GetFrequency(int noteNumber, double pitchBend)
        {
            var semitoneOffset = pitchBend;
            var baseFreq = config.Synthesis.A4Frequency * Math.Pow(2.0, (noteNumber - 69.0 + semitoneOffset) / 12.0);
            return Math.Max(config.Synthesis.MinFrequency, Math.Min(config.Synthesis.MaxFrequency, baseFreq));
        }

        public float GenerateWaveform(WaveformType type, double frequency, double time, float amplitude, double envelope)
        {
            var phase = 2 * Math.PI * frequency * time;
            double wave = type switch
            {
                WaveformType.Sine => Math.Sin(phase),
                WaveformType.Square => Math.Sign(Math.Sin(phase)),
                WaveformType.Sawtooth => (2 * (frequency * time - Math.Floor(frequency * time + 0.5))),
                WaveformType.Triangle => (4 * Math.Abs((frequency * time - Math.Floor(frequency * time + 0.75) + 0.25) % 1 - 0.5) - 1),
                WaveformType.Organ => Math.Sin(phase) + 0.5 * Math.Sin(2 * phase) + 0.25 * Math.Sin(3 * phase),
                WaveformType.Noise => (Random.Shared.NextDouble() * 2 - 1),
                _ => Math.Sin(phase)
            };
            return (float)(amplitude * envelope * wave);
        }

        public double GenerateBasicWaveform(string waveType, double frequency, double time)
        {
            var phase = 2 * Math.PI * frequency * time;
            return waveType.ToLower() switch
            {
                "sine" => Math.Sin(phase),
                "square" => Math.Sign(Math.Sin(phase)),
                "sawtooth" => (2 * (frequency * time - Math.Floor(frequency * time + 0.5))),
                "triangle" => (4 * Math.Abs((frequency * time - Math.Floor(frequency * time + 0.75) + 0.25) % 1 - 0.5) - 1),
                _ => Math.Sin(phase)
            };
        }

        public InstrumentSettings GetInstrumentSettings(int channel, int program, Dictionary<int, InstrumentSettings> instrumentSettings)
        {
            var key = channel == 9 ? -1 : program;

            var customInstrument = config.CustomInstruments.FirstOrDefault(c => c.Program == key);
            if (customInstrument != null)
            {
                return CreateInstrumentFromConfig(customInstrument);
            }

            ArgumentNullException.ThrowIfNull(instrumentSettings);

            return instrumentSettings.TryGetValue(key, out var settings) ? settings : instrumentSettings[0];
        }

        public Dictionary<int, InstrumentSettings> InitializeInstrumentSettings()
        {
            var settings = new Dictionary<int, InstrumentSettings>();

            foreach (var preset in config.InstrumentPresets)
            {
                for (int i = preset.StartProgram; i <= preset.EndProgram; i++)
                {
                    settings[i] = new InstrumentSettings
                    {
                        WaveType = Enum.Parse<WaveformType>(preset.Waveform, true),
                        Attack = preset.Attack,
                        Decay = preset.Decay,
                        Sustain = preset.Sustain,
                        Release = preset.Release,
                        VolumeMultiplier = preset.Volume,
                        FilterType = Enum.Parse<FilterType>(preset.Filter.Type, true),
                        FilterCutoff = preset.Filter.Cutoff,
                        FilterResonance = preset.Filter.Resonance,
                        FilterModulation = preset.Filter.Modulation,
                        FilterModulationRate = preset.Filter.ModulationRate
                    };
                }
            }

            settings[-1] = new InstrumentSettings
            {
                WaveType = WaveformType.Noise,
                Attack = 0.001,
                Decay = 0.05,
                Sustain = 0.1,
                Release = 0.1,
                VolumeMultiplier = 1.2f
            };

            return settings;
        }

        private InstrumentSettings CreateInstrumentFromConfig(CustomInstrument customInst)
        {
            return new InstrumentSettings
            {
                WaveType = Enum.Parse<WaveformType>(customInst.Waveform, true),
                Attack = customInst.Attack,
                Decay = customInst.Decay,
                Sustain = customInst.Sustain,
                Release = customInst.Release,
                VolumeMultiplier = customInst.Volume,
                FilterType = Enum.Parse<FilterType>(customInst.Filter.Type, true),
                FilterCutoff = customInst.Filter.Cutoff,
                FilterResonance = customInst.Filter.Resonance,
                FilterModulation = customInst.Filter.Modulation,
                FilterModulationRate = customInst.Filter.ModulationRate
            };
        }
    }
}
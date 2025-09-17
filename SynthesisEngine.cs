using System;
using System.Collections.Generic;
using System.Linq;
using ComputeSharp;
using System.IO;
using System.Reflection;
using System.Collections.Concurrent;

namespace MIDI
{
    public struct GpuNoteData
    {
        public int NoteNumber;
        public float Velocity;
        public int Channel;
        public int StartSample;
        public int EndSample;
        public float Frequency;
        public float BaseAmplitude;
        public int WaveType;
        public float Attack;
        public float Decay;
        public float Sustain;
        public float Release;
        public int FilterType;
        public float FilterCutoff;
        public float FilterResonance;
        public float PanLeft;
        public float PanRight;
    }

    public class SynthesisEngine
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;
        private readonly Dictionary<int, Queue<float>> karplusStrongBuffers = new Dictionary<int, Queue<float>>();
        private static readonly ConcurrentDictionary<string, float[]> wavetableCache = new ConcurrentDictionary<string, float[]>();

        public SynthesisEngine(MidiConfiguration config, int sampleRate)
        {
            this.config = config;
            this.sampleRate = sampleRate;
            LoadAllWavetables();
        }

        private void LoadAllWavetables()
        {
            try
            {
                var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                var wavetableDir = Path.Combine(baseDir, config.Synthesis.WavetableDirectory);
                if (!Directory.Exists(wavetableDir))
                {
                    Directory.CreateDirectory(wavetableDir);
                    return;
                }

                foreach (var file in Directory.GetFiles(wavetableDir, "*.wav"))
                {
                    LoadWavetable(file);
                }
            }
            catch { }
        }

        private static float[] LoadWavetable(string path)
        {
            if (wavetableCache.TryGetValue(path, out var cachedTable))
            {
                return cachedTable;
            }

            try
            {
                using (var reader = new BinaryReader(new FileStream(path, FileMode.Open)))
                {
                    reader.BaseStream.Seek(44, SeekOrigin.Begin);
                    var data = new List<float>();
                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                    {
                        data.Add(reader.ReadInt16() / 32768.0f);
                    }
                    var table = data.ToArray();
                    wavetableCache.TryAdd(path, table);
                    return table;
                }
            }
            catch
            {
                return new float[0];
            }
        }

        public double GetFrequency(int noteNumber, double pitchBend, double pitchLfo = 0.0)
        {
            var semitoneOffset = pitchBend + pitchLfo;
            var baseFreq = config.Synthesis.A4Frequency * Math.Pow(2.0, (noteNumber - 69.0 + semitoneOffset) / 12.0);
            return Math.Max(config.Synthesis.MinFrequency, Math.Min(config.Synthesis.MaxFrequency, baseFreq));
        }

        public float GenerateWaveform(WaveformType type, double frequency, double time, float amplitude, double envelope, int noteNumber, string? wavetableFile = null)
        {
            var phase = frequency * time;
            var phaseIncrement = frequency / sampleRate;
            double wave;

            if (config.Synthesis.EnableBandlimitedSynthesis)
            {
                wave = type switch
                {
                    WaveformType.Sine => Math.Sin(2 * Math.PI * phase),
                    WaveformType.Square => GenerateBandlimitedSquare(phase, phaseIncrement),
                    WaveformType.Sawtooth => GenerateBandlimitedSawtooth(phase, phaseIncrement),
                    WaveformType.Triangle => GenerateBandlimitedTriangle(phase, phaseIncrement),
                    WaveformType.Organ => (Math.Sin(2 * Math.PI * phase) + 0.5 * Math.Sin(4 * Math.PI * phase) + 0.25 * Math.Sin(6 * Math.PI * phase)) / 1.75,
                    WaveformType.Noise => (Random.Shared.NextDouble() * 2 - 1),
                    WaveformType.Wavetable => GenerateWavetable(2 * Math.PI * phase),
                    WaveformType.UserWavetable => GenerateUserWavetable(2 * Math.PI * phase, wavetableFile),
                    WaveformType.Fm => GenerateFm(2 * Math.PI * phase, time),
                    WaveformType.KarplusStrong => GenerateKarplusStrong(noteNumber, frequency, time),
                    _ => Math.Sin(2 * Math.PI * phase)
                };
            }
            else
            {
                var fullPhase = 2 * Math.PI * phase;
                wave = type switch
                {
                    WaveformType.Sine => Math.Sin(fullPhase),
                    WaveformType.Square => Math.Sign(Math.Sin(fullPhase)),
                    WaveformType.Sawtooth => (2 * (phase - Math.Floor(phase + 0.5))),
                    WaveformType.Triangle => (4 * Math.Abs((phase - Math.Floor(phase + 0.75) + 0.25) % 1 - 0.5) - 1),
                    WaveformType.Organ => Math.Sin(fullPhase) + 0.5 * Math.Sin(2 * fullPhase) + 0.25 * Math.Sin(3 * fullPhase),
                    WaveformType.Noise => (Random.Shared.NextDouble() * 2 - 1),
                    WaveformType.Wavetable => GenerateWavetable(fullPhase),
                    WaveformType.UserWavetable => GenerateUserWavetable(fullPhase, wavetableFile),
                    WaveformType.Fm => GenerateFm(fullPhase, time),
                    WaveformType.KarplusStrong => GenerateKarplusStrong(noteNumber, frequency, time),
                    _ => Math.Sin(fullPhase)
                };
            }
            return (float)(amplitude * envelope * wave);
        }

        private static double PolyBlep(double phase, double phaseIncrement)
        {
            if (phase < phaseIncrement)
            {
                phase /= phaseIncrement;
                return phase + phase - phase * phase - 1.0;
            }
            if (phase > 1.0 - phaseIncrement)
            {
                phase = (phase - 1.0) / phaseIncrement;
                return phase * phase + phase + phase + 1.0;
            }
            return 0.0;
        }

        private static double GenerateBandlimitedSawtooth(double phase, double phaseIncrement)
        {
            phase = phase - Math.Floor(phase);
            double value = 2.0 * phase - 1.0;
            value -= PolyBlep(phase, phaseIncrement);
            return value;
        }

        private static double GenerateBandlimitedSquare(double phase, double phaseIncrement)
        {
            phase = phase - Math.Floor(phase);
            double value = phase < 0.5 ? 1.0 : -1.0;
            value += PolyBlep(phase, phaseIncrement);
            value -= PolyBlep((phase + 0.5) % 1.0, phaseIncrement);
            return value;
        }

        private static double GenerateBandlimitedTriangle(double phase, double phaseIncrement)
        {
            phase = phase - Math.Floor(phase);
            double value = phase < 0.5 ? 1.0 : -1.0;
            value += PolyBlep(phase, phaseIncrement);
            value -= PolyBlep((phase + 0.5) % 1.0, phaseIncrement);
            return value * phaseIncrement + (1.0 - phaseIncrement) * value;
        }

        private double GenerateWavetable(double phase)
        {
            double wave1 = Math.Sin(phase);
            double wave2 = Math.Sign(Math.Sin(phase));
            double mix = 0.5;
            return (1 - mix) * wave1 + mix * wave2;
        }

        private double GenerateUserWavetable(double phase, string? wavetableFile)
        {
            if (string.IsNullOrEmpty(wavetableFile)) return 0;

            var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var wavetableDir = Path.Combine(baseDir, config.Synthesis.WavetableDirectory);
            var fullPath = Path.Combine(wavetableDir, wavetableFile);

            var table = LoadWavetable(fullPath);
            if (table.Length == 0) return 0;

            var index = (phase / (2 * Math.PI)) * (table.Length - 1);
            var i1 = (int)index % table.Length;
            var i2 = (i1 + 1) % table.Length;
            var frac = index - Math.Floor(index);

            return table[i1] * (1.0 - frac) + table[i2] * frac;
        }

        private double GenerateFm(double phase, double time)
        {
            double modulatorFrequency = config.Synthesis.FmModulatorFrequency;
            double modulationIndex = config.Synthesis.FmModulationIndex;
            double modulator = Math.Sin(2 * Math.PI * modulatorFrequency * time);
            return Math.Sin(phase + modulationIndex * modulator);
        }

        private float GenerateKarplusStrong(int noteNumber, double frequency, double time)
        {
            int bufferSize = (int)(sampleRate / frequency);
            if (bufferSize <= 1) return 0;

            bool isNoteStart = time < (1.0 / sampleRate);

            if (!karplusStrongBuffers.ContainsKey(noteNumber) || isNoteStart)
            {
                var buffer = new Queue<float>(bufferSize);
                for (int i = 0; i < bufferSize; i++)
                {
                    buffer.Enqueue((float)(Random.Shared.NextDouble() * 2 - 1));
                }
                karplusStrongBuffers[noteNumber] = buffer;
            }

            var currentBuffer = karplusStrongBuffers[noteNumber];

            var currentValue = currentBuffer.Dequeue();
            var nextValue = (currentValue + currentBuffer.Peek()) * 0.5f * 0.996f;
            currentBuffer.Enqueue(nextValue);
            return nextValue;
        }

        public double GetLfoValue(LfoSettings lfo, double time)
        {
            var phase = lfo.Rate * time;
            return lfo.Waveform switch
            {
                LfoWaveformType.Sine => Math.Sin(2 * Math.PI * phase),
                LfoWaveformType.Square => Math.Sign(Math.Sin(2 * Math.PI * phase)),
                LfoWaveformType.Sawtooth => (2 * (phase - Math.Floor(phase + 0.5))),
                LfoWaveformType.Triangle => (4 * Math.Abs((phase - Math.Floor(phase + 0.75) + 0.25) % 1 - 0.5) - 1),
                LfoWaveformType.Noise => (Random.Shared.NextDouble() * 2 - 1),
                LfoWaveformType.RandomHold => (int)(2 * phase) % 2 == 0 ? 1 : -1,
                _ => 0
            };
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
                        FilterLfo = preset.Filter.Lfo,
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
                UserWavetableFile = customInst.UserWavetableFile,
                AmplitudeEnvelope = customInst.AmplitudeEnvelope.Select(p => new EnvelopePoint(p.Time, p.Value)).ToList(),
                Release = customInst.Release,
                VolumeMultiplier = customInst.Volume,
                FilterType = Enum.Parse<FilterType>(customInst.Filter.Type, true),
                FilterCutoff = customInst.Filter.Cutoff,
                FilterResonance = customInst.Filter.Resonance,
                FilterLfo = customInst.Filter.Lfo,
                PitchLfo = customInst.PitchLfo,
                AmplitudeLfo = customInst.AmplitudeLfo
            };
        }
    }
}
using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Collections.Generic;
using YukkuriMovieMaker.Plugin;
using System.Runtime.CompilerServices;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace MIDI
{
    public class MidiConfiguration : SettingsBase<MidiConfiguration>
    {
        public override string Name => "MIDI 設定";
        public override SettingsCategory Category => SettingsCategory.None;
        public override bool HasSettingView => true;

        [JsonIgnore]
        public override object? SettingView => new MidiSettingsView();

        private static readonly string ConfigFileName = "MidiPluginConfig.json";
        private static string ConfigPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", ConfigFileName);

        private AudioSettings _audio = new();
        public AudioSettings Audio
        {
            get => _audio;
            set
            {
                if (_audio != null) _audio.PropertyChanged -= OnNestedPropertyChanged;
                Set(ref _audio!, value ?? new AudioSettings());
                if (_audio != null) _audio.PropertyChanged += OnNestedPropertyChanged;
            }
        }

        private PerformanceSettings _performance = new();
        public PerformanceSettings Performance
        {
            get => _performance;
            set
            {
                if (_performance != null) _performance.PropertyChanged -= OnNestedPropertyChanged;
                Set(ref _performance!, value ?? new PerformanceSettings());
                if (_performance != null) _performance.PropertyChanged += OnNestedPropertyChanged;
            }
        }

        private MidiSettings _midi = new();
        public MidiSettings MIDI
        {
            get => _midi;
            set
            {
                if (_midi != null) _midi.PropertyChanged -= OnNestedPropertyChanged;
                Set(ref _midi!, value ?? new MidiSettings());
                if (_midi != null) _midi.PropertyChanged += OnNestedPropertyChanged;
            }
        }

        private SoundFontSettings _soundFont = new();
        public SoundFontSettings SoundFont
        {
            get => _soundFont;
            set
            {
                if (_soundFont != null) _soundFont.PropertyChanged -= OnNestedPropertyChanged;
                Set(ref _soundFont!, value ?? new SoundFontSettings());
                if (_soundFont != null) _soundFont.PropertyChanged += OnNestedPropertyChanged;
            }
        }

        private SfzSettings _sfz = new();
        public SfzSettings SFZ
        {
            get => _sfz;
            set
            {
                if (_sfz != null) _sfz.PropertyChanged -= OnNestedPropertyChanged;
                Set(ref _sfz!, value ?? new SfzSettings());
                if (_sfz != null) _sfz.PropertyChanged += OnNestedPropertyChanged;
            }
        }

        private SynthesisSettings _synthesis = new();
        public SynthesisSettings Synthesis
        {
            get => _synthesis;
            set
            {
                if (_synthesis != null) _synthesis.PropertyChanged -= OnNestedPropertyChanged;
                Set(ref _synthesis!, value ?? new SynthesisSettings());
                if (_synthesis != null) _synthesis.PropertyChanged += OnNestedPropertyChanged;
            }
        }

        private EffectsSettings _effects = new();
        public EffectsSettings Effects
        {
            get => _effects;
            set
            {
                if (_effects != null) _effects.PropertyChanged -= OnNestedPropertyChanged;
                Set(ref _effects!, value ?? new EffectsSettings());
                if (_effects != null) _effects.PropertyChanged += OnNestedPropertyChanged;
            }
        }

        private ObservableCollection<InstrumentPreset> _instrumentPresets = CreateDefaultInstrumentPresets();
        public ObservableCollection<InstrumentPreset> InstrumentPresets { get => _instrumentPresets; set => Set(ref _instrumentPresets, value); }

        private ObservableCollection<CustomInstrument> _customInstruments = new();
        public ObservableCollection<CustomInstrument> CustomInstruments { get => _customInstruments; set => Set(ref _customInstruments, value); }

        private DebugSettings _debug = new();
        public DebugSettings Debug { get => _debug; set => Set(ref _debug, value); }

        public override void Initialize()
        {
            Load();
            AttachEventHandlers();
        }

        private void AttachEventHandlers()
        {
            if (_audio != null) _audio.PropertyChanged += OnNestedPropertyChanged;
            if (_performance != null) _performance.PropertyChanged += OnNestedPropertyChanged;
            if (_midi != null) _midi.PropertyChanged += OnNestedPropertyChanged;
            if (_soundFont != null) _soundFont.PropertyChanged += OnNestedPropertyChanged;
            if (_sfz != null) _sfz.PropertyChanged += OnNestedPropertyChanged;
            if (_synthesis != null) _synthesis.PropertyChanged += OnNestedPropertyChanged;
            if (_effects != null) _effects.PropertyChanged += OnNestedPropertyChanged;
        }

        private void OnNestedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Save();
            MidiAudioSource.ClearCache();
        }

        public void Load()
        {
            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = new MidiConfiguration();
                defaultConfig.Save();
                CopyFrom(defaultConfig);
                return;
            }

            try
            {
                var jsonString = File.ReadAllText(ConfigPath);

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true
                };

                try
                {
                    var loadedConfig = JsonSerializer.Deserialize<MidiConfiguration>(jsonString, options);
                    if (loadedConfig != null)
                    {
                        CopyFrom(loadedConfig);
                    }
                }
                catch (JsonException)
                {
                    JsonNode? root = JsonNode.Parse(jsonString);
                    if (root?["instrumentPresets"] is JsonObject presetsObject)
                    {
                        var presetsArray = new JsonArray();
                        foreach (var prop in presetsObject)
                        {
                            if (prop.Value is JsonObject presetObj)
                            {
                                presetObj["name"] = prop.Key;
                                presetsArray.Add(presetObj.DeepClone());
                            }
                        }
                        root["instrumentPresets"] = presetsArray;
                        jsonString = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                        var loadedConfig = JsonSerializer.Deserialize<MidiConfiguration>(jsonString, options);
                        if (loadedConfig != null)
                        {
                            CopyFrom(loadedConfig);
                        }
                        Save();
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"設定ファイル読み込みエラー: {ex.Message}");
                var defaultConfig = new MidiConfiguration();
                CopyFrom(defaultConfig);
            }
        }


        private void CopyFrom(MidiConfiguration source)
        {
            Audio = source.Audio;
            Performance = source.Performance;
            MIDI = source.MIDI;
            SoundFont = source.SoundFont;
            SFZ = source.SFZ;
            Synthesis = source.Synthesis;
            Effects = source.Effects;
            InstrumentPresets = source.InstrumentPresets;
            CustomInstruments = source.CustomInstruments;
            Debug = source.Debug;
        }

        public new void Save()
        {
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var jsonString = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigPath, jsonString);
            }
            catch (Exception ex)
            {
                LogError($"設定ファイル保存エラー: {ex.Message}");
            }
        }

        public void Reload()
        {
            Load();
            MidiAudioSource.ClearCache();
            OnPropertyChanged(string.Empty);
        }

        private static ObservableCollection<InstrumentPreset> CreateDefaultInstrumentPresets()
        {
            return new ObservableCollection<InstrumentPreset>
            {
                new InstrumentPreset { Name = "Piano", StartProgram = 0, EndProgram = 7, Waveform = "Sine", Attack = 0.01, Decay = 0.3, Sustain = 0.7, Release = 0.5, Volume = 1.0f, Filter = new FilterSettings { Type = "None", Cutoff = 22050, Resonance = 1.0, Modulation = 0.0, ModulationRate = 5.0 } },
                new InstrumentPreset { Name = "ChromaticPercussion", StartProgram = 8, EndProgram = 15, Waveform = "Triangle", Attack = 0.05, Decay = 0.2, Sustain = 0.6, Release = 0.8, Volume = 0.8f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 8000, Resonance = 1.2, Modulation = 0.1, ModulationRate = 3.0 } },
                new InstrumentPreset { Name = "Organ", StartProgram = 16, EndProgram = 23, Waveform = "Organ", Attack = 0.001, Decay = 0.1, Sustain = 0.9, Release = 0.2, Volume = 0.9f, Filter = new FilterSettings { Type = "None", Cutoff = 22050, Resonance = 1.0, Modulation = 0.0, ModulationRate = 5.0 } },
                new InstrumentPreset { Name = "Guitar", StartProgram = 24, EndProgram = 31, Waveform = "Sawtooth", Attack = 0.02, Decay = 0.15, Sustain = 0.5, Release = 0.6, Volume = 0.7f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 5000, Resonance = 1.5, Modulation = 0.2, ModulationRate = 4.0 } },
                new InstrumentPreset { Name = "Bass", StartProgram = 32, EndProgram = 39, Waveform = "Square", Attack = 0.01, Decay = 0.1, Sustain = 0.8, Release = 0.3, Volume = 0.6f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 2000, Resonance = 1.3, Modulation = 0.0, ModulationRate = 5.0 } },
                new InstrumentPreset { Name = "Strings", StartProgram = 40, EndProgram = 47, Waveform = "Sawtooth", Attack = 0.3, Decay = 0.2, Sustain = 0.8, Release = 1.0, Volume = 0.8f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 12000, Resonance = 1.1, Modulation = 0.05, ModulationRate = 2.0 } },
                new InstrumentPreset { Name = "Ensemble", StartProgram = 48, EndProgram = 55, Waveform = "Triangle", Attack = 0.2, Decay = 0.3, Sustain = 0.7, Release = 0.8, Volume = 0.7f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 8000, Resonance = 1.0, Modulation = 0.1, ModulationRate = 1.5 } },
                new InstrumentPreset { Name = "Brass", StartProgram = 56, EndProgram = 63, Waveform = "Square", Attack = 0.05, Decay = 0.1, Sustain = 0.9, Release = 0.2, Volume = 0.9f, Filter = new FilterSettings { Type = "BandPass", Cutoff = 4000, Resonance = 1.4, Modulation = 0.15, ModulationRate = 6.0 } },
                new InstrumentPreset { Name = "Reed", StartProgram = 64, EndProgram = 71, Waveform = "Sawtooth", Attack = 0.02, Decay = 0.15, Sustain = 0.8, Release = 0.4, Volume = 0.8f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 6000, Resonance = 1.3, Modulation = 0.1, ModulationRate = 4.5 } },
                new InstrumentPreset { Name = "Pipe", StartProgram = 72, EndProgram = 79, Waveform = "Sine", Attack = 0.1, Decay = 0.2, Sustain = 0.9, Release = 0.3, Volume = 0.7f, Filter = new FilterSettings { Type = "HighPass", Cutoff = 1000, Resonance = 1.0, Modulation = 0.05, ModulationRate = 3.0 } },
                new InstrumentPreset { Name = "SynthLead", StartProgram = 80, EndProgram = 87, Waveform = "Square", Attack = 0.001, Decay = 0.05, Sustain = 0.6, Release = 0.2, Volume = 0.8f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 8000, Resonance = 1.8, Modulation = 0.3, ModulationRate = 7.0 } },
                new InstrumentPreset { Name = "SynthPad", StartProgram = 88, EndProgram = 95, Waveform = "Triangle", Attack = 0.5, Decay = 0.3, Sustain = 0.8, Release = 1.5, Volume = 0.6f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 10000, Resonance = 1.1, Modulation = 0.2, ModulationRate = 1.0 } },
                new InstrumentPreset { Name = "SynthEffects", StartProgram = 96, EndProgram = 103, Waveform = "Noise", Attack = 0.1, Decay = 0.2, Sustain = 0.5, Release = 0.8, Volume = 0.5f, Filter = new FilterSettings { Type = "BandPass", Cutoff = 2000, Resonance = 2.0, Modulation = 0.5, ModulationRate = 10.0 } },
                new InstrumentPreset { Name = "Ethnic", StartProgram = 104, EndProgram = 111, Waveform = "Sawtooth", Attack = 0.05, Decay = 0.3, Sustain = 0.6, Release = 0.7, Volume = 0.8f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 7000, Resonance = 1.2, Modulation = 0.1, ModulationRate = 3.5 } },
                new InstrumentPreset { Name = "Percussive", StartProgram = 112, EndProgram = 119, Waveform = "Noise", Attack = 0.001, Decay = 0.1, Sustain = 0.3, Release = 0.2, Volume = 1.0f, Filter = new FilterSettings { Type = "BandPass", Cutoff = 3000, Resonance = 1.5, Modulation = 0.0, ModulationRate = 5.0 } },
                new InstrumentPreset { Name = "SoundEffects", StartProgram = 120, EndProgram = 127, Waveform = "Noise", Attack = 0.01, Decay = 0.5, Sustain = 0.2, Release = 1.0, Volume = 0.7f, Filter = new FilterSettings { Type = "HighPass", Cutoff = 500, Resonance = 1.0, Modulation = 0.8, ModulationRate = 15.0 } }
            };
        }

        private void LogError(string message)
        {
            try
            {
                var logPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "config_errors.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch { }
        }
    }

    public class AudioSettings : INotifyPropertyChanged
    {
        private int _sampleRate = 44100;
        public int SampleRate { get => _sampleRate; set => SetField(ref _sampleRate, value); }

        private float _masterVolume = 0.8f;
        public float MasterVolume { get => _masterVolume; set => SetField(ref _masterVolume, value); }

        private bool _enableNormalization = true;
        public bool EnableNormalization { get => _enableNormalization; set => SetField(ref _enableNormalization, value); }

        private float _normalizationThreshold = 1.0f;
        public float NormalizationThreshold { get => _normalizationThreshold; set => SetField(ref _normalizationThreshold, value); }

        private float _normalizationLevel = 0.95f;
        public float NormalizationLevel { get => _normalizationLevel; set => SetField(ref _normalizationLevel, value); }

        private bool _enableGlobalFadeOut;
        public bool EnableGlobalFadeOut { get => _enableGlobalFadeOut; set => SetField(ref _enableGlobalFadeOut, value); }

        private double _globalFadeOutSeconds = 0.05;
        public double GlobalFadeOutSeconds { get => _globalFadeOutSeconds; set => SetField(ref _globalFadeOutSeconds, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class GpuSettings : INotifyPropertyChanged
    {
        private bool _enableGpuSynthesis;
        public bool EnableGpuSynthesis { get => _enableGpuSynthesis; set => SetField(ref _enableGpuSynthesis, value); }

        private bool _enableGpuEqualizer;
        public bool EnableGpuEqualizer { get => _enableGpuEqualizer; set => SetField(ref _enableGpuEqualizer, value); }

        private bool _enableGpuEffectsChain;
        public bool EnableGpuEffectsChain { get => _enableGpuEffectsChain; set => SetField(ref _enableGpuEffectsChain, value); }

        private bool _enableGpuConvolutionReverb;
        public bool EnableGpuConvolutionReverb { get => _enableGpuConvolutionReverb; set => SetField(ref _enableGpuConvolutionReverb, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class PerformanceSettings : INotifyPropertyChanged
    {
        private int _bufferSize = 1024;
        public int BufferSize { get => _bufferSize; set => SetField(ref _bufferSize, value); }

        private bool _enableParallelProcessing = true;
        public bool EnableParallelProcessing { get => _enableParallelProcessing; set => SetField(ref _enableParallelProcessing, value); }

        private int _maxThreads = Environment.ProcessorCount;
        public int MaxThreads { get => _maxThreads; set => SetField(ref _maxThreads, value); }

        private int _maxPolyphony = 256;
        public int MaxPolyphony { get => _maxPolyphony; set => SetField(ref _maxPolyphony, value); }

        private double _initialSyncDurationSeconds = 15.0;
        public double InitialSyncDurationSeconds { get => _initialSyncDurationSeconds; set => SetField(ref _initialSyncDurationSeconds, value); }

        private GpuSettings _gpu = new();
        public GpuSettings GPU
        {
            get => _gpu;
            set
            {
                if (_gpu != null) _gpu.PropertyChanged -= OnNestedPropertyChanged;
                SetField(ref _gpu!, value ?? new GpuSettings());
                if (_gpu != null) _gpu.PropertyChanged += OnNestedPropertyChanged;
            }
        }

        public PerformanceSettings()
        {
            if (_gpu != null) _gpu.PropertyChanged += OnNestedPropertyChanged;
        }

        private void OnNestedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GPU)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class MidiSettings : INotifyPropertyChanged
    {
        private int _defaultTempo = 500000;
        public int DefaultTempo { get => _defaultTempo; set => SetField(ref _defaultTempo, value); }

        private double _pitchBendRange = 2.0;
        public double PitchBendRange { get => _pitchBendRange; set => SetField(ref _pitchBendRange, value); }

        private int _minVelocity = 1;
        public int MinVelocity { get => _minVelocity; set => SetField(ref _minVelocity, value); }

        private bool _processControlChanges = true;
        public bool ProcessControlChanges { get => _processControlChanges; set => SetField(ref _processControlChanges, value); }

        private bool _processPitchBend = true;
        public bool ProcessPitchBend { get => _processPitchBend; set => SetField(ref _processPitchBend, value); }

        private bool _processProgramChanges = true;
        public bool ProcessProgramChanges { get => _processProgramChanges; set => SetField(ref _processProgramChanges, value); }

        private ObservableCollection<int> _excludedChannels = new();
        public ObservableCollection<int> ExcludedChannels { get => _excludedChannels; set => SetField(ref _excludedChannels, value); }

        private ObservableCollection<int> _excludedTracks = new();
        public ObservableCollection<int> ExcludedTracks { get => _excludedTracks; set => SetField(ref _excludedTracks, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class SoundFontRule : INotifyPropertyChanged
    {
        private string _soundFontFile = string.Empty;
        public string SoundFontFile { get => _soundFontFile; set => SetField(ref _soundFontFile, value); }

        private double? _minDurationSeconds;
        public double? MinDurationSeconds { get => _minDurationSeconds; set => SetField(ref _minDurationSeconds, value); }

        private double? _maxDurationSeconds;
        public double? MaxDurationSeconds { get => _maxDurationSeconds; set => SetField(ref _maxDurationSeconds, value); }

        private int? _minTrackCount;
        public int? MinTrackCount { get => _minTrackCount; set => SetField(ref _minTrackCount, value); }

        private int? _maxTrackCount;
        public int? MaxTrackCount { get => _maxTrackCount; set => SetField(ref _maxTrackCount, value); }

        private ObservableCollection<int> _requiredPrograms = new();
        public ObservableCollection<int> RequiredPrograms { get => _requiredPrograms; set => SetField(ref _requiredPrograms, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class SoundFontSettings : INotifyPropertyChanged
    {
        private bool _enableSoundFont = true;
        public bool EnableSoundFont { get => _enableSoundFont; set => SetField(ref _enableSoundFont, value); }

        private bool _useDefaultSoundFont = true;
        public bool UseDefaultSoundFont { get => _useDefaultSoundFont; set => SetField(ref _useDefaultSoundFont, value); }

        private string _defaultSoundFontDirectory = "SoundFonts";
        public string DefaultSoundFontDirectory { get => _defaultSoundFontDirectory; set => SetField(ref _defaultSoundFontDirectory, value); }

        private string _preferredSoundFont = string.Empty;
        public string PreferredSoundFont { get => _preferredSoundFont; set => SetField(ref _preferredSoundFont, value); }

        private bool _fallbackToSynthesis = true;
        public bool FallbackToSynthesis { get => _fallbackToSynthesis; set => SetField(ref _fallbackToSynthesis, value); }

        private ObservableCollection<SoundFontRule> _rules = new();
        public ObservableCollection<SoundFontRule> Rules { get => _rules; set => SetField(ref _rules, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class SfzProgramMap : INotifyPropertyChanged
    {
        private int _program;
        public int Program { get => _program; set => SetField(ref _program, value); }

        private string _filePath = string.Empty;
        public string FilePath { get => _filePath; set => SetField(ref _filePath, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class SfzSettings : INotifyPropertyChanged
    {
        private bool _enableSfz;
        public bool EnableSfz { get => _enableSfz; set => SetField(ref _enableSfz, value); }

        private string _sfzSearchPath = "SFZ";
        public string SfzSearchPath { get => _sfzSearchPath; set => SetField(ref _sfzSearchPath, value); }

        private ObservableCollection<SfzProgramMap> _programMaps = new();
        public ObservableCollection<SfzProgramMap> ProgramMaps { get => _programMaps; set => SetField(ref _programMaps, value); }


        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class SynthesisSettings : INotifyPropertyChanged
    {
        private string _defaultWaveform = "Sine";
        public string DefaultWaveform { get => _defaultWaveform; set => SetField(ref _defaultWaveform, value); }

        private double _a4Frequency = 440.0;
        public double A4Frequency { get => _a4Frequency; set => SetField(ref _a4Frequency, value); }

        private double _minFrequency = 20.0;
        public double MinFrequency { get => _minFrequency; set => SetField(ref _minFrequency, value); }

        private double _maxFrequency = 20000.0;
        public double MaxFrequency { get => _maxFrequency; set => SetField(ref _maxFrequency, value); }

        private double _envelopeScale = 1.0;
        public double EnvelopeScale { get => _envelopeScale; set => SetField(ref _envelopeScale, value); }

        private double _defaultAttack = 0.005;
        public double DefaultAttack { get => _defaultAttack; set => SetField(ref _defaultAttack, value); }

        private double _defaultDecay = 0.2;
        public double DefaultDecay { get => _defaultDecay; set => SetField(ref _defaultDecay, value); }

        private double _defaultSustain = 0.7;
        public double DefaultSustain { get => _defaultSustain; set => SetField(ref _defaultSustain, value); }

        private double _defaultRelease = 0.01;
        public double DefaultRelease { get => _defaultRelease; set => SetField(ref _defaultRelease, value); }

        private double _fmModulatorFrequency = 5.0;
        public double FmModulatorFrequency { get => _fmModulatorFrequency; set => SetField(ref _fmModulatorFrequency, value); }

        private double _fmModulationIndex = 1.0;
        public double FmModulationIndex { get => _fmModulationIndex; set => SetField(ref _fmModulationIndex, value); }

        private bool _enableEnvelopeSmoothing = true;
        public bool EnableEnvelopeSmoothing { get => _enableEnvelopeSmoothing; set => SetField(ref _enableEnvelopeSmoothing, value); }

        private double _smoothingAttackSeconds = 0.005;
        public double SmoothingAttackSeconds { get => _smoothingAttackSeconds; set => SetField(ref _smoothingAttackSeconds, value); }

        private double _smoothingReleaseSeconds = 0.01;
        public double SmoothingReleaseSeconds { get => _smoothingReleaseSeconds; set => SetField(ref _smoothingReleaseSeconds, value); }

        private bool _enableAntiPop;
        public bool EnableAntiPop { get => _enableAntiPop; set => SetField(ref _enableAntiPop, value); }

        private double _antiPopAttackSeconds = 0.001;
        public double AntiPopAttackSeconds { get => _antiPopAttackSeconds; set => SetField(ref _antiPopAttackSeconds, value); }

        private double _antiPopReleaseSeconds = 0.005;
        public double AntiPopReleaseSeconds { get => _antiPopReleaseSeconds; set => SetField(ref _antiPopReleaseSeconds, value); }

        private bool _enableBandlimitedSynthesis = true;
        public bool EnableBandlimitedSynthesis { get => _enableBandlimitedSynthesis; set => SetField(ref _enableBandlimitedSynthesis, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class EffectsSettings : INotifyPropertyChanged
    {
        private bool _enableEffects = true;
        public bool EnableEffects { get => _enableEffects; set => SetField(ref _enableEffects, value); }

        private bool _enableCompression;
        public bool EnableCompression { get => _enableCompression; set => SetField(ref _enableCompression, value); }

        private float _compressionThreshold = 0.8f;
        public float CompressionThreshold { get => _compressionThreshold; set => SetField(ref _compressionThreshold, value); }

        private float _compressionRatio = 4.0f;
        public float CompressionRatio { get => _compressionRatio; set => SetField(ref _compressionRatio, value); }

        private float _compressionAttack = 0.003f;
        public float CompressionAttack { get => _compressionAttack; set => SetField(ref _compressionAttack, value); }

        private float _compressionRelease = 0.1f;
        public float CompressionRelease { get => _compressionRelease; set => SetField(ref _compressionRelease, value); }

        private bool _enableReverb;
        public bool EnableReverb { get => _enableReverb; set => SetField(ref _enableReverb, value); }

        private float _reverbDelay = 30.0f;
        public float ReverbDelay { get => _reverbDelay; set => SetField(ref _reverbDelay, value); }

        private float _reverbDecay = 0.3f;
        public float ReverbDecay { get => _reverbDecay; set => SetField(ref _reverbDecay, value); }

        private float _reverbStrength = 0.2f;
        public float ReverbStrength { get => _reverbStrength; set => SetField(ref _reverbStrength, value); }

        private bool _enableChorus;
        public bool EnableChorus { get => _enableChorus; set => SetField(ref _enableChorus, value); }

        private float _chorusDelay = 0.02f;
        public float ChorusDelay { get => _chorusDelay; set => SetField(ref _chorusDelay, value); }

        private float _chorusDepth = 0.005f;
        public float ChorusDepth { get => _chorusDepth; set => SetField(ref _chorusDepth, value); }

        private float _chorusRate = 1.5f;
        public float ChorusRate { get => _chorusRate; set => SetField(ref _chorusRate, value); }

        private float _chorusStrength = 0.3f;
        public float ChorusStrength { get => _chorusStrength; set => SetField(ref _chorusStrength, value); }

        private bool _enableEqualizer;
        public bool EnableEqualizer { get => _enableEqualizer; set => SetField(ref _enableEqualizer, value); }

        private EqualizerSettings _eq = new();
        public EqualizerSettings EQ
        {
            get => _eq;
            set
            {
                if (_eq != null) _eq.PropertyChanged -= OnNestedPropertyChanged;
                SetField(ref _eq!, value ?? new EqualizerSettings());
                if (_eq != null) _eq.PropertyChanged += OnNestedPropertyChanged;
            }
        }

        private bool _enableConvolutionReverb;
        public bool EnableConvolutionReverb { get => _enableConvolutionReverb; set => SetField(ref _enableConvolutionReverb, value); }

        private bool _enablePhaser;
        public bool EnablePhaser { get => _enablePhaser; set => SetField(ref _enablePhaser, value); }

        private float _phaserRate = 0.5f;
        public float PhaserRate { get => _phaserRate; set => SetField(ref _phaserRate, value); }

        private int _phaserStages = 4;
        public int PhaserStages { get => _phaserStages; set => SetField(ref _phaserStages, value); }

        private float _phaserFeedback = 0.5f;
        public float PhaserFeedback { get => _phaserFeedback; set => SetField(ref _phaserFeedback, value); }

        private bool _enableFlanger;
        public bool EnableFlanger { get => _enableFlanger; set => SetField(ref _enableFlanger, value); }

        private float _flangerDelay = 0.005f;
        public float FlangerDelay { get => _flangerDelay; set => SetField(ref _flangerDelay, value); }

        private float _flangerRate = 0.1f;
        public float FlangerRate { get => _flangerRate; set => SetField(ref _flangerRate, value); }

        private float _flangerDepth = 0.7f;
        public float FlangerDepth { get => _flangerDepth; set => SetField(ref _flangerDepth, value); }

        private bool _enableLimiter;
        public bool EnableLimiter { get => _enableLimiter; set => SetField(ref _enableLimiter, value); }

        private float _limiterThreshold = 0.95f;
        public float LimiterThreshold { get => _limiterThreshold; set => SetField(ref _limiterThreshold, value); }

        private bool _enableDCOffsetRemoval = true;
        public bool EnableDCOffsetRemoval { get => _enableDCOffsetRemoval; set => SetField(ref _enableDCOffsetRemoval, value); }

        public EffectsSettings()
        {
            if (_eq != null) _eq.PropertyChanged += OnNestedPropertyChanged;
        }

        private void OnNestedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EQ)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class EqualizerSettings : INotifyPropertyChanged
    {
        private float _bassGain = 1.0f;
        public float BassGain { get => _bassGain; set => SetField(ref _bassGain, value); }

        private float _midGain = 1.0f;
        public float MidGain { get => _midGain; set => SetField(ref _midGain, value); }

        private float _trebleGain = 1.0f;
        public float TrebleGain { get => _trebleGain; set => SetField(ref _trebleGain, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class InstrumentPreset : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        public string Name { get => _name; set => SetField(ref _name, value); }
        private int _startProgram;
        public int StartProgram { get => _startProgram; set => SetField(ref _startProgram, value); }

        private int _endProgram;
        public int EndProgram { get => _endProgram; set => SetField(ref _endProgram, value); }

        private string _waveform = string.Empty;
        public string Waveform { get => _waveform; set => SetField(ref _waveform, value); }

        private double _attack;
        public double Attack { get => _attack; set => SetField(ref _attack, value); }

        private double _decay;
        public double Decay { get => _decay; set => SetField(ref _decay, value); }

        private double _sustain;
        public double Sustain { get => _sustain; set => SetField(ref _sustain, value); }

        private double _release;
        public double Release { get => _release; set => SetField(ref _release, value); }

        private float _volume;
        public float Volume { get => _volume; set => SetField(ref _volume, value); }

        private FilterSettings _filter = new();
        public FilterSettings Filter
        {
            get => _filter;
            set
            {
                if (_filter != null) _filter.PropertyChanged -= OnNestedPropertyChanged;
                SetField(ref _filter!, value ?? new FilterSettings());
                if (_filter != null) _filter.PropertyChanged += OnNestedPropertyChanged;
            }
        }

        public InstrumentPreset()
        {
            if (_filter != null) _filter.PropertyChanged += OnNestedPropertyChanged;
        }

        private void OnNestedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Filter)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class CustomInstrument : INotifyPropertyChanged
    {
        private int _program;
        public int Program { get => _program; set => SetField(ref _program, value); }

        private string _waveform = string.Empty;
        public string Waveform { get => _waveform; set => SetField(ref _waveform, value); }

        private double _attack;
        public double Attack { get => _attack; set => SetField(ref _attack, value); }

        private double _decay;
        public double Decay { get => _decay; set => SetField(ref _decay, value); }

        private double _sustain;
        public double Sustain { get => _sustain; set => SetField(ref _sustain, value); }

        private double _release;
        public double Release { get => _release; set => SetField(ref _release, value); }

        private float _volume;
        public float Volume { get => _volume; set => SetField(ref _volume, value); }

        private FilterSettings _filter = new();
        public FilterSettings Filter
        {
            get => _filter;
            set
            {
                if (_filter != null) _filter.PropertyChanged -= OnNestedPropertyChanged;
                SetField(ref _filter!, value ?? new FilterSettings());
                if (_filter != null) _filter.PropertyChanged += OnNestedPropertyChanged;
            }
        }

        public CustomInstrument()
        {
            if (_filter != null) _filter.PropertyChanged += OnNestedPropertyChanged;
        }

        private void OnNestedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Filter)));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class FilterSettings : INotifyPropertyChanged
    {
        private string _type = string.Empty;
        public string Type { get => _type; set => SetField(ref _type, value); }

        private double _cutoff;
        public double Cutoff { get => _cutoff; set => SetField(ref _cutoff, value); }

        private double _resonance;
        public double Resonance { get => _resonance; set => SetField(ref _resonance, value); }

        private double _modulation;
        public double Modulation { get => _modulation; set => SetField(ref _modulation, value); }

        private double _modulationRate;
        public double ModulationRate { get => _modulationRate; set => SetField(ref _modulationRate, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class DebugSettings : INotifyPropertyChanged
    {
        private bool _enableLogging;
        public bool EnableLogging { get => _enableLogging; set => SetField(ref _enableLogging, value); }

        private bool _verboseLogging;
        public bool VerboseLogging { get => _verboseLogging; set => SetField(ref _verboseLogging, value); }

        private string _logFilePath = "midi_debug.log";
        public string LogFilePath { get => _logFilePath; set => SetField(ref _logFilePath, value); }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
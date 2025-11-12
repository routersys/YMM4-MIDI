using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.Collections.Generic;
using YukkuriMovieMaker.Plugin;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using System.Linq;
using MIDI.Configuration.Models;
using MIDI.Utils;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MIDI.UI.ViewModels.MidiEditor.Settings;

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
        private static string PluginDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        private static string OldConfigPath => Path.Combine(PluginDir, ConfigFileName);

        private static string ConfigDir
        {
            get
            {
                return Path.Combine(PluginDir, "Config");
            }
        }
        private static string ConfigPath => Path.Combine(ConfigDir, ConfigFileName);

        private bool _isApplyingPreset = false;

        [JsonIgnore]
        private static readonly Timer _saveTimer = new Timer(_ => _ = SaveAsync());


        private bool _isFirstLaunch = true;
        [DisplayName("初回起動")]
        [Description("プラグインが初めてロードされたかどうかを示します。ウィザード表示に使用されます。")]
        public bool IsFirstLaunch { get => _isFirstLaunch; set => Set(ref _isFirstLaunch, value); }


        private AudioSettings _audio = new();
        [DisplayName("音声設定")]
        public AudioSettings Audio { get => _audio; set => Set(ref _audio, value); }


        private PerformanceSettings _performance = new();
        [DisplayName("パフォーマンス設定")]
        public PerformanceSettings Performance { get => _performance; set => Set(ref _performance, value); }


        private MidiSettings _midi = new();
        [DisplayName("MIDI設定")]
        public MidiSettings MIDI { get => _midi; set => Set(ref _midi, value); }


        private SoundFontSettings _soundFont = new();
        [DisplayName("SoundFont設定")]
        public SoundFontSettings SoundFont { get => _soundFont; set => Set(ref _soundFont, value); }


        private SfzSettings _sfz = new();
        [DisplayName("SFZ設定")]
        public SfzSettings SFZ { get => _sfz; set => Set(ref _sfz, value); }


        private SynthesisSettings _synthesis = new();
        [DisplayName("シンセシス設定")]
        public SynthesisSettings Synthesis { get => _synthesis; set => Set(ref _synthesis, value); }


        private EffectsSettings _effects = new();
        [DisplayName("エフェクト設定")]
        public EffectsSettings Effects { get => _effects; set => Set(ref _effects, value); }


        private ObservableCollection<InstrumentPreset> _instrumentPresets = CreateDefaultInstrumentPresets();
        [DisplayName("インストゥルメントプリセット")]
        public ObservableCollection<InstrumentPreset> InstrumentPresets { get => _instrumentPresets; set => Set(ref _instrumentPresets, value); }


        private ObservableCollection<CustomInstrument> _customInstruments = new();
        [DisplayName("カスタムインストゥルメント")]
        public ObservableCollection<CustomInstrument> CustomInstruments { get => _customInstruments; set => Set(ref _customInstruments, value); }


        private DebugSettings _debug = new();
        [DisplayName("デバッグ設定")]
        public DebugSettings Debug { get => _debug; set => Set(ref _debug, value); }


        public override void Initialize()
        {
            Load();
            MidiEditorSettings.Default.Initialize();
            AttachEventHandlers();
        }


        public void BeginApplyPreset()
        {
            _isApplyingPreset = true;
            Logger.Info(LogMessages.PresetApplyStart);
        }


        public void EndApplyPreset()
        {
            _isApplyingPreset = false;
            OnPropertyChanged(nameof(IsFirstLaunch));
            OnPropertyChanged(nameof(Audio));
            OnPropertyChanged(nameof(Performance));
            OnPropertyChanged(nameof(MIDI));
            OnPropertyChanged(nameof(SoundFont));
            OnPropertyChanged(nameof(SFZ));
            OnPropertyChanged(nameof(Synthesis));
            OnPropertyChanged(nameof(Effects));
            OnPropertyChanged(nameof(InstrumentPresets));
            OnPropertyChanged(nameof(CustomInstruments));
            OnPropertyChanged(nameof(Debug));
            Logger.Info(LogMessages.PresetApplyEnd);
            Save();
        }

        private void AttachEventHandlers()
        {
            Audio.PropertyChanged += OnNestedPropertyChanged;
            Performance.PropertyChanged += OnNestedPropertyChanged;
            MIDI.PropertyChanged += OnNestedPropertyChanged;
            SoundFont.PropertyChanged += OnNestedPropertyChanged;
            SFZ.PropertyChanged += OnNestedPropertyChanged;
            Synthesis.PropertyChanged += OnNestedPropertyChanged;
            Effects.PropertyChanged += OnNestedPropertyChanged;
            Debug.PropertyChanged += OnNestedPropertyChanged;
        }

        private void OnNestedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isApplyingPreset) return;
            Logger.Info(LogMessages.SettingChanged, sender?.GetType().Name ?? "N/A", e.PropertyName ?? "N/A");
            Save();
            MidiAudioSource.ClearCache();
        }

        private void MigrateSettingsFile()
        {
            if (File.Exists(OldConfigPath))
            {
                try
                {
                    if (!Directory.Exists(ConfigDir))
                    {
                        Directory.CreateDirectory(ConfigDir);
                    }

                    if (!File.Exists(ConfigPath))
                    {
                        File.Move(OldConfigPath, ConfigPath);
                        Logger.Info($"Moved configuration file from '{OldConfigPath}' to '{ConfigPath}'.");
                    }
                    else
                    {
                        File.Delete(OldConfigPath);
                        Logger.Info($"Deleted old configuration file at '{OldConfigPath}' as new one already exists.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to migrate configuration file from '{OldConfigPath}' to '{ConfigPath}'.", ex);
                }
            }
        }

        public void Load()
        {
            MigrateSettingsFile();

            bool firstLaunchValue = true;
            if (!File.Exists(ConfigPath))
            {
                Logger.Warn(LogMessages.ConfigFileNotFound);
                var defaultConfig = new MidiConfiguration();
                defaultConfig.SaveSynchronously();
                CopyFrom(defaultConfig);
                firstLaunchValue = true;
            }
            else
            {
                try
                {
                    Logger.Info(LogMessages.LoadConfigFile, ConfigPath);
                    var jsonString = File.ReadAllText(ConfigPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        AllowTrailingCommas = true,
                        PropertyNameCaseInsensitive = true
                    };

                    jsonString = UpgradeSettings(jsonString, options);

                    var rootNode = JsonNode.Parse(jsonString)?.AsObject();
                    firstLaunchValue = rootNode?["isFirstLaunch"]?.GetValue<bool>() ?? true;

                    if (rootNode?.ContainsKey("editor") ?? false)
                    {
                        var editorNode = rootNode["editor"];
                        if (editorNode != null)
                        {
                            MidiEditorSettings.SaveEditorSettingsSeparately(editorNode.ToJsonString(options));
                            rootNode.Remove("editor");
                            jsonString = rootNode.ToJsonString(options);
                            File.WriteAllText(ConfigPath, jsonString);
                        }
                    }


                    var loadedConfig = JsonSerializer.Deserialize<MidiConfiguration>(jsonString, options);
                    if (loadedConfig != null)
                    {
                        CopyFrom(loadedConfig);
                    }
                    else
                    {
                        throw new JsonException("設定のデシリアライズに失敗しました。");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.ConfigReadError, ex, ex.Message);
                    BackupAndLoadDefault();
                    firstLaunchValue = true;
                }
            }


            IsFirstLaunch = firstLaunchValue;
        }


        private string UpgradeSettings(string jsonString, JsonSerializerOptions options)
        {
            try
            {
                var root = JsonNode.Parse(jsonString);
                if (root == null) return jsonString;

                if (root["instrumentPresets"] is JsonObject instrumentPresetsObject)
                {
                    Logger.Info(LogMessages.UpgradeOldPresetFormat);
                    var presetsArray = new JsonArray();
                    foreach (var prop in instrumentPresetsObject)
                    {
                        if (prop.Value is JsonObject presetObj)
                        {
                            presetObj["name"] = prop.Key;
                            presetsArray.Add(presetObj.DeepClone());
                        }
                    }
                    root["instrumentPresets"] = presetsArray;
                }

                if (root["debug"] is JsonObject debugObject && debugObject.ContainsKey("midiInputMode"))
                {
                    var midiInputMode = debugObject["midiInputMode"];
                    if (midiInputMode != null && root["editor"] is JsonObject editorNode)
                    {
                        var inputNode = editorNode["input"] as JsonObject ?? new JsonObject();
                        inputNode["midiInputMode"] = midiInputMode.DeepClone();
                        editorNode["input"] = inputNode;
                        debugObject.Remove("midiInputMode");
                    }
                }

                return root.ToJsonString(options);
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.UpgradeSettingsError, ex);
            }
            return jsonString;
        }


        private void BackupAndLoadDefault()
        {
            try
            {
                string backupDir = Path.Combine(PluginDir, "backup");
                if (!Directory.Exists(backupDir))
                {
                    Directory.CreateDirectory(backupDir);
                }
                if (File.Exists(ConfigPath))
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH.mm.ss");
                    var backupFileName = $"{Path.GetFileNameWithoutExtension(ConfigFileName)}-{timestamp}.bak";
                    var backupPath = Path.Combine(backupDir, backupFileName);

                    File.Move(ConfigPath, backupPath);
                    Logger.Warn(LogMessages.ConfigBackupSuccess, backupPath);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.ConfigBackupFailed, ex, ex.Message);
            }

            Logger.Warn(LogMessages.DefaultConfigLoad);
            var defaultConfig = new MidiConfiguration();
            CopyFrom(defaultConfig);
            SaveSynchronously();
        }

        public void CopyFrom(MidiConfiguration source)
        {

            Audio.CopyFrom(source.Audio);
            Performance.CopyFrom(source.Performance);
            MIDI.CopyFrom(source.MIDI);
            SoundFont.CopyFrom(source.SoundFont);
            SFZ.CopyFrom(source.SFZ);
            Synthesis.CopyFrom(source.Synthesis);
            Effects.CopyFrom(source.Effects);
            InstrumentPresets = new ObservableCollection<InstrumentPreset>(source.InstrumentPresets.Select(p => (InstrumentPreset)p.Clone()));
            CustomInstruments = new ObservableCollection<CustomInstrument>(source.CustomInstruments.Select(c => (CustomInstrument)c.Clone()));
            Debug.CopyFrom(source.Debug);

            if (!_isFirstLaunch && source.IsFirstLaunch)
            {

            }
            else
            {
                IsFirstLaunch = source.IsFirstLaunch;
            }

            Logger.Info(LogMessages.CopyMidiConfig);
        }

        public MidiConfiguration Clone()
        {
            var clone = new MidiConfiguration();
            clone.CopyFrom(this);
            Logger.Info(LogMessages.CloneMidiConfig);
            return clone;
        }


        public new void Save()
        {
            _saveTimer.Change(500, Timeout.Infinite);
        }

        private static async Task SaveAsync()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var jsonString = JsonSerializer.Serialize(Default, options);
                await File.WriteAllTextAsync(ConfigPath, jsonString);
                Logger.Info(LogMessages.SaveConfigFile, ConfigPath);
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.ConfigSaveError, ex, ex.Message);
            }
        }

        public void SaveSynchronously()
        {
            _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }
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
                Logger.Error(LogMessages.ConfigSaveError, ex, ex.Message);
            }
        }

        public void Reload()
        {
            Logger.Info(LogMessages.ReloadConfigStart);
            Load();
            MidiEditorSettings.Default.Reload();
            MidiAudioSource.ClearCache();
            OnPropertyChanged(string.Empty);
            Logger.Info(LogMessages.ReloadConfigEnd);
        }


        private static ObservableCollection<InstrumentPreset> CreateDefaultInstrumentPresets()
        {
            return new ObservableCollection<InstrumentPreset>
            {
                new InstrumentPreset { Name = "Piano", StartProgram = 0, EndProgram = 7, Waveform = "Sine", Attack = 0.01, Decay = 0.3, Sustain = 0.7, Release = 0.5, Volume = 1.0f, Filter = new FilterSettings { Type = "None", Cutoff = 22050, Resonance = 1.0, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "ChromaticPercussion", StartProgram = 8, EndProgram = 15, Waveform = "Triangle", Attack = 0.05, Decay = 0.2, Sustain = 0.6, Release = 0.8, Volume = 0.8f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 8000, Resonance = 1.2, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "Organ", StartProgram = 16, EndProgram = 23, Waveform = "Organ", Attack = 0.001, Decay = 0.1, Sustain = 0.9, Release = 0.2, Volume = 0.9f, Filter = new FilterSettings { Type = "None", Cutoff = 22050, Resonance = 1.0, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "Guitar", StartProgram = 24, EndProgram = 31, Waveform = "Sawtooth", Attack = 0.02, Decay = 0.15, Sustain = 0.5, Release = 0.6, Volume = 0.7f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 5000, Resonance = 1.5, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "Bass", StartProgram = 32, EndProgram = 39, Waveform = "Square", Attack = 0.01, Decay = 0.1, Sustain = 0.8, Release = 0.3, Volume = 0.6f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 2000, Resonance = 1.3, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "Strings", StartProgram = 40, EndProgram = 47, Waveform = "Sawtooth", Attack = 0.3, Decay = 0.2, Sustain = 0.8, Release = 1.0, Volume = 0.8f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 12000, Resonance = 1.1, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "Ensemble", StartProgram = 48, EndProgram = 55, Waveform = "Triangle", Attack = 0.2, Decay = 0.3, Sustain = 0.7, Release = 0.8, Volume = 0.7f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 8000, Resonance = 1.0, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "Brass", StartProgram = 56, EndProgram = 63, Waveform = "Square", Attack = 0.05, Decay = 0.1, Sustain = 0.9, Release = 0.2, Volume = 0.9f, Filter = new FilterSettings { Type = "BandPass", Cutoff = 4000, Resonance = 1.4, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "Reed", StartProgram = 64, EndProgram = 71, Waveform = "Sawtooth", Attack = 0.02, Decay = 0.15, Sustain = 0.8, Release = 0.4, Volume = 0.8f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 6000, Resonance = 1.3, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "Pipe", StartProgram = 72, EndProgram = 79, Waveform = "Sine", Attack = 0.1, Decay = 0.2, Sustain = 0.9, Release = 0.3, Volume = 0.7f, Filter = new FilterSettings { Type = "HighPass", Cutoff = 1000, Resonance = 1.0, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "SynthLead", StartProgram = 80, EndProgram = 87, Waveform = "Square", Attack = 0.001, Decay = 0.05, Sustain = 0.6, Release = 0.2, Volume = 0.8f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 8000, Resonance = 1.8, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "SynthPad", StartProgram = 88, EndProgram = 95, Waveform = "Triangle", Attack = 0.5, Decay = 0.3, Sustain = 0.8, Release = 1.5, Volume = 0.6f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 10000, Resonance = 1.1, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "SynthEffects", StartProgram = 96, EndProgram = 103, Waveform = "Noise", Attack = 0.1, Decay = 0.2, Sustain = 0.5, Release = 0.8, Volume = 0.5f, Filter = new FilterSettings { Type = "BandPass", Cutoff = 2000, Resonance = 2.0, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "Ethnic", StartProgram = 104, EndProgram = 111, Waveform = "Sawtooth", Attack = 0.05, Decay = 0.3, Sustain = 0.6, Release = 0.7, Volume = 0.8f, Filter = new FilterSettings { Type = "LowPass", Cutoff = 7000, Resonance = 1.2, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "Percussive", StartProgram = 112, EndProgram = 119, Waveform = "Noise", Attack = 0.001, Decay = 0.1, Sustain = 0.3, Release = 0.2, Volume = 1.0f, Filter = new FilterSettings { Type = "BandPass", Cutoff = 3000, Resonance = 1.5, Lfo = new LfoSettings() } },
                new InstrumentPreset { Name = "SoundEffects", StartProgram = 120, EndProgram = 127, Waveform = "Noise", Attack = 0.01, Decay = 0.5, Sustain = 0.2, Release = 1.0, Volume = 0.7f, Filter = new FilterSettings { Type = "HighPass", Cutoff = 500, Resonance = 1.0, Lfo = new LfoSettings() } }
            };
        }

        public void ApplyPreset(JsonObject presetNode)
        {
            BeginApplyPreset();
            try
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    AllowTrailingCommas = true,
                    PropertyNameCaseInsensitive = true
                };
                var currentJson = JsonSerializer.Serialize(this, options);
                var currentNode = JsonNode.Parse(currentJson)?.AsObject();

                if (currentNode == null)
                {
                    Logger.Error(LogMessages.PresetJsonMergeFailed, null);
                    return;
                }

                Logger.Info(LogMessages.PresetApplyProperties, presetNode.Count);
                foreach (var property in presetNode)
                {
                    if (property.Key.Equals("editor", StringComparison.OrdinalIgnoreCase)) continue;
                    if (property.Key.Equals("isFirstLaunch", StringComparison.OrdinalIgnoreCase)) continue;
                    currentNode[property.Key] = property.Value!.DeepClone();
                }


                var mergedConfig = JsonSerializer.Deserialize<MidiConfiguration>(currentNode.ToJsonString(), options);
                if (mergedConfig != null)
                {
                    var originalFirstLaunch = IsFirstLaunch;
                    CopyFrom(mergedConfig);
                    IsFirstLaunch = originalFirstLaunch;
                    Logger.Info(LogMessages.PresetMergeSuccess);
                }
                else
                {
                    Logger.Error(LogMessages.PresetMergeDeserializationFailed, null);
                }
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.PresetApplyError, ex);
            }
            finally
            {
                EndApplyPreset();
            }
        }

        public int CountChanges(MidiConfiguration other)
        {
            return GetChangedProperties(other).Count;
        }

        public List<string> GetChangedProperties(MidiConfiguration oldConfig)
        {
            var changes = new List<string>();
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = false };

            var newJson = JsonNode.Parse(JsonSerializer.Serialize(this, options)) as JsonObject;
            var oldJson = JsonNode.Parse(JsonSerializer.Serialize(oldConfig, options)) as JsonObject;

            if (newJson == null || oldJson == null)
            {
                Logger.Error(LogMessages.CompareSettingsJsonFailed, null);
                return changes;
            }

            CompareJsonNodes(newJson, oldJson, "", changes);

            return changes;
        }

        private void CompareJsonNodes(JsonNode? newNode, JsonNode? oldNode, string path, List<string> changes)
        {
            if (newNode is JsonObject newObj && oldNode is JsonObject oldObj)
            {
                var allKeys = newObj.Select(kv => kv.Key).Union(oldObj.Select(kv => kv.Key)).Distinct();
                foreach (var key in allKeys)
                {
                    if (key.Equals("editor", StringComparison.OrdinalIgnoreCase)) continue;
                    if (key.Equals("isFirstLaunch", StringComparison.OrdinalIgnoreCase)) continue;
                    var newPath = string.IsNullOrEmpty(path) ? key : $"{path}.{key}";
                    newObj.TryGetPropertyValue(key, out var newValue);
                    oldObj.TryGetPropertyValue(key, out var oldValue);
                    CompareJsonNodes(newValue, oldValue, newPath, changes);
                }
            }
            else if (newNode is JsonArray newArr && oldNode is JsonArray oldArr)
            {
                if (newArr.ToJsonString() != oldArr.ToJsonString())
                {
                    changes.Add($"{path}: [コレクションが変更されました]");
                }
            }
            else
            {
                var newValueString = newNode?.ToString() ?? "null";
                var oldValueString = oldNode?.ToString() ?? "null";
                if (newValueString != oldValueString)
                {
                    changes.Add($"{path}: {oldValueString} -> {newValueString}");
                }
            }
        }

        public string GetConfigurationHash()
        {
            var options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            var jsonNode = JsonSerializer.SerializeToNode(this, options);
            if (jsonNode is JsonObject jsonObject)
            {
                jsonObject.Remove("editor");
                jsonObject.Remove("isFirstLaunch");
            }

            var json = jsonNode?.ToJsonString() ?? "";

            using (var sha256 = SHA256.Create())
            {
                var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(json));
                var hashString = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                Logger.Info(LogMessages.ConfigHashGenerated, hashString);
                return hashString;
            }
        }
    }
}
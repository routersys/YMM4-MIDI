using YukkuriMovieMaker.Plugin.Voice;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MIDI.Voice.ViewModels;
using MIDI.Voice.Views;
using YukkuriMovieMaker.Plugin;
using MIDI.Configuration.Models;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.IO;
using System.Reflection;
using MIDI.Utils;
using System.Text.Encodings.Web;
using System.Runtime.CompilerServices;
using MIDI.Voice.Models;
using System;
using System.Threading;
using System.ComponentModel;

namespace MIDI.Voice
{
    public class NoteVoicePlugin : IVoicePlugin
    {
        private List<IVoiceSpeaker> _speakers = new List<IVoiceSpeaker>();
        private readonly object _lock = new object();

        public string Name => Translate.NoteVoicePluginName;

        public IEnumerable<IVoiceSpeaker> Voices
        {
            get
            {
                lock (_lock)
                {
                    if (!_speakers.Any())
                    {
                        LoadSpeakers();
                    }
                    return _speakers;
                }
            }
        }


        public bool CanUpdateVoices => true;

        public bool IsVoicesCached => true;

        public Task UpdateVoicesAsync()
        {
            lock (_lock)
            {
                LoadSpeakers();
            }
            return Task.CompletedTask;
        }

        private void LoadSpeakers()
        {
            _speakers.Clear();
            NoteVoiceSettings.Default.Initialize();
            foreach (var model in NoteVoiceSettings.Default.SynthSettings.VoiceModels)
            {
                if (model != null)
                {
                    _speakers.Add(new NoteVoiceSpeaker(model));
                }
            }
        }

    }

    public class NoteVoiceSettings : SettingsBase<NoteVoiceSettings>
    {
        public override SettingsCategory Category => SettingsCategory.Voice;
        public override string Name => Translate.NoteVoiceSettingsName;
        public override bool HasSettingView => true;

        private VoiceSynthSettings _synthSettings = new VoiceSynthSettings();
        public VoiceSynthSettings SynthSettings { get => _synthSettings; set => Set(ref _synthSettings, value); }


        [System.Text.Json.Serialization.JsonIgnore]
        public override object? SettingView => new NoteVoiceSettingsView { DataContext = new NoteVoiceSettingsViewModel(this) };

        private bool _isInitialized = false;
        private readonly object _initLock = new object();
        private static readonly string ConfigFileName = "NoteVoiceSettings.json";
        private static readonly List<int> ValidSampleRates = new List<int> { 22050, 44100, 48000, 96000 };
        private const int DefaultSampleRate = 44100;
        private const string PluginVoiceModelDir = "VoiceModel";

        private static string PluginDir
        {
            get
            {
                return Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            }
        }

        private static string OldConfigPath => Path.Combine(PluginDir, ConfigFileName);

        private static string ConfigDir
        {
            get
            {
                return Path.Combine(PluginDir, "Config");
            }
        }

        private static string? _cachedSettingsPath;
        private static readonly object _pathLock = new object();
        public static string GetSettingFilePath()
        {
            lock (_pathLock)
            {
                if (_cachedSettingsPath == null)
                {
                    try
                    {
                        if (!Directory.Exists(ConfigDir))
                        {
                            Directory.CreateDirectory(ConfigDir);
                        }
                        _cachedSettingsPath = Path.Combine(ConfigDir, ConfigFileName);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(LogMessages.SettingsPathError, ex);
                        _cachedSettingsPath = Path.Combine(ConfigDir, ConfigFileName);
                    }
                }
                return _cachedSettingsPath;
            }
        }

        public override void Initialize()
        {
            lock (_initLock)
            {
                if (_isInitialized) return;
                MigrateSettingsFile();
                Load();
                AttachEventHandlers();
                _isInitialized = true;
                Logger.Info(LogMessages.NoteVoiceSettingsInitialized);
            }
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

                    var newConfigPath = GetSettingFilePath();

                    if (!File.Exists(newConfigPath))
                    {
                        File.Move(OldConfigPath, newConfigPath);
                        Logger.Info($"Moved note voice settings file from '{OldConfigPath}' to '{newConfigPath}'.");
                    }
                    else
                    {
                        File.Delete(OldConfigPath);
                        Logger.Info($"Deleted old note voice settings file at '{OldConfigPath}' as new one already exists.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to migrate note voice settings file from '{OldConfigPath}' to '{GetSettingFilePath()}'.", ex);
                }
            }
        }

        private void AttachEventHandlers()
        {
            DetachEventHandlers();
            SynthSettings.PropertyChanged += OnSettingsChanged;
            if (SynthSettings.VoiceModels != null)
            {
                foreach (var model in SynthSettings.VoiceModels)
                {
                    if (model == null) continue;
                    model.PropertyChanged += OnModelChanged;
                    model.Layers.CollectionChanged += OnLayersChanged;
                    if (model.InternalSynthSettings != null)
                        model.InternalSynthSettings.PropertyChanged += OnSettingsChanged;
                }
                SynthSettings.VoiceModels.CollectionChanged += OnVoiceModelsChanged;
            }
            if (SynthSettings.SoundFontLayers != null)
            {
                SynthSettings.SoundFontLayers.CollectionChanged += OnLayersChanged;
            }
        }

        private void DetachEventHandlers()
        {
            SynthSettings.PropertyChanged -= OnSettingsChanged;
            if (SynthSettings.VoiceModels != null)
            {
                SynthSettings.VoiceModels.CollectionChanged -= OnVoiceModelsChanged;
                foreach (var model in SynthSettings.VoiceModels)
                {
                    if (model == null) continue;
                    model.PropertyChanged -= OnModelChanged;
                    model.Layers.CollectionChanged -= OnLayersChanged;
                    if (model.InternalSynthSettings != null)
                        model.InternalSynthSettings.PropertyChanged -= OnSettingsChanged;
                }
            }
            if (SynthSettings.SoundFontLayers != null)
            {
                SynthSettings.SoundFontLayers.CollectionChanged -= OnLayersChanged;
            }
        }

        private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            Save();
        }

        private void OnLayersChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            Save();
        }

        private void OnModelChanged(object? sender, PropertyChangedEventArgs e)
        {
            Save();
        }


        private void OnVoiceModelsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (VoiceModel newItem in e.NewItems.OfType<VoiceModel>())
                {
                    if (newItem == null) continue;
                    newItem.PropertyChanged += OnModelChanged;
                    newItem.Layers.CollectionChanged += OnLayersChanged;
                    if (newItem.InternalSynthSettings != null)
                        newItem.InternalSynthSettings.PropertyChanged += OnSettingsChanged;
                }
            }
            if (e.OldItems != null)
            {
                foreach (VoiceModel oldItem in e.OldItems.OfType<VoiceModel>())
                {
                    if (oldItem == null) continue;
                    oldItem.PropertyChanged -= OnModelChanged;
                    oldItem.Layers.CollectionChanged -= OnLayersChanged;
                    if (oldItem.InternalSynthSettings != null)
                        oldItem.InternalSynthSettings.PropertyChanged -= OnSettingsChanged;
                }
            }
            Save();
        }

        public void Load()
        {
            var path = GetSettingFilePath();
            if (System.IO.File.Exists(path))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(path);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        WriteIndented = true,
                        AllowTrailingCommas = true,
                        ReadCommentHandling = JsonCommentHandling.Skip
                    };
                    var loaded = JsonSerializer.Deserialize<NoteVoiceSettings>(json, options);
                    if (loaded != null && loaded.SynthSettings != null)
                    {
                        SynthSettings.CopyFrom(loaded.SynthSettings);
                        Logger.Info(LogMessages.NoteVoiceSettingsLoaded, path);
                    }
                    else
                    {
                        Logger.Warn(LogMessages.NoteVoiceSettingsDeserializationFailed, path);
                        SynthSettings = CreateDefaultSynthSettings();
                    }
                }
                catch (JsonException ex)
                {
                    Logger.Error(LogMessages.NoteVoiceSettingsLoadJsonError, ex, path);
                    SynthSettings = CreateDefaultSynthSettings();
                }
                catch (System.Exception ex)
                {
                    Logger.Error(LogMessages.NoteVoiceSettingsLoadError, ex, path);
                    SynthSettings = CreateDefaultSynthSettings();
                }
            }
            else
            {
                Logger.Info(LogMessages.NoteVoiceSettingsNotFound, path);
                SynthSettings = CreateDefaultSynthSettings();
            }

            ValidateAndEnsureDefaults();
        }

        private void ValidateAndEnsureDefaults()
        {
            if (SynthSettings == null) SynthSettings = new VoiceSynthSettings();
            if (SynthSettings.VoiceModels == null) SynthSettings.VoiceModels = new ObservableCollection<VoiceModel>();
            if (SynthSettings.SoundFontLayers == null) SynthSettings.SoundFontLayers = new ObservableCollection<SoundFontLayer>();
            if (SynthSettings.UtauVoiceBaseFolders == null) SynthSettings.UtauVoiceBaseFolders = new ObservableCollection<string>();

            var validModels = SynthSettings.VoiceModels.Where(m => m != null).ToList();
            if (validModels.Count < SynthSettings.VoiceModels.Count)
            {
                Logger.Warn("Null entries found in VoiceModels. Cleaning up.");
                SynthSettings.VoiceModels = new ObservableCollection<VoiceModel>(validModels);
            }

            if (SynthSettings.VoiceModels.Count == 0)
            {
                var defaultInternalModel = new VoiceModel { Name = Translate.DefaultSynthModelName, ModelType = ModelType.InternalSynth };
                defaultInternalModel.InternalSynthSettings = new InternalSynthModel();
                SynthSettings.VoiceModels.Add(defaultInternalModel);

                var defaultSfModel = new VoiceModel { Name = Translate.DefaultSoundFontModelName, ModelType = ModelType.SoundFont };
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                var defaultSfPath = Path.Combine(assemblyLocation, "GeneralUser-GS.sf2");
                if (File.Exists(defaultSfPath))
                {
                    defaultSfModel.Layers.Add(new SoundFontLayer { SoundFontFile = "GeneralUser-GS.sf2" });
                    Logger.Info(LogMessages.DefaultSoundFontAddedToModel, defaultSfModel.Name);
                }
                else
                {
                    Logger.Warn(LogMessages.DefaultSoundFontNotFoundForModel, defaultSfModel.Name, defaultSfPath);
                }
                SynthSettings.VoiceModels.Add(defaultSfModel);

                SynthSettings.CurrentModelName = Translate.DefaultSynthModelName;
            }

            if (string.IsNullOrEmpty(SynthSettings.CurrentModelName) || !SynthSettings.VoiceModels.Any(m => m.Name == SynthSettings.CurrentModelName))
            {
                SynthSettings.CurrentModelName = SynthSettings.VoiceModels.FirstOrDefault()?.Name ?? Translate.DefaultSynthModelName;
                Logger.Warn(LogMessages.CurrentSoundFontModelInvalid, SynthSettings.CurrentModelName);
            }

            if (!ValidSampleRates.Contains(SynthSettings.SampleRate))
            {
                Logger.Warn(LogMessages.InvalidSampleRateInSettings, SynthSettings.SampleRate);
                SynthSettings.SampleRate = DefaultSampleRate;
            }

            foreach (var model in SynthSettings.VoiceModels)
            {
                if (model.ModelType == ModelType.InternalSynth && model.InternalSynthSettings == null)
                {
                    model.InternalSynthSettings = new InternalSynthModel();
                }
            }

            EnsurePluginVoiceModelDirectory();
        }

        private void EnsurePluginVoiceModelDirectory()
        {
            try
            {
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (string.IsNullOrEmpty(assemblyLocation)) return;

                var voiceModelDir = Path.Combine(assemblyLocation, PluginVoiceModelDir);

                if (!Directory.Exists(voiceModelDir))
                {
                    Directory.CreateDirectory(voiceModelDir);
                }

                if (!SynthSettings.UtauVoiceBaseFolders.Contains(voiceModelDir))
                {
                    SynthSettings.UtauVoiceBaseFolders.Add(voiceModelDir);
                    Save();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to ensure VoiceModel directory exists.", ex);
            }
        }


        private VoiceSynthSettings CreateDefaultSynthSettings()
        {
            var settings = new VoiceSynthSettings();
            settings.SampleRate = DefaultSampleRate;
            settings.CurrentModelName = Translate.DefaultSynthModelName;
            settings.VoiceModels = new ObservableCollection<VoiceModel>();
            settings.SoundFontLayers = new ObservableCollection<SoundFontLayer>();
            settings.UtauVoiceBaseFolders = new ObservableCollection<string>();

            try
            {
                var defaultInternalModel = new VoiceModel { Name = Translate.DefaultSynthModelName, ModelType = ModelType.InternalSynth };
                defaultInternalModel.InternalSynthSettings = new InternalSynthModel();
                settings.VoiceModels.Add(defaultInternalModel);

                var defaultSfModel = new VoiceModel { Name = Translate.DefaultSoundFontModelName, ModelType = ModelType.SoundFont };
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (!string.IsNullOrEmpty(assemblyLocation))
                {
                    var defaultSfPath = Path.Combine(assemblyLocation, "GeneralUser-GS.sf2");
                    if (File.Exists(defaultSfPath))
                    {
                        defaultSfModel.Layers.Add(new SoundFontLayer { SoundFontFile = "GeneralUser-GS.sf2" });
                        settings.SoundFontLayers = new ObservableCollection<SoundFontLayer>(defaultSfModel.Layers);
                        Logger.Info(LogMessages.DefaultSoundFontFoundAndSet, defaultSfPath);
                    }
                    else
                    {
                        Logger.Warn(LogMessages.DefaultSoundFontNotFoundOnInitialization, defaultSfPath);
                    }

                    var voiceModelDir = Path.Combine(assemblyLocation, PluginVoiceModelDir);
                    if (!settings.UtauVoiceBaseFolders.Contains(voiceModelDir))
                    {
                        settings.UtauVoiceBaseFolders.Add(voiceModelDir);
                    }
                }
                else
                {
                    Logger.Error(LogMessages.AssemblyLocationError, null, "during default settings creation");
                }
                settings.VoiceModels.Add(defaultSfModel);
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.DefaultSettingsCreationError, ex);
            }
            Logger.Info(LogMessages.DefaultNoteVoiceSettingsCreated);
            return settings;
        }

        private static SemaphoreSlim _saveSemaphore = new SemaphoreSlim(1, 1);
        private static Task? _saveTask;
        private static bool _savePending = false;

        public new void Save()
        {
            _savePending = true;
            if (_saveTask == null || _saveTask.IsCompleted)
            {
                _saveTask = Task.Run(async () => {
                    await Task.Delay(500);
                    if (_savePending)
                    {
                        await SaveInternalAsync();
                    }
                });
            }
        }

        private async Task SaveInternalAsync()
        {
            await _saveSemaphore.WaitAsync();
            try
            {
                if (!_savePending) return;
                _savePending = false;

                var path = GetSettingFilePath();
                try
                {
                    var options = new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
                    };
                    var json = JsonSerializer.Serialize(this, options);
                    await File.WriteAllTextAsync(path, json);
                    Logger.Info(LogMessages.NoteVoiceSettingsSaved, path);
                }
                catch (IOException ex)
                {
                    Logger.Error(LogMessages.NoteVoiceSettingsSaveIOError, ex, path);
                    await Task.Delay(100);
                    try
                    {
                        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                        await File.WriteAllTextAsync(path, json);
                        Logger.Info(LogMessages.NoteVoiceSettingsSavedRetry, path);
                    }
                    catch (Exception retryEx)
                    {
                        Logger.Error(LogMessages.NoteVoiceSettingsSaveRetryFailed, retryEx, path);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.NoteVoiceSettingsSaveError, ex, path);
                }
            }
            finally
            {
                _saveSemaphore.Release();
            }
        }

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName ?? string.Empty);
            Save();
            return true;
        }
    }
}
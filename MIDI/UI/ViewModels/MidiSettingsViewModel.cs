using Microsoft.Win32;
using MIDI.API;
using MIDI.Configuration.Models;
using MIDI.UI.Commands;
using MIDI.UI.ViewModels;
using MIDI.UI.ViewModels.Models;
using MIDI.UI.ViewModels.Services;
using MIDI.UI.Views;
using MIDI.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MIDI
{
    public class MidiSettingsViewModel : INotifyPropertyChanged, IDisposable
    {
        public MidiConfiguration Settings => MidiConfiguration.Default;
        public MidiEditorSettings EditorSettings => MidiEditorSettings.Default;

        private readonly IFileService _fileService;
        private readonly IPresetService _presetService;
        private readonly IDragDropService _dragDropService;
        private FileSystemWatcher? _logWatcher;
        private long _logFileSize = 0;
        private readonly StringBuilder _logContentBuilder = new StringBuilder();

        public ICommand ReloadConfigCommand { get; }
        public ICommand RefreshFilesCommand { get; }
        public ICommand OpenPluginDirectoryCommand { get; }
        public ICommand EditSfzMapCommand { get; }
        public ICommand EditSoundFontRuleCommand { get; }
        public ICommand AddSoundFontLayerCommand { get; }
        public ICommand RemoveSoundFontLayerCommand { get; }
        public ICommand MoveSoundFontLayerUpCommand { get; }
        public ICommand MoveSoundFontLayerDownCommand { get; }
        public ICommand AddInstrumentPresetCommand { get; }
        public ICommand RemoveInstrumentPresetCommand { get; }
        public ICommand AddCustomInstrumentCommand { get; }
        public ICommand RemoveCustomInstrumentCommand { get; }
        public ICommand SelectImpulseResponseFileCommand { get; }
        public ICommand SavePresetCommand { get; }
        public ICommand DeletePresetCommand { get; }
        public ICommand DropCommand { get; }
        public ICommand DragOverCommand { get; }
        public ICommand DragLeaveCommand { get; }
        public ICommand ClearLogCommand { get; }
        public ICommand ShowReleaseNotesCommand { get; }

        private List<GitHubRelease> _allReleases = new();

        private bool _isDraggingOver;
        public bool IsDraggingOver
        {
            get => _isDraggingOver;
            set => SetField(ref _isDraggingOver, value);
        }

        private string _dragDropMessage = "";
        public string DragDropMessage
        {
            get => _dragDropMessage;
            set => SetField(ref _dragDropMessage, value);
        }

        public ObservableCollection<SfzFileViewModel> SfzFiles { get; } = new();
        public ObservableCollection<SoundFontFileViewModel> SoundFontFiles { get; } = new();
        public ObservableCollection<SoundFontLayer> SoundFontLayers => Settings.SoundFont.Layers;
        public ObservableCollection<string> WavetableFiles { get; } = new();
        public ObservableCollection<PresetViewModel> Presets { get; } = new();

        private PresetViewModel? _selectedPreset;
        public PresetViewModel? SelectedPreset
        {
            get => _selectedPreset;
            set
            {
                if (SetField(ref _selectedPreset, value) && value != null)
                {
                    _presetService.LoadPresetAsync(value.Name);
                }
            }
        }

        private string _newPresetName = "新しいプリセット";
        public string NewPresetName
        {
            get => _newPresetName;
            set => SetField(ref _newPresetName, value);
        }

        private string _currentVersionText = Translate.Key_VersionChecking;
        public string CurrentVersionText
        {
            get => _currentVersionText;
            set => SetField(ref _currentVersionText, value);
        }

        private string _updateStatusText = string.Empty;
        public string UpdateStatusText
        {
            get => _updateStatusText;
            set => SetField(ref _updateStatusText, value);
        }

        private Visibility _updateStatusVisibility = Visibility.Collapsed;
        public Visibility UpdateStatusVisibility
        {
            get => _updateStatusVisibility;
            set => SetField(ref _updateStatusVisibility, value);
        }

        private string _logContent = "";
        public string LogContent
        {
            get => _logContent;
            set => SetField(ref _logContent, value);
        }

        public bool IsApiActive => Settings.Debug.EnableNamedPipeApi && NamedPipeServer.IsClientConnected;

        public MidiSettingsViewModel()
        {
            _fileService = new FileService(Settings);
            _presetService = new PresetService(Settings, Presets, new PresetManager());
            _dragDropService = new DragDropService(Settings, _fileService, _presetService);

            ReloadConfigCommand = new RelayCommand(_ =>
            {
                Settings.Reload();
                EditorSettings.Reload();
                _ = RefreshAllFilesAsync();
                OnPropertyChanged(nameof(SoundFontLayers));
            });

            RefreshFilesCommand = new RelayCommand(async _ => await RefreshAllFilesAsync());

            ClearLogCommand = new RelayCommand(_ => {
                _logContentBuilder.Clear();
                LogContent = "";
                var logPath = GetLogPath();
                if (File.Exists(logPath))
                {
                    try
                    {
                        File.WriteAllText(logPath, string.Empty);
                        _logFileSize = 0;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(LogMessages.FileAccessError, ex, logPath);
                    }
                }
            });

            OpenPluginDirectoryCommand = new RelayCommand(_ =>
            {
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (assemblyLocation != null && Directory.Exists(assemblyLocation))
                {
                    Process.Start("explorer.exe", assemblyLocation);
                }
            });

            EditSfzMapCommand = new RelayCommand(p =>
            {
                if (p is SfzFileViewModel sfzFile)
                {
                    ShowSfzProgramMapEditor(sfzFile);
                }
            });

            EditSoundFontRuleCommand = new RelayCommand(p =>
            {
                if (p is SoundFontFileViewModel sfFile)
                {
                    ShowSoundFontRuleEditor(sfFile);
                }
            });

            AddSoundFontLayerCommand = new RelayCommand(p => {
                if (p is SoundFontFileViewModel sfFile)
                {
                    if (!SoundFontLayers.Any(l => l.SoundFontFile == sfFile.FileName))
                    {
                        SoundFontLayers.Add(new SoundFontLayer { SoundFontFile = sfFile.FileName });
                    }
                }
            });

            RemoveSoundFontLayerCommand = new RelayCommand(p => {
                if (p is SoundFontLayer layer)
                {
                    SoundFontLayers.Remove(layer);
                }
            });

            MoveSoundFontLayerUpCommand = new RelayCommand(p => {
                if (p is SoundFontLayer layer)
                {
                    var index = SoundFontLayers.IndexOf(layer);
                    if (index > 0)
                    {
                        SoundFontLayers.Move(index, index - 1);
                    }
                }
            });

            MoveSoundFontLayerDownCommand = new RelayCommand(p => {
                if (p is SoundFontLayer layer)
                {
                    var index = SoundFontLayers.IndexOf(layer);
                    if (index < SoundFontLayers.Count - 1)
                    {
                        SoundFontLayers.Move(index, index + 1);
                    }
                }
            });

            SelectImpulseResponseFileCommand = new RelayCommand(_ =>
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "WAV Files (*.wav)|*.wav|All files (*.*)|*.*",
                    Title = "インパルス応答ファイルを選択"
                };
                if (openFileDialog.ShowDialog() == true)
                {
                    Settings.Effects.ImpulseResponseFilePath = openFileDialog.FileName;
                }
            });

            AddInstrumentPresetCommand = new RelayCommand(_ => Settings.InstrumentPresets.Add(new InstrumentPreset()));
            RemoveInstrumentPresetCommand = new RelayCommand(p => { if (p is InstrumentPreset preset) Settings.InstrumentPresets.Remove(preset); });
            AddCustomInstrumentCommand = new RelayCommand(_ => Settings.CustomInstruments.Add(new CustomInstrument()));
            RemoveCustomInstrumentCommand = new RelayCommand(p => { if (p is CustomInstrument instrument) Settings.CustomInstruments.Remove(instrument); });

            SavePresetCommand = new RelayCommand(async _ => {
                string presetName = NewPresetName;
                if (await _presetService.SavePresetWithOptionsAsync(presetName))
                {
                    SelectedPreset = Presets.FirstOrDefault(p => p.Name == presetName);
                }
            });
            DeletePresetCommand = new RelayCommand(async _ => {
                if (SelectedPreset is not null)
                {
                    await _presetService.DeletePresetAsync(SelectedPreset.Name);
                    SelectedPreset = Presets.FirstOrDefault();
                }
            });

            DropCommand = new RelayCommand(async p => {
                IsDraggingOver = false;
                if (p is IDataObject dataObject)
                {
                    bool presetsUpdated = await _dragDropService.HandleDropAsync(dataObject);
                    if (presetsUpdated)
                    {
                        SelectedPreset = Presets.FirstOrDefault(pr => pr.Name == _presetService.CurrentSettingsPresetName);
                    }
                }
            });
            DragOverCommand = new RelayCommand(p => {
                if (p is DragEventArgs e)
                {
                    var (canDrop, message) = _dragDropService.CanHandleDrop(e);
                    IsDraggingOver = canDrop;
                    DragDropMessage = message;
                }
            });
            DragLeaveCommand = new RelayCommand(_ => IsDraggingOver = false);

            ShowReleaseNotesCommand = new RelayCommand(
                _ => ShowReleaseNotesWindow(),
                _ => _allReleases.Any()
            );

            _ = RefreshAllFilesAsync();

            Settings.SFZ.ProgramMaps.CollectionChanged += (s, e) => _ = RefreshAllFilesAsync();
            Settings.SoundFont.Rules.CollectionChanged += (s, e) => _ = RefreshAllFilesAsync();
            Settings.Performance.GPU.PropertyChanged += OnGpuSettingsChanged;
            Settings.SoundFont.Layers.CollectionChanged += (s, e) => Settings.Save();
            Settings.Synthesis.PropertyChanged += (s, e) => {
                if (e.PropertyName == nameof(Settings.Synthesis.WavetableDirectory))
                {
                    _ = LoadWavetableFilesAsync();
                }
            };
            Settings.Debug.PropertyChanged += OnDebugSettingsChanged;

            NamedPipeServer.ConnectionStatusChanged += OnApiConnectionChanged;

            _ = CheckForUpdates();
            UpdateLogWatcher();
        }

        private void OnApiConnectionChanged()
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                OnPropertyChanged(nameof(IsApiActive));
            });
        }

        private void OnDebugSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.Debug.EnableNamedPipeApi))
            {
                OnPropertyChanged(nameof(IsApiActive));
            }
            if (e.PropertyName == nameof(Settings.Debug.EnableLogging))
            {
                UpdateLogWatcher();
            }
        }

        private void OnGpuSettingsChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Settings.Performance.GPU.EnableGpuSynthesis))
            {
                if (!Settings.Performance.GPU.EnableGpuSynthesis &&
                    (Settings.Performance.RenderingMode == RenderingMode.HighQualityGPU ||
                     Settings.Performance.RenderingMode == RenderingMode.RealtimeGPU))
                {
                    Settings.Performance.RenderingMode = RenderingMode.HighQualityCPU;
                }
            }
        }

        private string GetLogPath()
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            return Path.Combine(assemblyLocation, Settings.Debug.LogFilePath);
        }

        private void UpdateLogWatcher()
        {
            _logWatcher?.Dispose();
            _logWatcher = null;

            if (Settings.Debug.EnableLogging)
            {
                var logPath = GetLogPath();
                var logDirectory = Path.GetDirectoryName(logPath);
                var logFileName = Path.GetFileName(logPath);

                if (logDirectory != null && Directory.Exists(logDirectory))
                {
                    _logWatcher = new FileSystemWatcher(logDirectory, logFileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                    };
                    _logWatcher.Changed += OnLogFileChanged;
                    _logWatcher.EnableRaisingEvents = true;
                }
                _ = LoadInitialLogContent();
            }
            else
            {
                _logContentBuilder.Clear();
                LogContent = "";
            }
        }

        private async Task LoadInitialLogContent()
        {
            var logPath = GetLogPath();
            if (File.Exists(logPath))
            {
                try
                {
                    using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var sr = new StreamReader(fs, Encoding.UTF8);
                    var content = await sr.ReadToEndAsync();

                    _logContentBuilder.Clear();
                    _logContentBuilder.Append(content);
                    _logFileSize = fs.Length;

                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LogContent = content;
                    });
                }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.FileAccessError, ex, logPath);
                }
            }
        }

        private void OnLogFileChanged(object sender, FileSystemEventArgs e)
        {
            var logPath = GetLogPath();
            try
            {
                if (!File.Exists(logPath)) return;

                using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs.Length > _logFileSize)
                {
                    fs.Seek(_logFileSize, SeekOrigin.Begin);
                    using var sr = new StreamReader(fs, Encoding.UTF8);
                    var appendedContent = sr.ReadToEnd();
                    _logFileSize = fs.Length;

                    _logContentBuilder.Append(appendedContent);

                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LogContent = _logContentBuilder.ToString();
                    });
                }
                else if (fs.Length < _logFileSize)
                {
                    fs.Seek(0, SeekOrigin.Begin);
                    using var sr = new StreamReader(fs, Encoding.UTF8);
                    var newContent = sr.ReadToEnd();
                    _logFileSize = fs.Length;

                    _logContentBuilder.Clear();
                    _logContentBuilder.Append(newContent);

                    Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        LogContent = newContent;
                    });
                }
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.FileAccessError, ex, logPath);
            }
        }

        private async Task CheckForUpdates()
        {
            var currentVersion = UpdateChecker.GetCurrentVersion();
            CurrentVersionText = string.Format(Translate.Key_VersionCurrent, currentVersion);

            _allReleases = await UpdateChecker.GetAllReleasesAsync();
            (ShowReleaseNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();

            if (_allReleases.Any())
            {
                var latestVersion = _allReleases.First().TagName.TrimStart('v');
                if (UpdateChecker.CompareVersions(currentVersion, latestVersion) < 0)
                {
                    UpdateStatusText = string.Format(Translate.Key_VersionNewAvailable, latestVersion);
                }
                else
                {
                    UpdateStatusText = Translate.Key_VersionUpToDate;
                }
            }
            else
            {
                UpdateStatusText = Translate.Key_VersionCheckFailed;
            }
            UpdateStatusVisibility = Visibility.Visible;
        }

        private void ShowReleaseNotesWindow()
        {
            var window = new ReleaseNotesWindow
            {
                DataContext = new ReleaseNotesViewModel(_allReleases, UpdateChecker.GetCurrentVersion()),
                Owner = Application.Current.MainWindow
            };
            window.ShowDialog();
        }

        private void ShowSfzProgramMapEditor(SfzFileViewModel sfzFile)
        {
            var isNewMap = sfzFile.Map == null;
            var mapToEdit = sfzFile.Map ?? new SfzProgramMap { FilePath = sfzFile.FileName, Program = 0 };
            var originalProgram = mapToEdit.Program;

            var editorViewModel = new SfzProgramMapEditorViewModel(mapToEdit, sfzFile.FileName);
            var editorWindow = new SfzProgramMapEditor
            {
                DataContext = editorViewModel,
                Owner = Application.Current.MainWindow
            };

            bool? dialogResult = null;
            editorViewModel.CloseAction = (result) =>
            {
                dialogResult = result;
                editorWindow.Close();
            };

            editorWindow.ShowDialog();

            if (dialogResult == true)
            {
                if (isNewMap)
                {
                    Settings.SFZ.ProgramMaps.Add(mapToEdit);
                }
                Settings.Save();
                _ = RefreshAllFilesAsync();
            }
            else if (dialogResult == false)
            {
                if (!isNewMap)
                {
                    mapToEdit.Program = originalProgram;
                }
            }
            else
            {
                if (!isNewMap)
                {
                    Settings.SFZ.ProgramMaps.Remove(mapToEdit);
                    Settings.Save();
                    _ = RefreshAllFilesAsync();
                }
            }
        }

        private void ShowSoundFontRuleEditor(SoundFontFileViewModel sfFile)
        {
            var isNewRule = sfFile.Rule == null;
            var ruleToEdit = sfFile.Rule ?? new SoundFontRule { SoundFontFile = sfFile.FileName };
            var originalRule = isNewRule ? null : (SoundFontRule)ruleToEdit.Clone();

            var editorViewModel = new SoundFontRuleEditorViewModel(ruleToEdit, sfFile.FileName);
            var editorWindow = new SoundFontRuleEditor
            {
                DataContext = editorViewModel,
                Owner = Application.Current.MainWindow
            };

            bool? dialogResult = null;
            editorViewModel.CloseAction = (result) =>
            {
                dialogResult = result;
                editorWindow.Close();
            };

            editorWindow.ShowDialog();

            if (dialogResult == true)
            {
                if (isNewRule)
                {
                    Settings.SoundFont.Rules.Add(ruleToEdit);
                }
                Settings.Save();
                _ = RefreshAllFilesAsync();
            }
            else if (dialogResult == false)
            {
                if (!isNewRule && originalRule != null && sfFile.Rule != null)
                {
                    sfFile.Rule.MinDurationSeconds = originalRule.MinDurationSeconds;
                    sfFile.Rule.MaxDurationSeconds = originalRule.MaxDurationSeconds;
                    sfFile.Rule.MinTrackCount = originalRule.MinTrackCount;
                    sfFile.Rule.MaxTrackCount = originalRule.MaxTrackCount;
                    sfFile.Rule.RequiredPrograms = originalRule.RequiredPrograms;
                }
            }
            else
            {
                if (!isNewRule && sfFile.Rule != null)
                {
                    Settings.SoundFont.Rules.Remove(sfFile.Rule);
                    Settings.Save();
                    _ = RefreshAllFilesAsync();
                }
            }
        }

        public async Task RefreshAllFilesAsync()
        {
            await LoadSfzFilesAsync();
            await LoadSoundFontFilesAsync();
            await LoadWavetableFilesAsync();
            await _presetService.LoadPresetFilesAsync();
            SelectedPreset = Presets.FirstOrDefault(p => p.Name == _presetService.CurrentSettingsPresetName);
        }

        private async Task LoadSfzFilesAsync()
        {
            var files = await _fileService.GetSfzFilesAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                SfzFiles.Clear();
                foreach (var file in files)
                {
                    SfzFiles.Add(file);
                }
            });
        }

        private async Task LoadSoundFontFilesAsync()
        {
            var files = await _fileService.GetSoundFontFilesAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                SoundFontFiles.Clear();
                foreach (var file in files)
                {
                    SoundFontFiles.Add(file);
                }
            });
        }

        private async Task LoadWavetableFilesAsync()
        {
            var files = await _fileService.GetWavetableFilesAsync();
            Application.Current.Dispatcher.Invoke(() =>
            {
                WavetableFiles.Clear();
                foreach (var file in files)
                {
                    WavetableFiles.Add(file);
                }
            });
        }

        public void Dispose()
        {
            _logWatcher?.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
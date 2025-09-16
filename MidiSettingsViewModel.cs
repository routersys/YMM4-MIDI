using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace MIDI
{
    public class MidiSettingsViewModel : INotifyPropertyChanged
    {
        public MidiConfiguration Settings => MidiConfiguration.Default;

        public ICommand ReloadConfigCommand { get; }
        public ICommand RefreshFilesCommand { get; }
        public ICommand EditSfzMapCommand { get; }
        public ICommand EditSoundFontRuleCommand { get; }
        public ICommand AddInstrumentPresetCommand { get; }
        public ICommand RemoveInstrumentPresetCommand { get; }
        public ICommand AddCustomInstrumentCommand { get; }
        public ICommand RemoveCustomInstrumentCommand { get; }

        public ObservableCollection<SfzFileViewModel> SfzFiles { get; } = new();
        public ObservableCollection<SoundFontFileViewModel> SoundFontFiles { get; } = new();

        private string _currentVersionText = "バージョン情報を取得中...";
        public string CurrentVersionText
        {
            get => _currentVersionText;
            set
            {
                _currentVersionText = value;
                OnPropertyChanged(nameof(CurrentVersionText));
            }
        }

        private string _updateStatusText = string.Empty;
        public string UpdateStatusText
        {
            get => _updateStatusText;
            set
            {
                _updateStatusText = value;
                OnPropertyChanged(nameof(UpdateStatusText));
            }
        }

        private Visibility _updateStatusVisibility = Visibility.Collapsed;
        public Visibility UpdateStatusVisibility
        {
            get => _updateStatusVisibility;
            set
            {
                _updateStatusVisibility = value;
                OnPropertyChanged(nameof(UpdateStatusVisibility));
            }
        }

        public MidiSettingsViewModel()
        {
            ReloadConfigCommand = new RelayCommand(_ =>
            {
                Settings.Reload();
                RefreshAllFiles();
            });

            RefreshFilesCommand = new RelayCommand(_ => RefreshAllFiles());

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

            AddInstrumentPresetCommand = new RelayCommand(_ => Settings.InstrumentPresets.Add(new InstrumentPreset()));
            RemoveInstrumentPresetCommand = new RelayCommand(p => { if (p is InstrumentPreset preset) Settings.InstrumentPresets.Remove(preset); });
            AddCustomInstrumentCommand = new RelayCommand(_ => Settings.CustomInstruments.Add(new CustomInstrument()));
            RemoveCustomInstrumentCommand = new RelayCommand(p => { if (p is CustomInstrument instrument) Settings.CustomInstruments.Remove(instrument); });

            RefreshAllFiles();

            Settings.SFZ.ProgramMaps.CollectionChanged += (s, e) => RefreshAllFiles();
            Settings.SoundFont.Rules.CollectionChanged += (s, e) => RefreshAllFiles();
            Settings.Performance.GPU.PropertyChanged += OnGpuSettingsChanged;

            _ = CheckForUpdates();
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

        private async Task CheckForUpdates()
        {
            var (currentVersion, latestVersion, isNewerAvailable) = await UpdateChecker.CheckForUpdatesAsync();
            CurrentVersionText = $"現在のバージョン: {currentVersion}";

            if (latestVersion != null)
            {
                if (isNewerAvailable)
                {
                    UpdateStatusText = $"新しいバージョンが利用可能です: {latestVersion}";
                }
                else
                {
                    UpdateStatusText = "お使いのバージョンは最新です。";
                }
            }
            else
            {
                UpdateStatusText = "更新情報を取得できませんでした。";
            }
            UpdateStatusVisibility = Visibility.Visible;
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
                    sfzFile.Map = mapToEdit;
                }
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
                    sfzFile.Map = null;
                }
            }
        }

        private void ShowSoundFontRuleEditor(SoundFontFileViewModel sfFile)
        {
            var isNewRule = sfFile.Rule == null;
            var ruleToEdit = sfFile.Rule ?? new SoundFontRule { SoundFontFile = sfFile.FileName };
            var originalRule = isNewRule ? null : new SoundFontRule
            {
                SoundFontFile = ruleToEdit.SoundFontFile,
                MinDurationSeconds = ruleToEdit.MinDurationSeconds,
                MaxDurationSeconds = ruleToEdit.MaxDurationSeconds,
                MinTrackCount = ruleToEdit.MinTrackCount,
                MaxTrackCount = ruleToEdit.MaxTrackCount,
                RequiredPrograms = new ObservableCollection<int>(ruleToEdit.RequiredPrograms)
            };

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
                    sfFile.Rule = ruleToEdit;
                }
            }
            else if (dialogResult == false)
            {
                if (!isNewRule && originalRule != null)
                {
                    sfFile.Rule!.MinDurationSeconds = originalRule.MinDurationSeconds;
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
                    sfFile.Rule = null;
                }
            }
        }

        private void RefreshAllFiles()
        {
            LoadSfzFilesAsync();
            LoadSoundFontFilesAsync();
        }

        private async void LoadSfzFilesAsync()
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var sfzSearchDir = Path.Combine(assemblyLocation, Settings.SFZ.SfzSearchPath);

            var files = await Task.Run(() =>
            {
                if (!Directory.Exists(sfzSearchDir))
                {
                    Directory.CreateDirectory(sfzSearchDir);
                }
                return Directory.GetFiles(sfzSearchDir, "*.sfz", SearchOption.AllDirectories)
                                .Select(fullPath => Path.GetRelativePath(sfzSearchDir, fullPath))
                                .ToList();
            });

            Application.Current.Dispatcher.Invoke(() =>
            {
                SfzFiles.Clear();
                var mappedFiles = Settings.SFZ.ProgramMaps.ToDictionary(m => m.FilePath, m => m);

                foreach (var file in files)
                {
                    var vm = new SfzFileViewModel(file);
                    if (mappedFiles.TryGetValue(file, out var map))
                    {
                        vm.Map = map;
                    }
                    SfzFiles.Add(vm);
                }

                foreach (var map in Settings.SFZ.ProgramMaps)
                {
                    if (SfzFiles.All(f => f.FileName != map.FilePath))
                    {
                        var vm = new SfzFileViewModel(map.FilePath, isMissing: true)
                        {
                            Map = map
                        };
                        SfzFiles.Add(vm);
                    }
                }
            });
        }

        private async void LoadSoundFontFilesAsync()
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var sfDir = Path.Combine(assemblyLocation, Settings.SoundFont.DefaultSoundFontDirectory);

            var files = await Task.Run(() =>
            {
                if (!Directory.Exists(sfDir))
                {
                    Directory.CreateDirectory(sfDir);
                }
                return Directory.GetFiles(sfDir, "*.sf2", SearchOption.AllDirectories)
                                .Select(Path.GetFileName)
                                .ToList();
            });

            Application.Current.Dispatcher.Invoke(() =>
            {
                SoundFontFiles.Clear();
                var mappedFiles = Settings.SoundFont.Rules.ToDictionary(r => r.SoundFontFile, r => r);

                foreach (var file in files)
                {
                    var vm = new SoundFontFileViewModel(file!);
                    if (mappedFiles.TryGetValue(file!, out var rule))
                    {
                        vm.Rule = rule;
                    }
                    SoundFontFiles.Add(vm);
                }

                foreach (var rule in Settings.SoundFont.Rules)
                {
                    if (SoundFontFiles.All(f => f.FileName != rule.SoundFontFile))
                    {
                        var vm = new SoundFontFileViewModel(rule.SoundFontFile, isMissing: true)
                        {
                            Rule = rule
                        };
                        SoundFontFiles.Add(vm);
                    }
                }
            });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SfzFileViewModel : INotifyPropertyChanged
    {
        public string FileName { get; }
        public bool IsMissing { get; }

        private SfzProgramMap? _map;
        public SfzProgramMap? Map
        {
            get => _map;
            set
            {
                if (_map != value)
                {
                    _map = value;
                    OnPropertyChanged(nameof(Map));
                    OnPropertyChanged(nameof(IsMapped));
                    OnPropertyChanged(nameof(Program));
                }
            }
        }

        public bool IsMapped => Map != null;

        public int Program
        {
            get => Map?.Program ?? 0;
            set
            {
                if (Map != null && Map.Program != value)
                {
                    Map.Program = value;
                    OnPropertyChanged(nameof(Program));
                }
            }
        }

        public SfzFileViewModel(string fileName, bool isMissing = false)
        {
            FileName = fileName;
            IsMissing = isMissing;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class SoundFontFileViewModel : INotifyPropertyChanged
    {
        public string FileName { get; }
        public bool IsMissing { get; }

        private SoundFontRule? _rule;
        public SoundFontRule? Rule
        {
            get => _rule;
            set
            {
                if (_rule != value)
                {
                    _rule = value;
                    OnPropertyChanged(nameof(Rule));
                    OnPropertyChanged(nameof(IsMapped));
                }
            }
        }

        public bool IsMapped => Rule != null;


        public SoundFontFileViewModel(string fileName, bool isMissing = false)
        {
            FileName = fileName;
            IsMissing = isMissing;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }


    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        public RelayCommand(Action<object?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows;
using MIDI.Configuration.Models;
using MIDI.Localization;
using MIDI.UI.Commands;
using MIDI.UI.ViewModels.MidiEditor;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;
using MIDI;
using System.Security.Cryptography;

namespace MIDI.UI.ViewModels
{
    public enum SetupMode
    {
        Easy,
        Full
    }

    public enum WizardStepStatus
    {
        Pending,
        Current,
        Completed
    }

    public abstract class WizardPageViewModel : ViewModelBase
    {
        public string CategoryName { get; protected set; }
        public bool ShowSidebar { get; protected set; }
        public string IconGeometry { get; set; }

        private WizardStepStatus _status;
        public WizardStepStatus Status
        {
            get => _status;
            set => SetField(ref _status, value);
        }

        protected WizardPageViewModel(string categoryName, bool showSidebar, string iconGeometry = "")
        {
            CategoryName = categoryName;
            ShowSidebar = showSidebar;
            IconGeometry = iconGeometry;
            Status = WizardStepStatus.Pending;
        }
    }

    public class WizardSelectionPageViewModel : WizardPageViewModel
    {
        public ICommand SelectEasyCommand { get; }
        public ICommand SelectFullCommand { get; }

        public WizardSelectionPageViewModel(ICommand easyCommand, ICommand fullCommand)
            : base("はじめに", false, Icons.Home)
        {
            SelectEasyCommand = easyCommand;
            SelectFullCommand = fullCommand;
        }
    }

    public class WizardSettingsPageViewModel : WizardPageViewModel
    {
        public string CategoryKey { get; }
        public ObservableCollection<WizardSettingItem> Settings { get; }

        private WizardSettingItem? _selectedSetting;
        public WizardSettingItem? SelectedSetting
        {
            get => _selectedSetting;
            set => SetField(ref _selectedSetting, value);
        }

        public WizardSettingsPageViewModel(string categoryKey, string categoryName, string iconGeometry)
            : base(categoryName, true, iconGeometry)
        {
            CategoryKey = categoryKey;
            Settings = new ObservableCollection<WizardSettingItem>();
        }
    }

    public static class Icons
    {
        public const string Home = "M 10,20 V 14 H 14 V 20 H 19 V 12 H 22 L 12,3 L 2,12 H 5 V 20 H 10 Z";
        public const string Audio = "M 14,3.23 V 5.29 C 16.89,6.15 19,8.83 19,12 C 19,15.17 16.89,17.84 14,18.7 V 20.77 C 18,19.86 21,16.28 21,12 C 21,7.72 18,4.14 14,3.23 M 16.5,12 C 16.5,10.23 15.5,8.71 14,7.97 V 16.02 C 15.5,15.29 16.5,13.76 16.5,12 M 3,9 V 15 H 7 L 12,20 V 4 L 7,9 H 3 Z";
        public const string Performance = "M 12,16 A 3,3 0 0,1 9,13 C 9,11.88 9.61,10.9 10.5,10.39 L 20.21,4.77 L 14.68,14.35 C 14.18,15.33 13.17,16 12,16 M 12,3 C 16.97,3 21,7.03 21,12 A 9,9 0 0,1 12,21 C 7.03,21 3,16.97 3,12 C 3,7.03 7.03,3 12,3 Z";
        public const string MIDI = "M 12,3 V 13.55 C 11.41,13.21 10.73,13 10,13 C 7.79,13 6,14.79 6,17 C 6,19.21 7.79,21 10,21 C 12.21,21 14,19.21 14,17 V 7 H 18 V 3 H 12 Z";
        public const string SoundFont = "M 14,2 H 6 C 4.89,2 4,2.89 4,4 V 20 C 4,21.11 4.89,22 6,22 H 18 C 19.11,22 20,21.11 20,20 V 8 L 14,2 M 13,13 H 11 V 18 A 2,2 0 0,1 9,20 A 2,2 0 0,1 7,18 A 2,2 0 0,1 9,16 C 9.4,16 9.7,16.1 10,16.3 V 11 H 13 V 13 M 13,9 V 3.5 L 18.5,9 H 13 Z";
        public const string Sfz = "M 14,2 H 6 C 4.89,2 4,2.89 4,4 V 20 C 4,21.11 4.89,22 6,22 H 18 C 19.11,22 20,21.11 20,20 V 8 L 14,2 M 13,9 V 3.5 L 18.5,9 H 13 Z";
        public const string Synthesis = "M 2,12 L 4,16 L 8,8 L 12,18 L 16,6 L 20,14 L 22,12";
        public const string Effects = "M 7.5,5.6 L 5,7 L 6.4,4.5 L 5,2 L 7.5,3.4 L 10,2 L 8.6,4.5 L 10,7 L 7.5,5.6 M 19.5,15.4 L 22,14 L 20.6,16.5 L 22,19 L 19.5,17.6 L 17,19 L 18.4,16.5 L 17,14 L 19.5,15.4 M 22,2 L 20.6,4.5 L 22,7 L 19.5,5.6 L 17,7 L 18.4,4.5 L 17,2 L 19.5,3.4 L 22,2 M 13.34,12.78 L 15.78,10.34 L 13.66,8.22 L 11.22,10.66 L 13.34,12.78 M 14.3,13.74 L 10.74,17.3 C 10.54,17.5 10.23,17.5 10.03,17.3 L 7.2,14.47 C 7,14.27 7,13.96 7.2,13.76 L 10.76,10.2 L 14.3,13.74 Z";
        public const string Preset = "M 4,6 H 20 V 8 H 4 V 6 M 4,11 H 20 V 13 H 4 V 11 M 4,16 H 20 V 18 H 4 V 16 Z";
        public const string Custom = "M 20,2 H 4 C 2.89,2 2,2.89 2,4 V 20 C 2,21.11 2.89,22 4,22 H 20 C 21.11,22 22,21.11 22,20 V 4 C 22,2.89 21.11,2 20,2 M 20,20 H 4 V 4 H 20 V 20 M 6,6 H 18 V 8 H 6 V 6 M 6,10 H 18 V 12 H 6 V 10 M 6,14 H 18 V 16 H 6 V 14 Z";
        public const string Debug = "M 20.5,11 H 19 V 7 C 19,5.89 18.1,5 17,5 H 15 V 3 H 13 V 5 H 11 V 3 H 9 V 5 H 7 C 5.9,5 5,5.89 5,7 V 11 H 3.5 V 13 H 5 V 15 H 3.5 V 17 H 5 V 21 H 19 V 17 H 20.5 V 15 H 19 V 13 H 20.5 V 11 M 17,19 H 7 V 7 H 17 V 19 M 10,9 H 14 V 11 H 10 V 9 M 10,15 H 14 V 17 H 10 V 15 Z";
        public const string Default = "M 11,9 H 13 V 7 H 11 M 12,20 C 7.59,20 4,16.41 4,12 C 4,7.59 7.59,4 12,4 C 16.41,4 20,7.59 20,12 C 20,16.41 16.41,20 12,20 M 12,2 A 10,10 0 0,0 2,12 A 10,10 0 0,0 12,22 A 10,10 0 0,0 22,12 A 10,10 0 0,0 12,2 M 11,17 H 13 V 11 H 11 V 17 Z";
    }

    public class WizardViewModel : ViewModelBase, IDisposable
    {
        private readonly MidiConfiguration _configuration;
        private readonly ObservableCollection<WizardPageViewModel> _pages = new ObservableCollection<WizardPageViewModel>();
        private int _currentPageIndex;

        private WaveOutEvent? _waveOut;
        private MidiAudioSource? _previewSource;
        private string? _previewMidiPath;
        private byte[]? _currentMidiHash;
        private DateTime _lastAudioChangeTime;
        private bool _isPreviewScheduled;
        private bool _isPlaying;

        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetField(ref _isPlaying, value);
        }

        public ObservableCollection<WizardPageViewModel> Pages => _pages;

        private WizardPageViewModel? _selectedPage;
        public WizardPageViewModel? SelectedPage
        {
            get => _selectedPage;
            set
            {
                if (SetField(ref _selectedPage, value) && value != null)
                {
                    _currentPageIndex = _pages.IndexOf(value);
                    UpdateCommandStates();
                    UpdateStepStatus();
                    OnPropertyChanged(nameof(CurrentPageIndexDisplay));
                    OnPropertyChanged(nameof(IsSettingsPage));
                    OnPropertyChanged(nameof(SelectedSettingItem));
                }
            }
        }

        public WizardSettingItem? SelectedSettingItem => (_selectedPage as WizardSettingsPageViewModel)?.SelectedSetting;

        public string CurrentPageIndexDisplay
        {
            get
            {
                if (_currentPageIndex == 0) return "";
                return $"{_currentPageIndex} / {_pages.Count - 1}";
            }
        }

        public int TotalPages => _pages.Count;

        public bool CanGoPrevious => _currentPageIndex > 0;
        public bool CanGoNext
        {
            get
            {
                if (_currentPageIndex >= 0 && _currentPageIndex < _pages.Count)
                {
                    if (_pages[_currentPageIndex] is WizardSettingsPageViewModel settingsPage)
                    {
                        if (settingsPage.Settings.Any(s => s.HasErrors)) return false;
                    }
                }
                return _currentPageIndex < _pages.Count - 1;
            }
        }
        public bool IsLastPage => _currentPageIndex == _pages.Count - 1 && _currentPageIndex > 0;
        public bool IsSettingsPage => _selectedPage is WizardSettingsPageViewModel;

        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand FinishCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand TogglePlayCommand { get; }

        public Action<bool>? CloseAction { get; set; }

        public bool RegisterExtensions { get; set; } = true;

        public WizardViewModel(MidiConfiguration configuration)
        {
            _configuration = configuration;

            _pages.Add(new WizardSelectionPageViewModel(
                new RelayCommand(_ => SelectMode(SetupMode.Easy)),
                new RelayCommand(_ => SelectMode(SetupMode.Full))
            ));

            NextCommand = new RelayCommand(_ => GoNext(), _ => CanGoNext);
            PreviousCommand = new RelayCommand(_ => GoPrevious(), _ => CanGoPrevious);
            FinishCommand = new RelayCommand(_ => Finish(), _ => IsLastPage);
            CancelCommand = new RelayCommand(_ => Cancel());
            TogglePlayCommand = new RelayCommand(_ => TogglePlay());

            _currentPageIndex = 0;
            SelectedPage = _pages.FirstOrDefault();
            UpdateCommandStates();

            InitializePreview();
            _configuration.Audio.PropertyChanged += OnAudioConfigurationChanged;
        }

        private void InitializePreview()
        {
            Task.Run(() =>
            {
                try
                {
                    string? pluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    if (pluginDir != null)
                    {
                        string samplesDir = Path.Combine(pluginDir, "Samples");
                        if (Directory.Exists(samplesDir))
                        {
                            _previewMidiPath = Directory.GetFiles(samplesDir, "*.mid").FirstOrDefault();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to initialize preview: {ex.Message}");
                }
            });
        }

        private async void OnAudioConfigurationChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_isPlaying) return;

            _lastAudioChangeTime = DateTime.Now;
            if (_isPreviewScheduled) return;

            _isPreviewScheduled = true;
            await Task.Delay(500);
            _isPreviewScheduled = false;

            if (DateTime.Now - _lastAudioChangeTime >= TimeSpan.FromMilliseconds(500))
            {
                UpdatePreview(true);
            }
        }

        private void TogglePlay()
        {
            if (IsPlaying)
            {
                StopPreview();
                IsPlaying = false;
            }
            else
            {
                UpdatePreview(false);
                IsPlaying = true;
            }
        }

        private void UpdatePreview(bool isConfigChange)
        {
            if (string.IsNullOrEmpty(_previewMidiPath)) return;

            Task.Run(() =>
            {
                try
                {
                    byte[] newHash;
                    using (var md5 = MD5.Create())
                    using (var stream = File.OpenRead(_previewMidiPath))
                    {
                        newHash = md5.ComputeHash(stream);
                    }

                    bool sameFile = _currentMidiHash != null && newHash.SequenceEqual(_currentMidiHash);
                    _currentMidiHash = newHash;

                    if (_waveOut != null)
                    {
                        _waveOut.Stop();
                        _waveOut.Dispose();
                        _waveOut = null;
                    }
                    if (_previewSource != null)
                    {
                        _previewSource.Dispose();
                        _previewSource = null;
                    }

                    _previewSource = new MidiAudioSource(_previewMidiPath, _configuration);
                    var sampleProvider = new LoopingMidiSampleProvider(_previewSource);

                    _waveOut = new WaveOutEvent();
                    _waveOut.Init(sampleProvider);
                    _waveOut.Play();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Preview playback failed: {ex.Message}");
                    IsPlaying = false;
                }
            });
        }

        private void StopPreview()
        {
            Task.Run(() =>
            {
                if (_waveOut != null)
                {
                    _waveOut.Stop();
                    _waveOut.Dispose();
                    _waveOut = null;
                }
                if (_previewSource != null)
                {
                    _previewSource.Dispose();
                    _previewSource = null;
                }
            });
        }

        public void Dispose()
        {
            StopPreview();
            _configuration.Audio.PropertyChanged -= OnAudioConfigurationChanged;
            GC.SuppressFinalize(this);
        }

        private void SelectMode(SetupMode mode)
        {
            GenerateSettingsPages(mode);
            GoNext();
        }

        private string GetResourceString(string key, string defaultValue)
        {
            var value = WizardStringResources.GetString(key);
            return !string.IsNullOrEmpty(value) ? value : defaultValue;
        }

        private void GenerateSettingsPages(SetupMode mode)
        {
            while (_pages.Count > 1)
            {
                _pages.RemoveAt(_pages.Count - 1);
            }

            var associationPage = new WizardSettingsPageViewModel("Association", GetResourceString("Category_Association", "拡張子の関連付け"), Icons.Default);
            var propInfo = GetType().GetProperty(nameof(RegisterExtensions))!;
            associationPage.Settings.Add(new WizardSettingItem(
                this,
                propInfo,
                GetResourceString("Setting_RegisterExtensions", "拡張子の関連付け"),
                GetResourceString("Description_RegisterExtensions", ".mpp および .meffect ファイルをこのアプリケーションに関連付けます。")
            ));
            _pages.Add(associationPage);


            var configType = typeof(MidiConfiguration);
            var properties = configType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                      .Where(p => p.Name != nameof(MidiConfiguration.IsFirstLaunch) &&
                                                  p.Name != nameof(MidiConfiguration.SoundFont) &&
                                                  p.Name != nameof(MidiConfiguration.SFZ) &&
                                                  p.Name != "DistributedProcessing" &&
                                                  p.CanRead && p.CanWrite &&
                                                  p.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() == null);

            var generatedPages = new List<WizardSettingsPageViewModel>();

            foreach (var prop in properties)
            {
                if (mode == SetupMode.Easy)
                {
                    if (prop.Name != nameof(MidiConfiguration.Audio) &&
                        prop.Name != nameof(MidiConfiguration.MIDI) &&
                        prop.Name != nameof(MidiConfiguration.InstrumentPresets))
                    {
                        continue;
                    }
                }

                var categoryKey = prop.Name;
                var categoryName = GetResourceString($"Category_{categoryKey}", categoryKey);
                var icon = GetIconForCategory(categoryKey);
                var page = new WizardSettingsPageViewModel(categoryKey, categoryName, icon);
                var categoryObject = prop.GetValue(_configuration);

                if (categoryObject == null) continue;


                var settingsProps = categoryObject.GetType()
                                                  .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                  .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);


                foreach (var settingProp in settingsProps)
                {

                    if (settingProp.Name.Contains("Distributed") || settingProp.PropertyType.Name.Contains("Distributed"))
                    {
                        continue;
                    }

                    if (settingProp.PropertyType == typeof(ObservableCollection<int>))
                    {
                        continue;
                    }

                    var settingKey = settingProp.Name;
                    var settingName = GetResourceString($"Setting_{settingKey}", settingKey);
                    var settingDescription = GetResourceString($"Description_{settingKey}", "");

                    if (typeof(INotifyPropertyChanged).IsAssignableFrom(settingProp.PropertyType) &&
                        !settingProp.PropertyType.IsEnum &&
                        settingProp.PropertyType != typeof(string) &&
                        !settingProp.PropertyType.IsPrimitive &&
                        settingProp.PropertyType != typeof(Color) &&
                        !typeof(IEnumerable).IsAssignableFrom(settingProp.PropertyType))
                    {
                        var nestedObject = settingProp.GetValue(categoryObject);
                        if (nestedObject != null)
                        {
                            var nestedProps = nestedObject.GetType()
                                                          .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                          .Where(np => np.CanRead && np.CanWrite && np.GetIndexParameters().Length == 0);
                            foreach (var nestedProp in nestedProps)
                            {

                                if (nestedProp.Name.Contains("Distributed") || nestedProp.PropertyType.Name.Contains("Distributed"))
                                {
                                    continue;
                                }

                                var nestedKey = nestedProp.Name;
                                var nestedName = GetResourceString($"Setting_{nestedKey}", $"{settingName} - {nestedKey}");
                                var nestedDescription = GetResourceString($"Description_{nestedKey}", "");
                                var item = new WizardSettingItem(nestedObject, nestedProp, nestedName, nestedDescription);
                                item.ErrorsChanged += (s, e) => UpdateCommandStates();
                                page.Settings.Add(item);
                            }
                        }
                    }
                    else
                    {
                        var item = new WizardSettingItem(categoryObject, settingProp, settingName, settingDescription);
                        item.ErrorsChanged += (s, e) => UpdateCommandStates();
                        page.Settings.Add(item);
                    }
                }

                if (page.Settings.Any())
                {
                    generatedPages.Add(page);
                }
            }

            generatedPages.Sort((p1, p2) => GetCategoryOrder(p1.CategoryKey).CompareTo(GetCategoryOrder(p2.CategoryKey)));
            foreach (var page in generatedPages)
            {
                _pages.Add(page);
            }
        }

        private string GetIconForCategory(string categoryKey)
        {
            return categoryKey switch
            {
                "Association" => Icons.Default,
                nameof(MidiConfiguration.Audio) => Icons.Audio,
                nameof(MidiConfiguration.Performance) => Icons.Performance,
                nameof(MidiConfiguration.MIDI) => Icons.MIDI,
                nameof(MidiConfiguration.SoundFont) => Icons.SoundFont,
                nameof(MidiConfiguration.SFZ) => Icons.Sfz,
                nameof(MidiConfiguration.Synthesis) => Icons.Synthesis,
                nameof(MidiConfiguration.Effects) => Icons.Effects,
                nameof(MidiConfiguration.InstrumentPresets) => Icons.Preset,
                nameof(MidiConfiguration.CustomInstruments) => Icons.Custom,
                nameof(MidiConfiguration.Debug) => Icons.Debug,
                _ => Icons.Default
            };
        }

        private int GetCategoryOrder(string categoryKey)
        {
            return categoryKey switch
            {
                "Association" => -1,
                nameof(MidiConfiguration.Audio) => 0,
                nameof(MidiConfiguration.Performance) => 1,
                nameof(MidiConfiguration.MIDI) => 2,
                nameof(MidiConfiguration.SoundFont) => 3,
                nameof(MidiConfiguration.SFZ) => 4,
                nameof(MidiConfiguration.Synthesis) => 5,
                nameof(MidiConfiguration.Effects) => 6,
                nameof(MidiConfiguration.InstrumentPresets) => 7,
                nameof(MidiConfiguration.CustomInstruments) => 8,
                nameof(MidiConfiguration.Debug) => 9,
                _ => 100
            };
        }

        private void GoNext()
        {
            if (CanGoNext)
            {
                _currentPageIndex++;
                SelectedPage = _pages[_currentPageIndex];
            }
        }

        private void GoPrevious()
        {
            if (CanGoPrevious)
            {
                _currentPageIndex--;
                SelectedPage = _pages[_currentPageIndex];
            }
        }

        private void UpdateStepStatus()
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                if (i < _currentPageIndex) _pages[i].Status = WizardStepStatus.Completed;
                else if (i == _currentPageIndex) _pages[i].Status = WizardStepStatus.Current;
                else _pages[i].Status = WizardStepStatus.Pending;
            }
        }

        private void Finish()
        {
            StopPreview();
            if (RegisterExtensions)
            {
                try
                {
                    string? exePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                    if (exePath != null)
                    {
                        string pluginPath = Path.Combine(exePath, "MidiPlugin.exe");
                        if (File.Exists(pluginPath))
                        {
                            Process.Start(pluginPath, "/register");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to register extensions: {ex.Message}");
                }
            }

            _configuration.IsFirstLaunch = false;
            _configuration.SaveSynchronously();
            MidiEditorSettings.Default.SaveSynchronously();
            CloseAction?.Invoke(true);
        }

        private void Cancel()
        {
            var result = MessageBox.Show("設定の変更を破棄してウィザードを閉じますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            StopPreview();
            foreach (var page in _pages)
            {
                if (page is WizardSettingsPageViewModel settingsPage)
                {
                    foreach (var setting in settingsPage.Settings)
                    {
                        setting.ResetValue();
                    }
                }
            }
            _configuration.IsFirstLaunch = false;
            _configuration.SaveSynchronously();
            MidiEditorSettings.Default.SaveSynchronously();
            CloseAction?.Invoke(false);
        }

        private void UpdateCommandStates()
        {
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(IsLastPage));
            (NextCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PreviousCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (FinishCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private class LoopingMidiSampleProvider : ISampleProvider
        {
            private readonly MidiAudioSource _source;
            public WaveFormat WaveFormat { get; }

            public LoopingMidiSampleProvider(MidiAudioSource source)
            {
                _source = source;
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(source.Hz, 2);
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int totalRead = 0;
                while (totalRead < count)
                {
                    int read = _source.Read(buffer, offset + totalRead, count - totalRead);
                    if (read == 0)
                    {
                        _source.Seek(TimeSpan.Zero);
                        continue;
                    }
                    totalRead += read;
                }
                return totalRead;
            }
        }
    }
}
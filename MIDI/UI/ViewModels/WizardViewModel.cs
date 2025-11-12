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
using MIDI.Configuration.Models;
using MIDI.Localization;
using MIDI.UI.Commands;
using MIDI.UI.ViewModels.MidiEditor;
using System.Diagnostics;
using System.IO;

namespace MIDI.UI.ViewModels
{
    public class WizardPageViewModel : ViewModelBase
    {
        public string CategoryKey { get; }
        public string CategoryName { get; }
        public ObservableCollection<WizardSettingItem> Settings { get; }

        public WizardPageViewModel(string categoryKey, string categoryName)
        {
            CategoryKey = categoryKey;
            CategoryName = categoryName;
            Settings = new ObservableCollection<WizardSettingItem>();
        }
    }

    public class WizardViewModel : ViewModelBase
    {
        private readonly MidiConfiguration _configuration;
        private readonly List<WizardPageViewModel> _pages = new List<WizardPageViewModel>();
        private int _currentPageIndex;
        private ResourceManager _resourceManager = WizardResources.ResourceManager;

        public ObservableCollection<WizardPageViewModel> Pages => new ObservableCollection<WizardPageViewModel>(_pages);

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
                    OnPropertyChanged(nameof(CurrentPageIndexDisplay));
                }
            }
        }

        public string CurrentPageIndexDisplay => $"{_currentPageIndex + 1} / {_pages.Count}";
        public int TotalPages => _pages.Count;


        public bool CanGoPrevious => _currentPageIndex > 0;
        public bool CanGoNext => _currentPageIndex < _pages.Count - 1;
        public bool IsLastPage => _currentPageIndex == _pages.Count - 1;

        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand FinishCommand { get; }
        public ICommand CancelCommand { get; }

        public Action<bool>? CloseAction { get; set; }

        public bool RegisterExtensions { get; set; } = true;

        public WizardViewModel(MidiConfiguration configuration)
        {
            _configuration = configuration;
            LoadSettings();

            NextCommand = new RelayCommand(_ => GoNext(), _ => CanGoNext);
            PreviousCommand = new RelayCommand(_ => GoPrevious(), _ => CanGoPrevious);
            FinishCommand = new RelayCommand(_ => Finish(), _ => IsLastPage);
            CancelCommand = new RelayCommand(_ => Cancel());

            _currentPageIndex = 0;
            SelectedPage = _pages.FirstOrDefault();
            UpdateCommandStates();
        }

        private string GetResourceString(string key, string defaultValue)
        {
            try
            {
                return _resourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }


        private void LoadSettings()
        {
            var associationPage = new WizardPageViewModel("Association", GetResourceString("Category_Association", "拡張子の関連付け"));
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
                                                  p.CanRead && p.CanWrite &&
                                                  p.GetCustomAttribute<System.Text.Json.Serialization.JsonIgnoreAttribute>() == null);

            foreach (var prop in properties)
            {
                var categoryKey = prop.Name;
                var categoryName = GetResourceString($"Category_{categoryKey}", categoryKey);
                var page = new WizardPageViewModel(categoryKey, categoryName);
                var categoryObject = prop.GetValue(_configuration);

                if (categoryObject == null) continue;


                var settingsProps = categoryObject.GetType()
                                                  .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                                  .Where(p => p.CanRead && p.CanWrite && p.GetIndexParameters().Length == 0);


                foreach (var settingProp in settingsProps)
                {
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
                                var nestedKey = nestedProp.Name;
                                var nestedName = GetResourceString($"Setting_{nestedKey}", $"{settingName} - {nestedKey}");
                                var nestedDescription = GetResourceString($"Description_{nestedKey}", "");
                                page.Settings.Add(new WizardSettingItem(nestedObject, nestedProp, nestedName, nestedDescription));
                            }
                        }
                    }
                    else
                    {
                        page.Settings.Add(new WizardSettingItem(categoryObject, settingProp, settingName, settingDescription));
                    }
                }

                if (page.Settings.Any())
                {
                    _pages.Add(page);
                }
            }

            _pages.Sort((p1, p2) => GetCategoryOrder(p1.CategoryKey).CompareTo(GetCategoryOrder(p2.CategoryKey)));
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

        private void Finish()
        {
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

            foreach (var page in _pages)
            {
                foreach (var setting in page.Settings)
                {
                    setting.ResetValue();
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
    }
}
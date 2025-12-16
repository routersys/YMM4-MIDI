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
    public abstract class WizardPageViewModel : ViewModelBase
    {
        public string CategoryName { get; protected set; }
        public bool ShowSidebar { get; protected set; }

        protected WizardPageViewModel(string categoryName, bool showSidebar)
        {
            CategoryName = categoryName;
            ShowSidebar = showSidebar;
        }
    }

    public class WizardSelectionPageViewModel : WizardPageViewModel
    {
        public ICommand SelectEasyCommand { get; }
        public ICommand SelectFullCommand { get; }

        public WizardSelectionPageViewModel(ICommand easyCommand, ICommand fullCommand)
            : base("はじめに", false)
        {
            SelectEasyCommand = easyCommand;
            SelectFullCommand = fullCommand;
        }
    }

    public class WizardSettingsPageViewModel : WizardPageViewModel
    {
        public string CategoryKey { get; }
        public ObservableCollection<WizardSettingItem> Settings { get; }

        public WizardSettingsPageViewModel(string categoryKey, string categoryName)
            : base(categoryName, true)
        {
            CategoryKey = categoryKey;
            Settings = new ObservableCollection<WizardSettingItem>();
        }
    }

    public enum SetupMode
    {
        None,
        Easy,
        Full
    }

    public class WizardViewModel : ViewModelBase
    {
        private readonly MidiConfiguration _configuration;
        private readonly ObservableCollection<WizardPageViewModel> _pages = new ObservableCollection<WizardPageViewModel>();
        private int _currentPageIndex;
        private ResourceManager _resourceManager = WizardResources.ResourceManager;

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
                    OnPropertyChanged(nameof(CurrentPageIndexDisplay));
                    OnPropertyChanged(nameof(IsSettingsPage));
                }
            }
        }

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
        public bool CanGoNext => _currentPageIndex < _pages.Count - 1;
        public bool IsLastPage => _currentPageIndex == _pages.Count - 1 && _currentPageIndex > 0;
        public bool IsSettingsPage => _selectedPage is WizardSettingsPageViewModel;

        public ICommand NextCommand { get; }
        public ICommand PreviousCommand { get; }
        public ICommand FinishCommand { get; }
        public ICommand CancelCommand { get; }

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

            _currentPageIndex = 0;
            SelectedPage = _pages.FirstOrDefault();
            UpdateCommandStates();
        }

        private void SelectMode(SetupMode mode)
        {
            GenerateSettingsPages(mode);
            GoNext();
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

        private void GenerateSettingsPages(SetupMode mode)
        {
            while (_pages.Count > 1)
            {
                _pages.RemoveAt(_pages.Count - 1);
            }

            var associationPage = new WizardSettingsPageViewModel("Association", GetResourceString("Category_Association", "拡張子の関連付け"));
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
                var page = new WizardSettingsPageViewModel(categoryKey, categoryName);
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
                    generatedPages.Add(page);
                }
            }

            generatedPages.Sort((p1, p2) => GetCategoryOrder(p1.CategoryKey).CompareTo(GetCategoryOrder(p2.CategoryKey)));
            foreach (var page in generatedPages)
            {
                _pages.Add(page);
            }
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
    }
}
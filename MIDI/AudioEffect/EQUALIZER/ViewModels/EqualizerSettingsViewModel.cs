using MIDI.AudioEffect.EQUALIZER.Interfaces;
using MIDI.AudioEffect.EQUALIZER.Models;
using MIDI.AudioEffect.EQUALIZER.Views;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MIDI.AudioEffect.EQUALIZER.ViewModels
{
    public class EqualizerSettingsViewModel : ViewModelBase
    {
        private readonly IPresetService _presetService;
        private readonly IConfigService _configService;
        private PresetInfo? _selectedPreset;
        private string? _selectedDefaultPreset;
        private GroupItem _selectedGroupItem = default!;

        public ObservableCollection<PresetInfo> Presets { get; } = new();
        public ObservableCollection<string> AllPresetNames { get; } = new();
        public ObservableCollection<GroupItem> Groups { get; } = new();

        public GroupItem SelectedGroupItem
        {
            get => _selectedGroupItem;
            set
            {
                if (SetProperty(ref _selectedGroupItem, value))
                {
                    LoadData();
                }
            }
        }

        public PresetInfo? SelectedPreset
        {
            get => _selectedPreset;
            set => SetProperty(ref _selectedPreset, value);
        }

        public string? SelectedDefaultPreset
        {
            get => _selectedDefaultPreset;
            set
            {
                if (SetProperty(ref _selectedDefaultPreset, value))
                {
                    if (value != null) _configService.DefaultPreset = value == "なし" ? "" : value;
                }
            }
        }

        public bool HighQualityMode
        {
            get => _configService.HighQualityMode;
            set
            {
                if (_configService.HighQualityMode != value)
                {
                    _configService.HighQualityMode = value;
                    OnPropertyChanged();
                }
            }
        }

        public double EditorHeight
        {
            get => _configService.EditorHeight;
            set
            {
                if (_configService.EditorHeight != value)
                {
                    _configService.EditorHeight = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand RenameCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ChangeGroupCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand ClearDefaultPresetCommand { get; }

        public EqualizerSettingsViewModel()
        {
            _presetService = ServiceLocator.PresetService;
            _configService = ServiceLocator.ConfigService;

            RenameCommand = new RelayCommand(p => RenamePreset(), p => SelectedPreset != null);
            DeleteCommand = new RelayCommand(p => DeletePreset(), p => SelectedPreset != null);
            ImportCommand = new RelayCommand(p => ImportPreset());
            ExportCommand = new RelayCommand(p => ExportPreset(), p => SelectedPreset != null);
            ChangeGroupCommand = new RelayCommand(p => ChangeGroup(), p => SelectedPreset != null);
            ToggleFavoriteCommand = new RelayCommand(ToggleFavorite);
            SaveSettingsCommand = new RelayCommand(p => _configService.Save());
            ClearDefaultPresetCommand = new RelayCommand(p => SelectedDefaultPreset = "なし");

            InitializeGroups();
            _presetService.PresetsChanged += (s, e) => LoadData();
            LoadData();
        }

        private void InitializeGroups()
        {
            Groups.Add(new GroupItem("すべて", ""));
            Groups.Add(new GroupItem("お気に入り", "favorites"));
            Groups.Add(new GroupItem("ボーカル", "vocal"));
            Groups.Add(new GroupItem("BGM", "bgm"));
            Groups.Add(new GroupItem("効果音", "sfx"));
            Groups.Add(new GroupItem("その他", "other"));
            _selectedGroupItem = Groups[0];
        }

        private void LoadData()
        {
            Application.Current.Dispatcher.Invoke(() => {
                var previousSelectionName = SelectedPreset?.Name;

                Presets.Clear();
                AllPresetNames.Clear();
                AllPresetNames.Add("なし");

                var allNames = _presetService.GetAllPresetNames();
                var allPresets = allNames.Select(name => _presetService.GetPresetInfo(name));

                if (SelectedGroupItem != null && !string.IsNullOrEmpty(SelectedGroupItem.Tag))
                {
                    if (SelectedGroupItem.Tag == "favorites")
                    {
                        allPresets = allPresets.Where(p => p.IsFavorite);
                    }
                    else
                    {
                        allPresets = allPresets.Where(p => p.Group == SelectedGroupItem.Tag);
                    }
                }

                foreach (var preset in allPresets.OrderBy(p => p.Name))
                {
                    Presets.Add(preset);
                }

                foreach (var name in allNames)
                {
                    AllPresetNames.Add(name);
                }

                var currentDefault = _configService.DefaultPreset;
                SelectedDefaultPreset = string.IsNullOrEmpty(currentDefault) || !allNames.Contains(currentDefault) ? "なし" : currentDefault;

                if (previousSelectionName != null)
                {
                    SelectedPreset = Presets.FirstOrDefault(p => p.Name == previousSelectionName);
                }
            });
        }

        private void RenamePreset()
        {
            if (SelectedPreset == null) return;
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            var dialog = new InputDialogWindow("新しいプリセット名", "プリセット名の変更", SelectedPreset.Name) { Owner = window };
            if (dialog.ShowDialog() == true)
            {
                string newName = dialog.InputText;
                if (!string.IsNullOrWhiteSpace(newName) && newName != SelectedPreset.Name)
                {
                    _presetService.RenamePreset(SelectedPreset.Name, newName);
                }
            }
        }

        private void DeletePreset()
        {
            if (SelectedPreset == null) return;
            if (MessageBox.Show($"プリセット「{SelectedPreset.Name}」を削除しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _presetService.DeletePreset(SelectedPreset.Name);
            }
        }

        private void ImportPreset()
        {
            var dialog = new OpenFileDialog
            {
                Title = "プリセットファイルを選択",
                Filter = "JSONファイル (*.json)|*.json|すべてのファイル (*.*)|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                foreach (var file in dialog.FileNames)
                {
                    _presetService.ImportPreset(file, Path.GetFileNameWithoutExtension(file));
                }
            }
        }

        private void ExportPreset()
        {
            if (SelectedPreset == null) return;
            var dialog = new SaveFileDialog
            {
                Title = "プリセットをエクスポート",
                Filter = "JSONファイル (*.json)|*.json",
                FileName = $"{SelectedPreset.Name}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                _presetService.ExportPreset(SelectedPreset.Name, dialog.FileName);
            }
        }

        private void ChangeGroup()
        {
            if (SelectedPreset == null) return;
            var groups = new[] { "vocal", "bgm", "sfx", "other" };
            var groupNames = new[] { "ボーカル", "BGM", "効果音", "その他" };
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            var dialog = new GroupSelectionWindow(groups, groupNames, SelectedPreset.Group) { Owner = window };

            if (dialog.ShowDialog() == true)
            {
                _presetService.SetPresetGroup(SelectedPreset.Name, dialog.SelectedGroup);
            }
        }

        private void ToggleFavorite(object? parameter)
        {
            if (parameter is PresetInfo info)
            {
                _presetService.SetPresetFavorite(info.Name, !info.IsFavorite);
            }
        }
    }
}
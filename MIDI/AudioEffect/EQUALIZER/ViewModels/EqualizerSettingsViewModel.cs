using MIDI.AudioEffect.EQUALIZER.Interfaces;
using MIDI.AudioEffect.EQUALIZER.Models;
using MIDI.AudioEffect.EQUALIZER.Services;
using MIDI.AudioEffect.EQUALIZER.Views;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
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
        private readonly IGroupService _groupService;
        private PresetInfo? _selectedPreset;
        private string? _selectedDefaultPreset;
        private GroupItem _selectedGroupItem = default!;

        public ObservableCollection<PresetInfo> Presets { get; } = new();
        public ObservableCollection<string> AllPresetNames { get; } = new();
        public ObservableCollection<GroupItem> Groups { get; } = new();

        public IEnumerable<EqualizerAlgorithm> AlgorithmOptions => Enum.GetValues(typeof(EqualizerAlgorithm)).Cast<EqualizerAlgorithm>();

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

        public EqualizerAlgorithm SelectedAlgorithm
        {
            get => _configService.Algorithm;
            set
            {
                if (_configService.Algorithm != value)
                {
                    _configService.Algorithm = value;
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
        public ICommand AddGroupCommand { get; }
        public ICommand DeleteGroupCommand { get; }
        public ICommand MoveGroupUpCommand { get; }
        public ICommand MoveGroupDownCommand { get; }

        public EqualizerSettingsViewModel()
        {
            _presetService = ServiceLocator.PresetService;
            _configService = ServiceLocator.ConfigService;
            _groupService = ServiceLocator.GroupService;

            RenameCommand = new RelayCommand(p => RenamePreset(), p => SelectedPreset != null);
            DeleteCommand = new RelayCommand(p => DeletePreset(), p => SelectedPreset != null);
            ImportCommand = new RelayCommand(p => ImportPreset());
            ExportCommand = new RelayCommand(p => ExportPreset(), p => SelectedPreset != null);
            ChangeGroupCommand = new RelayCommand(p => ChangeGroup(), p => SelectedPreset != null);
            ToggleFavoriteCommand = new RelayCommand(ToggleFavorite);
            SaveSettingsCommand = new RelayCommand(p => _configService.Save());
            ClearDefaultPresetCommand = new RelayCommand(p => SelectedDefaultPreset = "なし");

            AddGroupCommand = new RelayCommand(p => AddGroup());
            DeleteGroupCommand = new RelayCommand(p => DeleteGroup(), p => IsUserGroup(SelectedGroupItem));
            MoveGroupUpCommand = new RelayCommand(p => MoveGroupUp(), p => IsUserGroup(SelectedGroupItem) && CanMoveUp(SelectedGroupItem));
            MoveGroupDownCommand = new RelayCommand(p => MoveGroupDown(), p => IsUserGroup(SelectedGroupItem) && CanMoveDown(SelectedGroupItem));

            RefreshGroups();
            _presetService.PresetsChanged += (s, e) => LoadData();
            _groupService.UserGroups.CollectionChanged += (s, e) => RefreshGroups();
            LoadData();
        }

        private bool IsUserGroup(GroupItem? item)
        {
            if (item == null) return false;
            return item.Tag != "" && item.Tag != "favorites" && item.Tag != "other";
        }

        private bool CanMoveUp(GroupItem item)
        {
            var index = _groupService.UserGroups.IndexOf(item);
            return index > 0;
        }

        private bool CanMoveDown(GroupItem item)
        {
            var index = _groupService.UserGroups.IndexOf(item);
            return index >= 0 && index < _groupService.UserGroups.Count - 2;
        }

        private void RefreshGroups()
        {
            var currentTag = SelectedGroupItem?.Tag;

            Groups.Clear();
            Groups.Add(new GroupItem("すべて", ""));
            Groups.Add(new GroupItem("お気に入り", "favorites"));

            foreach (var group in _groupService.UserGroups)
            {
                Groups.Add(group);
            }

            var nextSelection = Groups.FirstOrDefault(g => g.Tag == currentTag);
            SelectedGroupItem = nextSelection ?? Groups[0];
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
                Filter = "EQPファイル (*.eqp)|*.eqp|すべてのファイル (*.*)|*.*",
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
                Filter = "EQPファイル (*.eqp)|*.eqp",
                FileName = $"{SelectedPreset.Name}.eqp"
            };

            if (dialog.ShowDialog() == true)
            {
                _presetService.ExportPreset(SelectedPreset.Name, dialog.FileName);
            }
        }

        private void ChangeGroup()
        {
            if (SelectedPreset == null) return;
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            var dialog = new GroupSelectionWindow(SelectedPreset.Group) { Owner = window };

            if (dialog.ShowDialog() == true)
            {
                _presetService.SetPresetGroup(SelectedPreset.Name, dialog.SelectedGroup?.Tag ?? "");
            }
        }

        private void AddGroup()
        {
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            var dialog = new InputDialogWindow("グループ名を入力してください", "グループ追加") { Owner = window };

            if (dialog.ShowDialog() == true)
            {
                string name = dialog.InputText;
                _groupService.AddGroup(name);
            }
        }

        private void DeleteGroup()
        {
            if (SelectedGroupItem != null)
            {
                if (!IsUserGroup(SelectedGroupItem))
                {
                    MessageBox.Show("このグループは削除できません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (MessageBox.Show($"グループ「{SelectedGroupItem.Name}」を削除しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _groupService.DeleteGroup(SelectedGroupItem);
                }
            }
        }

        private void MoveGroupUp()
        {
            if (SelectedGroupItem != null)
            {
                _groupService.MoveGroupUp(SelectedGroupItem);
            }
        }

        private void MoveGroupDown()
        {
            if (SelectedGroupItem != null)
            {
                _groupService.MoveGroupDown(SelectedGroupItem);
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
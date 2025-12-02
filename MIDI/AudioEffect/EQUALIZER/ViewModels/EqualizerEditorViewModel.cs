using MIDI.AudioEffect.EQUALIZER.Interfaces;
using MIDI.AudioEffect.EQUALIZER.Models;
using MIDI.AudioEffect.EQUALIZER.Services;
using MIDI.AudioEffect.EQUALIZER.Views;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MIDI.AudioEffect.EQUALIZER.ViewModels
{
    public class EqualizerEditorViewModel : ViewModelBase
    {
        private readonly IPresetService _presetService;
        private readonly IGroupService _groupService;

        private EqualizerAudioEffect? _effect;

        private ObservableCollection<EQBand>? _bands;
        private EQBand? _selectedBand;
        private string _selectedPresetName = "プリセットを選択...";
        private double _zoom = 24;
        private double _currentTime = 0;
        private string _currentGroupFilter = "";
        private bool _isPopupOpen;
        private GroupItem _selectedGroupItem = default!;

        public event EventHandler? RequestRedraw;
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        public EqualizerAudioEffect? Effect
        {
            get => _effect;
            set
            {
                if (SetProperty(ref _effect, value))
                {
                    if (_effect != null)
                    {
                        Bands = _effect.Bands;
                    }
                }
            }
        }

        public ObservableCollection<EQBand>? Bands
        {
            get => _bands;
            set
            {
                if (SetProperty(ref _bands, value))
                {
                    OnPropertyChanged(nameof(HasBands));
                }
            }
        }

        public bool HasBands => Bands != null && Bands.Count > 0;

        public EQBand? SelectedBand
        {
            get => _selectedBand;
            set => SetProperty(ref _selectedBand, value);
        }

        public string SelectedPresetName
        {
            get => _selectedPresetName;
            set => SetProperty(ref _selectedPresetName, value);
        }

        public double Zoom
        {
            get => _zoom;
            set
            {
                if (SetProperty(ref _zoom, value))
                {
                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double CurrentTime
        {
            get => _currentTime;
            set
            {
                if (SetProperty(ref _currentTime, value))
                {
                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public double EditorHeight
        {
            get => EqualizerSettings.Default.EditorHeight;
            set
            {
                EqualizerSettings.Default.EditorHeight = value;
                EqualizerSettings.Default.Save();
                OnPropertyChanged();
            }
        }

        public bool IsPopupOpen
        {
            get => _isPopupOpen;
            set => SetProperty(ref _isPopupOpen, value);
        }

        public ObservableCollection<PresetInfo> FilteredPresets { get; } = new();
        public ObservableCollection<GroupItem> Groups { get; } = new();

        public GroupItem SelectedGroupItem
        {
            get => _selectedGroupItem;
            set
            {
                if (SetProperty(ref _selectedGroupItem, value))
                {
                    _currentGroupFilter = value?.Tag ?? "";
                    LoadPresets();
                }
            }
        }

        public ICommand SavePresetCommand { get; }
        public ICommand RenamePresetCommand { get; }
        public ICommand DeletePresetCommand { get; }
        public ICommand LoadPresetCommand { get; }
        public ICommand ToggleFavoriteCommand { get; }
        public ICommand OpenSettingsCommand { get; }
        public ICommand AddPointCommand { get; }
        public ICommand DeletePointCommand { get; }
        public ICommand ChangeGroupCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand AddGroupCommand { get; }
        public ICommand DeleteGroupCommand { get; }

        public EqualizerEditorViewModel()
        {
            _presetService = ServiceLocator.PresetService;
            _groupService = ServiceLocator.GroupService;
            _presetService.PresetsChanged += (s, e) => LoadPresets();
            _groupService.UserGroups.CollectionChanged += (s, e) => RefreshGroups();

            SavePresetCommand = new RelayCommand(p => SavePreset(), p => HasBands);
            RenamePresetCommand = new RelayCommand(RenamePreset, p => p is PresetInfo);
            DeletePresetCommand = new RelayCommand(DeletePreset, p => p is PresetInfo);
            LoadPresetCommand = new RelayCommand(LoadPreset, p => p is PresetInfo);
            ToggleFavoriteCommand = new RelayCommand(ToggleFavorite, p => p is PresetInfo);
            OpenSettingsCommand = new RelayCommand(p => OpenSettings());
            AddPointCommand = new RelayCommand(AddPoint, p => p is Point || p is null);
            DeletePointCommand = new RelayCommand(DeletePoint, p => p is EQBand);
            ChangeGroupCommand = new RelayCommand(ChangeGroup, p => p is PresetInfo);
            ExportCommand = new RelayCommand(ExportPreset, p => p is PresetInfo);

            AddGroupCommand = new RelayCommand(p => AddGroup());
            DeleteGroupCommand = new RelayCommand(DeleteGroup, p => p is GroupItem item && !string.IsNullOrEmpty(item.Tag) && item.Tag != "favorites" && item.Tag != "other");

            RefreshGroups();
            LoadPresets();
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
            _selectedGroupItem = nextSelection ?? Groups[0];
            _currentGroupFilter = _selectedGroupItem.Tag;
            OnPropertyChanged(nameof(SelectedGroupItem));
        }

        private void LoadPresets()
        {
            Application.Current.Dispatcher.Invoke(() => {
                var allPresets = _presetService.GetAllPresetNames()
                    .Select(name => _presetService.GetPresetInfo(name))
                    .ToList();

                FilteredPresets.Clear();
                IEnumerable<PresetInfo> query = allPresets;

                if (_currentGroupFilter == "favorites")
                    query = query.Where(p => p.IsFavorite);
                else if (!string.IsNullOrEmpty(_currentGroupFilter))
                    query = query.Where(p => p.Group == _currentGroupFilter);

                foreach (var preset in query.OrderBy(p => p.Name))
                {
                    FilteredPresets.Add(preset);
                }
            });
        }

        private void LoadPreset(object? parameter)
        {
            if (parameter is PresetInfo presetInfo && Effect != null)
            {
                var loadedBands = _presetService.LoadPreset(presetInfo.Name);
                if (loadedBands != null)
                {
                    BeginEdit?.Invoke(this, EventArgs.Empty);
                    Effect.ApplyBands(loadedBands);
                    SelectedPresetName = presetInfo.Name;
                    IsPopupOpen = false;
                    EndEdit?.Invoke(this, EventArgs.Empty);
                    RequestRedraw?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private void SavePreset()
        {
            if (!HasBands) return;
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            var dialog = new InputDialogWindow("プリセット名を入力してください", "プリセットの保存") { Owner = window };

            if (dialog.ShowDialog() == true)
            {
                string name = dialog.InputText;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    if (_presetService.SavePreset(name, Bands!))
                    {
                        SelectedPresetName = name;
                        LoadPresets();
                    }
                }
            }
        }

        private void RenamePreset(object? parameter)
        {
            if (parameter is not PresetInfo info) return;

            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            var dialog = new InputDialogWindow("新しいプリセット名を入力してください", "プリセット名の変更", info.Name) { Owner = window };

            if (dialog.ShowDialog() == true)
            {
                string newName = dialog.InputText;
                if (!string.IsNullOrWhiteSpace(newName) && newName != info.Name)
                {
                    if (_presetService.RenamePreset(info.Name, newName))
                    {
                        if (SelectedPresetName == info.Name) SelectedPresetName = newName;
                        LoadPresets();
                    }
                }
            }
        }

        private void DeletePreset(object? parameter)
        {
            if (parameter is not PresetInfo info) return;

            if (MessageBox.Show($"プリセット「{info.Name}」を削除しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _presetService.DeletePreset(info.Name);
                if (SelectedPresetName == info.Name) SelectedPresetName = "プリセットを選択...";
                LoadPresets();
            }
        }

        private void ExportPreset(object? parameter)
        {
            if (parameter is not PresetInfo info) return;

            var dialog = new SaveFileDialog
            {
                Title = "プリセットをエクスポート",
                Filter = "EQPファイル (*.eqp)|*.eqp",
                FileName = $"{info.Name}.eqp"
            };

            if (dialog.ShowDialog() == true)
            {
                _presetService.ExportPreset(info.Name, dialog.FileName);
            }
        }

        private void ChangeGroup(object? parameter)
        {
            if (parameter is not PresetInfo info) return;

            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            var dialog = new GroupSelectionWindow(info.Group) { Owner = window };

            if (dialog.ShowDialog() == true)
            {
                _presetService.SetPresetGroup(info.Name, dialog.SelectedGroup?.Tag ?? "");
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

        private void DeleteGroup(object? parameter)
        {
            if (parameter is GroupItem item)
            {
                if (item.Tag == "favorites" || item.Tag == "" || item.Tag == "other")
                {
                    MessageBox.Show("このグループは削除できません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (MessageBox.Show($"グループ「{item.Name}」を削除しますか？", "確認", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    _groupService.DeleteGroup(item);
                }
            }
        }

        private void ToggleFavorite(object? parameter)
        {
            if (parameter is PresetInfo info)
            {
                _presetService.SetPresetFavorite(info.Name, !info.IsFavorite);
            }
        }

        private void OpenSettings()
        {
            var window = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive) ?? Application.Current.MainWindow;
            var settingsWindow = new EqualizerSettingsWindow
            {
                Owner = window,
                DataContext = new EqualizerSettingsViewModel(),
                Topmost = true
            };
            settingsWindow.ShowDialog();
        }

        private void AddPoint(object? parameter)
        {
            if (parameter is Point point && Effect != null)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);

                var unusedBand = Effect.Items.FirstOrDefault(b => !b.IsUsed);
                if (unusedBand != null)
                {
                    unusedBand.IsUsed = true;
                    unusedBand.IsEnabled = true;
                    unusedBand.Type = (Models.FilterType)FilterType.Peak;
                    unusedBand.Frequency.Values[0].Value = point.X;
                    unusedBand.Gain.Values[0].Value = point.Y;
                    unusedBand.Q.Values[0].Value = 1.0;
                    unusedBand.StereoMode = StereoMode.Stereo;

                    Effect.UpdateBandsCollection();
                    SelectedBand = unusedBand;
                }
                else
                {
                    MessageBox.Show("これ以上ポイントを追加できません（最大32個）");
                }

                EndEdit?.Invoke(this, EventArgs.Empty);
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        private void DeletePoint(object? parameter)
        {
            if (parameter is EQBand band && Effect != null)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);

                var index = Bands?.IndexOf(band) ?? -1;

                band.IsUsed = false;
                Effect.UpdateBandsCollection();

                if (Bands != null && Bands.Count > 0)
                {
                    if (index > 0)
                        SelectedBand = Bands[index - 1];
                    else
                        SelectedBand = Bands.FirstOrDefault();
                }
                else
                {
                    SelectedBand = null;
                }

                EndEdit?.Invoke(this, EventArgs.Empty);
                RequestRedraw?.Invoke(this, EventArgs.Empty);
            }
        }

        public void NotifyBeginEdit() => BeginEdit?.Invoke(this, EventArgs.Empty);
        public void NotifyEndEdit() => EndEdit?.Invoke(this, EventArgs.Empty);
    }
}
using MIDI.AudioEffect.EQUALIZER.Interfaces;
using MIDI.AudioEffect.EQUALIZER.Models;
using MIDI.AudioEffect.EQUALIZER.UI;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace MIDI.AudioEffect.EQUALIZER.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
    }

    public class EqualizerSettingsViewModel : INotifyPropertyChanged
    {
        private readonly IPresetService _presetService;
        private readonly IConfigService _configService;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public ObservableCollection<PresetInfo> Presets { get; } = new();
        public ObservableCollection<string> AllPresetNames { get; } = new();

        private PresetInfo? _selectedPreset;
        public PresetInfo? SelectedPreset
        {
            get => _selectedPreset;
            set { _selectedPreset = value; OnPropertyChanged(nameof(SelectedPreset)); }
        }

        private string? _selectedDefaultPreset;
        public string? SelectedDefaultPreset
        {
            get => _selectedDefaultPreset;
            set
            {
                _selectedDefaultPreset = value;
                if (value != null) _configService.DefaultPreset = value == "なし" ? "" : value;
                OnPropertyChanged(nameof(SelectedDefaultPreset));
            }
        }

        public bool HighQualityMode
        {
            get => _configService.HighQualityMode;
            set { _configService.HighQualityMode = value; OnPropertyChanged(nameof(HighQualityMode)); }
        }

        public double EditorHeight
        {
            get => _configService.EditorHeight;
            set { _configService.EditorHeight = value; OnPropertyChanged(nameof(EditorHeight)); }
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

            LoadData();
            _presetService.PresetsChanged += (s, e) => LoadData();

            RenameCommand = new RelayCommand(p => RenamePreset(), p => SelectedPreset != null);
            DeleteCommand = new RelayCommand(p => DeletePreset(), p => SelectedPreset != null);
            ImportCommand = new RelayCommand(p => ImportPreset());
            ExportCommand = new RelayCommand(p => ExportPreset(), p => SelectedPreset != null);
            ChangeGroupCommand = new RelayCommand(p => ChangeGroup(), p => SelectedPreset != null);
            ToggleFavoriteCommand = new RelayCommand(p => ToggleFavorite(p));
            SaveSettingsCommand = new RelayCommand(p => _configService.Save());
            ClearDefaultPresetCommand = new RelayCommand(p => SelectedDefaultPreset = "なし");
        }

        private void LoadData()
        {
            Application.Current.Dispatcher.Invoke(() => {
                Presets.Clear();
                AllPresetNames.Clear();
                AllPresetNames.Add("なし");

                var names = _presetService.GetAllPresetNames();
                foreach (var name in names)
                {
                    Presets.Add(_presetService.GetPresetInfo(name));
                    AllPresetNames.Add(name);
                }

                var currentDefault = _configService.DefaultPreset;
                SelectedDefaultPreset = string.IsNullOrEmpty(currentDefault) || !names.Contains(currentDefault) ? "なし" : currentDefault;
            });
        }

        private void RenamePreset()
        {
            if (SelectedPreset == null) return;
            var dialog = new InputDialogWindow("新しいプリセット名", "プリセット名の変更", SelectedPreset.Name);
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
            var dialog = new GroupSelectionWindow(groups, groupNames, SelectedPreset.Group);

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
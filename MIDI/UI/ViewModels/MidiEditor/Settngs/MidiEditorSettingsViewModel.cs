using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Media;
using MIDI.UI.Commands;

namespace MIDI.UI.ViewModels.MidiEditor.Settings
{
    public class MidiEditorSettingsViewModel : ViewModelBase
    {
        public ObservableCollection<MajorSettingGroupViewModel> MajorGroups { get; } = new();

        private SettingGroupViewModel? _selectedGroup;
        public SettingGroupViewModel? SelectedGroup
        {
            get => _selectedGroup;
            set => SetField(ref _selectedGroup, value);
        }

        private readonly object _settingsRoot;

        public ICommand ResetSettingGroupCommand { get; }

        public MidiEditorSettingsViewModel(object settingsRoot)
        {
            _settingsRoot = settingsRoot;
            ResetSettingGroupCommand = new RelayCommand(ResetSettings);
            GeneratedSettingsLoader.Load(_settingsRoot, MajorGroups);
            SelectedGroup = MajorGroups.FirstOrDefault()?.Groups.FirstOrDefault();
        }

        private void ResetSettings(object? parameter)
        {
            if (parameter is not SettingGroupViewModel groupToReset) return;
            GeneratedSettingsLoader.Reset(_settingsRoot, groupToReset);
        }
    }

    public class MajorSettingGroupViewModel : ViewModelBase
    {
        public string Name { get; }
        public ObservableCollection<SettingGroupViewModel> Groups { get; } = new();

        public MajorSettingGroupViewModel(string name)
        {
            Name = name;
        }
    }

    public class SettingGroupViewModel : ViewModelBase
    {
        public string Name { get; }
        public ObservableCollection<ISetting> Settings { get; } = new();
        public string IconPath { get; }

        public SettingGroupViewModel(string name)
        {
            Name = name;
            IconPath = GetIconForCategory(name);
        }

        private string GetIconForCategory(string name)
        {
            return name switch
            {
                "表示" => "M12,4.5C7,4.5 2.73,7.61 1,12C2.73,16.39 7,19.5 12,19.5C17,19.5 21.27,16.39 23,12C21.27,7.61 17,4.5 12,4.5M12,17A5,5 0 0,1 7,12A5,5 0 0,1 12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17M12,9A3,3 0 0,0 9,12A3,3 0 0,0 12,15A3,3 0 0,0 15,12A3,3 0 0,0 12,9Z",
                "ノート" => "M12,3V13.55A4,4 0 0,0 10,13A4,4 0 0,0 6,17A4,4 0 0,0 10,21A4,4 0 0,0 14,17V7H18V3H12Z",
                "フラグ" => "M14.4,6L14,4H5V21H7V14H12.6L13,16H20V6H14.4Z",
                "グリッド" => "M4,4H8V8H4V4M10,4H14V8H10V4M16,4H20V8H16V4M4,10H8V14H4V10M10,10H14V14H10V10M16,10H20V14H16V10M4,16H8V20H4V16M10,16H14V20H10V16M16,16H20V20H16V16Z",
                "入力" => "M21,18H3V6H21M21,4H3C1.9,4 1,4.9 1,6V18C1,19.1 1.9,20 3,20H21C22.1,20 23,19.1 23,18V6C23,4.9 22.1,4 21,4M7,12V10H5V12H7M11,12V10H9V12H11M15,12V10H13V12H15M19,12V10H17V12H19Z",
                "メトロノーム" => "M12,6V18A3,3 0 0,1 9,21A3,3 0 0,1 6,18A3,3 0 0,1 9,15C9.35,15 9.68,15.07 10,15.2V6H12M18,3H15V11C15,12.11 14.1,13 13,13C11.89,13 11,12.11 11,11C11,9.89 11.89,9 13,9C13.3,9 13.58,9.07 13.84,9.21L15.2,7.79C14.53,7.3 13.79,7 13,7C10.79,7 9,8.79 9,11C9,13.21 10.79,15 13,15C15.21,15 17,13.21 17,11V3H18V3Z",
                "バックアップ" => "M17 1L7 1C5.9 1 5 1.9 5 3V17H7V3H17V1ZM21 5H11C9.9 5 9 5.9 9 7V21C9 22.1 9.9 23 11 23H21C22.1 23 23 22.1 23 21V7C23 5.9 22.1 5 21 5ZM21 21H11V7H21V21Z",
                _ => "M9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.43,13.73L20.5,19.79L19.79,20.5L13.73,14.43C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3M9.5,5A4.5,4.5 0 0,0 5,9.5A4.5,4.5 0 0,0 9.5,14A4.5,4.5 0 0,0 14,9.5A4.5,4.5 0 0,0 9.5,5Z"
            };
        }
    }
}
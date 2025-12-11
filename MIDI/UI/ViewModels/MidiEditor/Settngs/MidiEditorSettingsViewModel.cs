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
        public Geometry Icon { get; }

        public SettingGroupViewModel(string name, string? iconPath)
        {
            Name = name;
            var path = iconPath ?? "M9.5,3A6.5,6.5 0 0,1 16,9.5C16,11.11 15.41,12.59 14.43,13.73L20.5,19.79L19.79,20.5L13.73,14.43C12.59,15.41 11.11,16 9.5,16A6.5,6.5 0 0,1 3,9.5A6.5,6.5 0 0,1 9.5,3M9.5,5A4.5,4.5 0 0,0 5,9.5A4.5,4.5 0 0,0 9.5,14A4.5,4.5 0 0,0 14,9.5A4.5,4.5 0 0,0 9.5,5Z";
            Icon = Geometry.Parse(path);
        }
    }
}
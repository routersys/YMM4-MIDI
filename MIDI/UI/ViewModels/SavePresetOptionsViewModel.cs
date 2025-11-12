using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using MIDI.Configuration.Models;
using MIDI.UI.Commands;

namespace MIDI
{
    public class SettingCategoryViewModel : INotifyPropertyChanged
    {
        private bool _isSelected;
        public string Name { get; }
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public SettingCategoryViewModel(string name)
        {
            Name = name;
            IsSelected = true;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class SavePresetOptionsViewModel
    {
        public List<SettingCategoryViewModel> Categories { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public System.Action<List<string>?>? CloseAction { get; set; }

        public SavePresetOptionsViewModel()
        {
            Categories = new List<SettingCategoryViewModel>
            {
                new SettingCategoryViewModel(nameof(MidiConfiguration.Audio)),
                new SettingCategoryViewModel(nameof(MidiConfiguration.Performance)),
                new SettingCategoryViewModel(nameof(MidiConfiguration.MIDI)),
                new SettingCategoryViewModel(nameof(MidiConfiguration.SoundFont)),
                new SettingCategoryViewModel(nameof(MidiConfiguration.SFZ)),
                new SettingCategoryViewModel(nameof(MidiConfiguration.Synthesis)),
                new SettingCategoryViewModel(nameof(MidiConfiguration.Effects)),

                new SettingCategoryViewModel(nameof(MidiEditorSettings.Default.View)),
                new SettingCategoryViewModel(nameof(MidiEditorSettings.Default.Note)),
                new SettingCategoryViewModel(nameof(MidiEditorSettings.Default.Flag)),
                new SettingCategoryViewModel(nameof(MidiEditorSettings.Default.Grid)),
                new SettingCategoryViewModel(nameof(MidiEditorSettings.Default.Input)),
                new SettingCategoryViewModel(nameof(MidiEditorSettings.Default.Metronome)),
                new SettingCategoryViewModel(nameof(MidiEditorSettings.Default.Backup)),

                new SettingCategoryViewModel(nameof(MidiConfiguration.InstrumentPresets)),
                new SettingCategoryViewModel(nameof(MidiConfiguration.CustomInstruments)),
                new SettingCategoryViewModel(nameof(MidiConfiguration.Debug))
            };

            SaveCommand = new RelayCommand(_ => CloseAction?.Invoke(Categories.Where(c => c.IsSelected).Select(c => c.Name).ToList()));
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(null));
        }
    }
}
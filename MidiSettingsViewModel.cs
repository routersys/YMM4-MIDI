using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace MIDI
{
    public class MidiSettingsViewModel : INotifyPropertyChanged
    {
        public MidiConfiguration Settings => MidiConfiguration.Default;

        public ICommand ReloadConfigCommand { get; }
        public ICommand AddSfzMapCommand { get; }
        public ICommand RemoveSfzMapCommand { get; }
        public ICommand AddSoundFontRuleCommand { get; }
        public ICommand RemoveSoundFontRuleCommand { get; }
        public ICommand AddInstrumentPresetCommand { get; }
        public ICommand RemoveInstrumentPresetCommand { get; }
        public ICommand AddCustomInstrumentCommand { get; }
        public ICommand RemoveCustomInstrumentCommand { get; }

        public MidiSettingsViewModel()
        {
            ReloadConfigCommand = new RelayCommand(_ => Settings.Reload());
            AddSfzMapCommand = new RelayCommand(_ => Settings.SFZ.ProgramMaps.Add(new SfzProgramMap()));
            RemoveSfzMapCommand = new RelayCommand(p => { if (p is SfzProgramMap map) Settings.SFZ.ProgramMaps.Remove(map); });
            AddSoundFontRuleCommand = new RelayCommand(_ => Settings.SoundFont.Rules.Add(new SoundFontRule()));
            RemoveSoundFontRuleCommand = new RelayCommand(p => { if (p is SoundFontRule rule) Settings.SoundFont.Rules.Remove(rule); });
            AddInstrumentPresetCommand = new RelayCommand(_ => Settings.InstrumentPresets.Add(new InstrumentPreset()));
            RemoveInstrumentPresetCommand = new RelayCommand(p => { if (p is InstrumentPreset preset) Settings.InstrumentPresets.Remove(preset); });
            AddCustomInstrumentCommand = new RelayCommand(_ => Settings.CustomInstruments.Add(new CustomInstrument()));
            RemoveCustomInstrumentCommand = new RelayCommand(p => { if (p is CustomInstrument instrument) Settings.CustomInstruments.Remove(instrument); });
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly System.Action<object?> _execute;
        public RelayCommand(System.Action<object?> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute(parameter);

        public event System.EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
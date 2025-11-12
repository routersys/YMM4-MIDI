namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class RenameFlagViewModel : ViewModelBase
    {
        private string _flagName;
        public string FlagName
        {
            get => _flagName;
            set => SetField(ref _flagName, value);
        }

        public RenameFlagViewModel(string currentName)
        {
            _flagName = currentName;
        }
    }
}
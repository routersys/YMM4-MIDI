namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class TransposeViewModel : ViewModelBase
    {
        private int _semitones;
        public int Semitones
        {
            get => _semitones;
            set => SetField(ref _semitones, value);
        }

        private int _octaves;
        public int Octaves
        {
            get => _octaves;
            set => SetField(ref _octaves, value);
        }

        public int TotalSemitones => Semitones + (Octaves * 12);
    }
}
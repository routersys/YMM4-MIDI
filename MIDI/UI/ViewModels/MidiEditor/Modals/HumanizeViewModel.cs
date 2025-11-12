namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class HumanizeViewModel : ViewModelBase
    {
        private int _timingAmount = 10;
        public int TimingAmount
        {
            get => _timingAmount;
            set => SetField(ref _timingAmount, value);
        }

        private int _velocityAmount = 10;
        public int VelocityAmount
        {
            get => _velocityAmount;
            set => SetField(ref _velocityAmount, value);
        }
    }
}
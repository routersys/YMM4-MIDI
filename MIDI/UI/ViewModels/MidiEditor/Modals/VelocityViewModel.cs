namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class VelocityViewModel : ViewModelBase
    {
        private bool _isFixedValueMode = true;
        public bool IsFixedValueMode
        {
            get => _isFixedValueMode;
            set
            {
                if (SetField(ref _isFixedValueMode, value))
                {
                    OnPropertyChanged(nameof(IsRampMode));
                }
            }
        }

        public bool IsRampMode
        {
            get => !_isFixedValueMode;
            set => IsFixedValueMode = !value;
        }

        private int _fixedValue = 100;
        public int FixedValue
        {
            get => _fixedValue;
            set => SetField(ref _fixedValue, value);
        }

        private int _rampStartValue = 80;
        public int RampStartValue
        {
            get => _rampStartValue;
            set => SetField(ref _rampStartValue, value);
        }

        private int _rampEndValue = 120;
        public int RampEndValue
        {
            get => _rampEndValue;
            set => SetField(ref _rampEndValue, value);
        }
    }
}
using System.Collections.Generic;

namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class QuantizeSettingsViewModel : ViewModelBase
    {
        public List<string> QuantizeOptions { get; } = new List<string>
        {
            "1/4", "1/8", "1/16", "1/32",
            "1/4T", "1/8T", "1/16T", "1/32T"
        };

        private string _selectedQuantizeOption = "1/16";
        public string SelectedQuantizeOption
        {
            get => _selectedQuantizeOption;
            set => SetField(ref _selectedQuantizeOption, value);
        }

        private double _strength = 100.0;
        public double Strength
        {
            get => _strength;
            set => SetField(ref _strength, value);
        }

        private double _swing = 0.0;
        public double Swing
        {
            get => _swing;
            set => SetField(ref _swing, value);
        }
    }
}
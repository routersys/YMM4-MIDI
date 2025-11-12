using System.Collections.Generic;

namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class DisplaySettingsViewModel : ViewModelBase
    {
        public List<string> GridOptions { get; } = new List<string>
        {
            "1/4", "1/8", "1/16", "1/32",
            "1/4T", "1/8T", "1/16T", "1/32T"
        };

        public List<string> TimeRulerIntervalOptions { get; } = new List<string>
        {
            "1", "2", "5", "10", "15", "30"
        };

        private string _selectedGridOption = "1/16";
        public string SelectedGridOption
        {
            get => _selectedGridOption;
            set => SetField(ref _selectedGridOption, value);
        }

        private string _selectedTimeRulerInterval = "5";
        public string SelectedTimeRulerInterval
        {
            get => _selectedTimeRulerInterval;
            set => SetField(ref _selectedTimeRulerInterval, value);
        }
    }
}
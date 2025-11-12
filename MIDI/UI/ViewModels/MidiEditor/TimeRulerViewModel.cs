using System;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class TimeRulerViewModel : ViewModelBase
    {
        public string TimeLabel { get; }
        public double Width { get; }
        public double X { get; }

        public TimeRulerViewModel(TimeSpan time, double width, double x)
        {
            TimeLabel = time.ToString(@"m\:ss");
            Width = width;
            X = x;
        }
    }
}
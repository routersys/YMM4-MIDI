namespace MIDI.UI.ViewModels.MidiEditor
{
    public class GridLineViewModel : ViewModelBase
    {
        public double X { get; }
        public double Y { get; }

        public GridLineViewModel(double x, double y)
        {
            X = x;
            Y = y;
        }
    }
}
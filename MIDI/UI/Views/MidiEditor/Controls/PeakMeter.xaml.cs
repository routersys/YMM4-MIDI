using System.Windows.Controls;

namespace MIDI.UI.Views.MidiEditor.Controls
{
    public class MeterTick
    {
        public string Label { get; set; } = "";
        public double Height { get; set; }
    }

    public partial class PeakMeter : UserControl
    {
        public PeakMeter()
        {
            InitializeComponent();
        }
    }
}
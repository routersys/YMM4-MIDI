using System.Windows;
using System.Windows.Controls;

namespace MIDI.Shape.MidiPianoRoll.Controls
{
    public partial class MidiToggleSlider : UserControl
    {
        public MidiToggleSlider()
        {
            InitializeComponent();
        }

        public bool Value
        {
            get { return (bool)GetValue(ValueProperty); }
            set { SetValue(ValueProperty, value); }
        }
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register(nameof(Value), typeof(bool), typeof(MidiToggleSlider),
                new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    }
}
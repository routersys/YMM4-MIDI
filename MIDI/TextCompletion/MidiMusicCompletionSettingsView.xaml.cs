using System.Windows;
using System.Windows.Controls;

namespace MIDI.TextCompletion
{
    public partial class MidiMusicCompletionSettingsView : UserControl
    {
        public MidiMusicCompletionSettingsView()
        {
            InitializeComponent();
            this.SizeChanged += MidiMusicCompletionSettingsView_SizeChanged;
        }

        private void MidiMusicCompletionSettingsView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisualState(e.NewSize.Width);
        }

        private void UpdateVisualState(double width)
        {
            if (width < 750)
            {
                VisualStateManager.GoToState(this, "Compact", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "Wide", true);
            }
        }
    }
}
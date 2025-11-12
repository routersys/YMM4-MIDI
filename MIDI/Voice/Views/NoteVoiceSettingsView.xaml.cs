using System.Windows.Controls;
using System.Windows;
using System.IO;
using System.Reflection;
using System.Linq;
using MIDI.Configuration.Models;
using MIDI.Voice.ViewModels;
using MIDI.UI.Views.MidiEditor.Converters;

namespace MIDI.Voice.Views
{
    public partial class NoteVoiceSettingsView : UserControl
    {
        public NoteVoiceSettingsView()
        {
            InitializeComponent();
        }

        private void NoteVoiceSettingsView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateVisualState(e.NewSize.Width);
        }

        private void UpdateVisualState(double width)
        {
            if (width < 650)
            {
                VisualStateManager.GoToState(this, "Compact", true);
                VisualStateManager.GoToState(this, "Stacked", true);
            }
            else
            {
                VisualStateManager.GoToState(this, "Wide", true);
                VisualStateManager.GoToState(this, "SideBySide", true);
            }
        }

        private void TabListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (MenuPopup != null)
            {
                MenuPopup.IsOpen = false;
            }
        }
    }
}
using MIDI.UI.ViewModels.MidiEditor;
using MIDI.UI.ViewModels.MidiEditor.Modals;
using System.Windows;

namespace MIDI.UI.Views.MidiEditor.Modals
{
    public partial class NewMidiFileWindow : Window
    {
        public NewMidiFileWindow()
        {
            InitializeComponent();
            DataContext = new NewMidiFileViewModel();
        }

        public NewMidiFileViewModel ViewModel => (NewMidiFileViewModel)DataContext;
    }
}
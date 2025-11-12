using System.Windows;
using MIDI.UI.ViewModels.MidiEditor.Modals;

namespace MIDI.UI.Views.MidiEditor.Modals
{
    public partial class MissingMidiFileDialog : Window
    {
        public MissingMidiFileDialog(MissingMidiFileViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        public MissingMidiFileViewModel ViewModel => (MissingMidiFileViewModel)DataContext;
    }
}
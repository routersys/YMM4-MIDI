using System.Windows;
using MIDI.UI.ViewModels;
using static MIDI.MidiAudioSourcePlugin;

namespace MIDI.UI.Views
{
    public partial class MidiInfoWindow : Window
    {
        public MidiInfoWindow(MidiFileInfo fileInfo)
        {
            InitializeComponent();
            DataContext = new MidiInfoWindowViewModel(fileInfo);
        }
    }
}
using MIDI.UI.ViewModels.MidiEditor.Modals;
using System.Windows;

namespace MIDI.UI.Views.MidiEditor.Modals
{
    public partial class ShortcutHelpWindow : Window
    {
        public ShortcutHelpWindow()
        {
            InitializeComponent();
            DataContext = new ShortcutHelpViewModel();
        }
    }
}
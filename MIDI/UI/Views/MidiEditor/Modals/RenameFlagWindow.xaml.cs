using System.Windows;
using MIDI.UI.ViewModels.MidiEditor.Modals;

namespace MIDI.UI.Views.MidiEditor.Modals
{
    public partial class RenameFlagWindow : Window
    {
        public RenameFlagWindow(string currentName)
        {
            InitializeComponent();
            DataContext = new RenameFlagViewModel(currentName);
        }

        public RenameFlagViewModel ViewModel => (RenameFlagViewModel)DataContext;

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
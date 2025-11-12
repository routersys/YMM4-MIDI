using MIDI.UI.ViewModels.MidiEditor.Modals;
using System.Windows;

namespace MIDI.UI.Views.MidiEditor.Modals
{
    public partial class DisplaySettingsWindow : Window
    {
        public DisplaySettingsWindow()
        {
            InitializeComponent();
            DataContext = new DisplaySettingsViewModel();
        }

        public DisplaySettingsViewModel ViewModel => (DisplaySettingsViewModel)DataContext;

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
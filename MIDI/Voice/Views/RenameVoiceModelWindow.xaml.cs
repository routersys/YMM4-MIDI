using MIDI.Voice.ViewModels;
using System.Windows;

namespace MIDI.Voice.Views
{
    public partial class RenameVoiceModelWindow : Window
    {
        public RenameVoiceModelViewModel ViewModel => (RenameVoiceModelViewModel)DataContext;

        public RenameVoiceModelWindow(RenameVoiceModelViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
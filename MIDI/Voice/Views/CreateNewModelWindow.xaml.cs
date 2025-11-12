using MIDI.Voice.ViewModels;
using System.Windows;

namespace MIDI.Voice.Views
{
    public partial class CreateNewModelWindow : Window
    {
        public CreateNewModelViewModel ViewModel => (CreateNewModelViewModel)DataContext;

        public CreateNewModelWindow(CreateNewModelViewModel viewModel)
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
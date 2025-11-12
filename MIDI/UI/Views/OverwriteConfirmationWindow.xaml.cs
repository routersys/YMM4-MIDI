using System.Windows;

namespace MIDI
{
    public partial class OverwriteConfirmationWindow : Window
    {
        public OverwriteConfirmationWindow(OverwriteConfirmationViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseAction = (result) =>
            {
                DialogResult = result;
                Close();
            };
        }
    }
}
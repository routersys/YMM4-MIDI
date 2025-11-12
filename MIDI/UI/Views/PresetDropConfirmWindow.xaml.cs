using System.Windows;

namespace MIDI
{
    public partial class PresetDropConfirmWindow : Window
    {
        public PresetDropConfirmWindow(PresetDropConfirmViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            if (viewModel != null)
            {
                viewModel.CloseAction = (result) =>
                {
                    DialogResult = result;
                    Close();
                };
            }
        }
    }
}
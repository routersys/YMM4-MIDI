using System.Windows;

namespace MIDI
{
    public partial class FileDropProgressWindow : Window
    {
        public FileDropProgressWindow(FileDropProgressViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseAction = () => Close();
        }
    }
}
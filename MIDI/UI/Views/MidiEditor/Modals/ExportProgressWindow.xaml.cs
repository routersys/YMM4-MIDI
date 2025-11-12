using System.Windows;
using MIDI.UI.ViewModels.MidiEditor.Modals;

namespace MIDI.UI.Views.MidiEditor.Modals
{
    public partial class ExportProgressWindow : Window
    {
        public ExportProgressWindow(ExportProgressViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseAction = () => Close();
        }
    }
}
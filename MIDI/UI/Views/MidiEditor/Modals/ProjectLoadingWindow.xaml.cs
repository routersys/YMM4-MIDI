using System.Windows;
using MIDI.UI.ViewModels.MidiEditor.Modals;

namespace MIDI.UI.Views.MidiEditor.Modals
{
    public partial class ProjectLoadingWindow : Window
    {
        public ProjectLoadingWindow(ProjectLoadingViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.CloseAction = () => Close();
        }
    }
}
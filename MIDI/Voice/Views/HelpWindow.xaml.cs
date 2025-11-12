using System.Windows;
using MIDI.Voice.ViewModels;

namespace MIDI.Voice.Views
{
    public partial class HelpWindow : Window
    {
        public HelpWindow(HelpWindowViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
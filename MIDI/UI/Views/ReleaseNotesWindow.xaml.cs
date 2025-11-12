using System.Windows;
using MIDI.UI.ViewModels;

namespace MIDI.UI.Views
{
    public partial class ReleaseNotesWindow : Window
    {
        public ReleaseNotesWindow()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is ReleaseNotesViewModel viewModel)
            {
                viewModel.PropertyChanged += (s, args) =>
                {
                    if (args.PropertyName == nameof(viewModel.ReleaseNotesDocument))
                    {
                        Viewer.Document = viewModel.ReleaseNotesDocument;
                    }
                };
                Viewer.Document = viewModel.ReleaseNotesDocument;
            }
        }
    }
}
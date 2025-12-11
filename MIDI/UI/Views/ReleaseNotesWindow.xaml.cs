using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
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
                        var sb = FindResource("FadeInAnimation") as System.Windows.Media.Animation.Storyboard;
                        sb?.Begin(Viewer);
                        SetupHyperlinks(Viewer.Document);
                    }
                };
                if (viewModel.ReleaseNotesDocument != null)
                {
                    Viewer.Document = viewModel.ReleaseNotesDocument;
                    SetupHyperlinks(Viewer.Document);
                }
            }
        }

        private void SetupHyperlinks(FlowDocument document)
        {
            foreach (var block in document.Blocks)
            {
                ProcessBlock(block);
            }
        }

        private void ProcessBlock(Block block)
        {
            if (block is Paragraph paragraph)
            {
                foreach (var inline in paragraph.Inlines)
                {
                    if (inline is Hyperlink hyperlink)
                    {
                        hyperlink.RequestNavigate += (s, e) =>
                        {
                            try
                            {
                                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                                e.Handled = true;
                            }
                            catch { }
                        };
                    }
                }
            }
            else if (block is List list)
            {
                foreach (var listItem in list.ListItems)
                {
                    foreach (var subBlock in listItem.Blocks)
                    {
                        ProcessBlock(subBlock);
                    }
                }
            }
        }
    }
}
using System;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using YukkuriMovieMaker.Commons;

namespace MIDI.Shape.MidiPianoRoll.Views
{
    public partial class FileSelector : UserControl, IPropertyEditorControl
    {
        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;
        public event EventHandler? ReloadRequested;

        public FileSelector()
        {
            InitializeComponent();
        }

        public string FilePath
        {
            get { return (string)GetValue(FilePathProperty); }
            set { SetValue(FilePathProperty, value); }
        }
        public static readonly DependencyProperty FilePathProperty =
            DependencyProperty.Register(nameof(FilePath), typeof(string), typeof(FileSelector),
                new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string Filter
        {
            get { return (string)GetValue(FilterProperty); }
            set { SetValue(FilterProperty, value); }
        }
        public static readonly DependencyProperty FilterProperty =
            DependencyProperty.Register(nameof(Filter), typeof(string), typeof(FileSelector), new PropertyMetadata("All files (*.*)|*.*"));

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = Filter
            };
            if (!string.IsNullOrEmpty(FilePath) && System.IO.File.Exists(FilePath))
            {
                dialog.InitialDirectory = System.IO.Path.GetDirectoryName(FilePath);
                dialog.FileName = System.IO.Path.GetFileName(FilePath);
            }
            if (dialog.ShowDialog() == true)
            {
                BeginEdit?.Invoke(this, EventArgs.Empty);
                FilePath = dialog.FileName;
                EndEdit?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ReloadButton_Click(object sender, RoutedEventArgs e)
        {
            ReloadRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
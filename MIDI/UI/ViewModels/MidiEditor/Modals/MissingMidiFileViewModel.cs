using Microsoft.Win32;
using MIDI.UI.Commands;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class MissingMidiFileViewModel : ViewModelBase
    {
        public string OriginalPath { get; }

        private string _newPath;
        public string NewPath
        {
            get => _newPath;
            set => SetField(ref _newPath, value);
        }

        public ICommand BrowseCommand { get; }
        public ICommand OkCommand { get; }

        public MissingMidiFileViewModel(string originalPath)
        {
            OriginalPath = originalPath;
            _newPath = originalPath;

            BrowseCommand = new RelayCommand(_ =>
            {
                var openFileDialog = new OpenFileDialog
                {
                    Filter = "MIDI Files (*.mid;*.midi)|*.mid;*.midi|All files (*.*)|*.*",
                    FileName = Path.GetFileName(OriginalPath),
                    InitialDirectory = Path.GetDirectoryName(OriginalPath)
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    NewPath = openFileDialog.FileName;
                }
            });

            OkCommand = new RelayCommand(p =>
            {
                if (p is Window window)
                {
                    if (File.Exists(NewPath))
                    {
                        window.DialogResult = true;
                        window.Close();
                    }
                    else
                    {
                        MessageBox.Show("指定されたファイルが見つかりません。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });
        }
    }
}
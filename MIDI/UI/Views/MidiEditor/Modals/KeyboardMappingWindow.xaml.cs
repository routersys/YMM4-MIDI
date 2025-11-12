using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MIDI.UI.ViewModels.MidiEditor.Modals;

namespace MIDI.UI.Views.MidiEditor.Modals
{
    public partial class KeyboardMappingWindow : Window
    {
        public KeyboardMappingWindow()
        {
            InitializeComponent();
            DataContext = new KeyboardMappingViewModel();
        }

        private void PianoKey_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PianoKeyMappingViewModel keyVm)
            {
                (DataContext as KeyboardMappingViewModel)?.SelectPianoKey(keyVm);
            }
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox) return;
            (DataContext as KeyboardMappingViewModel)?.AssignKey(e.Key);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            (DataContext as KeyboardMappingViewModel)?.SaveMappings();
        }

        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.EditAction == DataGridEditAction.Commit)
            {
                var textBox = e.EditingElement as TextBox;
                if (textBox != null && string.IsNullOrEmpty(textBox.Text))
                {
                    var mapping = e.Row.Item as KeyboardMapping;
                    if (mapping != null)
                    {
                        var vm = DataContext as KeyboardMappingViewModel;
                        vm?.Mappings.Remove(mapping);
                    }
                }
            }
        }
    }
}
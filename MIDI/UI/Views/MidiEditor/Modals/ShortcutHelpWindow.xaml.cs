using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MIDI.UI.ViewModels.MidiEditor.Modals;

namespace MIDI.UI.Views.MidiEditor.Modals
{
    public partial class ShortcutHelpWindow : Window
    {
        public ShortcutHelpWindow()
        {
            InitializeComponent();
            DataContext = new ShortcutHelpViewModel();
        }

        private void ShortcutTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.Tag is ShortcutItem shortcutItem)
            {
                e.Handled = true;

                var key = e.Key == Key.System ? e.SystemKey : e.Key;

                if (key == Key.LeftCtrl || key == Key.RightCtrl ||
                    key == Key.LeftAlt || key == Key.RightAlt ||
                    key == Key.LeftShift || key == Key.RightShift ||
                    key == Key.LWin || key == Key.RWin)
                {
                    return;
                }

                var sb = new StringBuilder();
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) sb.Append("Ctrl + ");
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) sb.Append("Shift + ");
                if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) sb.Append("Alt + ");

                sb.Append(key.ToString());

                var keyString = sb.ToString();
                shortcutItem.AddKey(keyString);

                textBox.Clear();
                Keyboard.ClearFocus();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
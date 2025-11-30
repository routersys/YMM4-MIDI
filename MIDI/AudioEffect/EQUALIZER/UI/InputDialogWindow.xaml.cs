using System.Windows;

namespace MIDI.AudioEffect.EQUALIZER.UI
{
    public partial class InputDialogWindow : Window
    {
        public string InputText { get; private set; } = "";

        public InputDialogWindow(string message, string title, string defaultText = "")
        {
            InitializeComponent();
            Title = title;
            MessageLabel.Text = message;
            InputTextBox.Text = defaultText;
            InputTextBox.Focus();
            InputTextBox.SelectAll();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = InputTextBox.Text;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
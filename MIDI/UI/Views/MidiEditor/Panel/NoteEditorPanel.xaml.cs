using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using MIDI.UI.Core;

namespace MIDI.UI.Views.MidiEditor.Panel
{
    [LayoutContent("noteEditor", "編集")]
    public partial class NoteEditorPanel : UserControl
    {
        public NoteEditorPanel()
        {
            InitializeComponent();
        }

        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                var textBlock = sender as TextBlock;
                var parent = textBlock?.Parent as Grid;
                var textBox = parent?.Children.OfType<TextBox>().FirstOrDefault();
                if (textBlock != null && textBox != null)
                {
                    textBlock.Visibility = Visibility.Collapsed;
                    textBox.Visibility = Visibility.Visible;
                    textBox.Focus();
                    textBox.SelectAll();
                }
            }
        }

        private void TextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            SwitchToTextBlock(sender);
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                SwitchToTextBlock(sender);
            }
            else if (e.Key == Key.Escape)
            {
                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    BindingExpression be = textBox.GetBindingExpression(TextBox.TextProperty);
                    be?.UpdateTarget();
                    SwitchToTextBlock(sender);
                }
            }
        }

        private void SwitchToTextBlock(object sender)
        {
            var textBox = sender as TextBox;
            var parent = textBox?.Parent as Grid;
            var textBlock = parent?.Children.OfType<TextBlock>().FirstOrDefault();
            if (textBox != null && textBlock != null)
            {
                BindingExpression be = textBox.GetBindingExpression(TextBox.TextProperty);
                be?.UpdateSource();

                textBox.Visibility = Visibility.Collapsed;
                textBlock.Visibility = Visibility.Visible;
            }
        }

        private void NoteEditor_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (StartTimeEditToggle != null)
            {
                StartTimeEditToggle.IsChecked = false;
            }
            if (DurationEditToggle != null)
            {
                DurationEditToggle.IsChecked = false;
            }
        }
    }
}
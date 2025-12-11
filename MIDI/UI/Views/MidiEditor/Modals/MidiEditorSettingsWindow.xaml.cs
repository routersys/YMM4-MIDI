using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MIDI.Configuration.Models;
using MIDI.UI.ViewModels.MidiEditor.Settings;

namespace MIDI.UI.Views.MidiEditor.Modals
{
    public partial class MidiEditorSettingsWindow : Window
    {
        private readonly MidiEditorSettings _originalSettings;

        public MidiEditorSettingsWindow(object settingsRoot)
        {
            InitializeComponent();
            var settings = (settingsRoot as MidiEditorSettings)!;
            _originalSettings = settings.Clone();

            DataContext = new MidiEditorSettingsViewModel(settingsRoot);
        }

        private void TreeView_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is MidiEditorSettingsViewModel vm && e.NewValue is SettingGroupViewModel group)
            {
                vm.SelectedGroup = group;
            }
        }

        private void ColorTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (textBox.CaretIndex == 0 && (e.Key == Key.Back || e.Key == Key.Delete))
            {
                e.Handled = true;
            }
        }

        private void ColorTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            var textBox = (TextBox)sender;
            if (!textBox.Text.StartsWith("#"))
            {
                var currentCaret = textBox.CaretIndex;
                textBox.Text = "#" + textBox.Text.Replace("#", "");
                if (currentCaret > 0)
                {
                    textBox.CaretIndex = currentCaret + 1;
                }
                else
                {
                    textBox.CaretIndex = 1;
                }
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
            DialogResult = true;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ApplySettings();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is MidiEditorSettingsViewModel vm)
            {
                var field = vm.GetType().GetField("_settingsRoot", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var currentSettings = field.GetValue(vm) as MidiEditorSettings;
                    currentSettings?.CopyFrom(_originalSettings);
                }
            }
            DialogResult = false;
        }

        private void ApplySettings()
        {
            MidiEditorSettings.Default.Save();
        }
    }
}
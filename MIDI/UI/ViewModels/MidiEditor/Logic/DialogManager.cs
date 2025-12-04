using MIDI.Configuration.Models;
using MIDI.UI.Views.MidiEditor.Modals;
using System.Windows;

namespace MIDI.UI.ViewModels.MidiEditor.Logic
{
    public class DialogManager
    {
        private readonly MidiEditorViewModel _vm;

        public DialogManager(MidiEditorViewModel vm)
        {
            _vm = vm;
        }

        public void OpenEditorSettings()
        {
            var settingsWindow = new MidiEditorSettingsWindow(MidiEditorSettings.Default)
            {
                Owner = Application.Current.MainWindow
            };
            settingsWindow.ShowDialog();
            MidiConfiguration.Default.Save();
            _vm.UpdateBackupTimer();
        }

        public void OpenKeyboardMapping()
        {
            var keyboardMappingWindow = new KeyboardMappingWindow
            {
                Owner = Application.Current.MainWindow
            };
            keyboardMappingWindow.ShowDialog();
        }

        public void ShowShortcutHelp()
        {
            var helpWindow = new ShortcutHelpWindow
            {
                Owner = Application.Current.MainWindow
            };
            helpWindow.ShowDialog();
        }
    }
}
using YukkuriMovieMaker.Commons;
using System.Windows;
using MIDI.Voice.ViewModels;

namespace MIDI.Voice.Views
{
    internal class NoteNotationHelpAttribute : PropertyEditorAttribute2
    {
        public override FrameworkElement Create()
        {
            return new NoteNotationHelpControl();
        }

        public override void SetBindings(FrameworkElement control, ItemProperty[] itemProperties)
        {
            if (control is not NoteNotationHelpControl editor)
                return;
            editor.DataContext = new NoteNotationHelpViewModel(itemProperties);
        }

        public override void ClearBindings(FrameworkElement control)
        {
            if (control is not NoteNotationHelpControl editor)
                return;
            var vm = editor.DataContext as NoteNotationHelpViewModel;
            vm?.Dispose();
            editor.DataContext = null;
        }
    }
}
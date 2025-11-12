using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MIDI.UI.Commands;

namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class ProjectLoadingViewModel : ViewModelBase
    {
        private string _statusMessage = "読み込み中...";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public Action? CloseAction { get; set; }

        public void Close()
        {
            CloseAction?.Invoke();
        }
    }
}
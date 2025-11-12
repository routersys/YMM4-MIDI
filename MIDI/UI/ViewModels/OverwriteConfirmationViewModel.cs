using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MIDI.UI.Commands;

namespace MIDI
{
    public class OverwriteConfirmationViewModel : INotifyPropertyChanged
    {
        public string Message { get; }
        public ICommand OverwriteCommand { get; }
        public ICommand CancelCommand { get; }
        public Action<bool>? CloseAction { get; set; }

        public OverwriteConfirmationViewModel(string fileName)
        {
            Message = $"同名のファイルが既に存在します。上書きしますか？\n{fileName}";
            OverwriteCommand = new RelayCommand(_ => CloseAction?.Invoke(true));
            CancelCommand = new RelayCommand(_ => CloseAction?.Invoke(false));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
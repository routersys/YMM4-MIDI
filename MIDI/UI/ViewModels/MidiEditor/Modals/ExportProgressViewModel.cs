using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MIDI.UI.Commands;

namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class ExportProgressViewModel : INotifyPropertyChanged
    {
        private string _fileName = string.Empty;
        public string FileName
        {
            get => _fileName;
            set => SetField(ref _fileName, value);
        }

        private double _progress;
        public double Progress
        {
            get => _progress;
            set => SetField(ref _progress, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        private bool _isComplete;
        public bool IsComplete
        {
            get => _isComplete;
            set => SetField(ref _isComplete, value);
        }

        private bool _isIndeterminate;
        public bool IsIndeterminate
        {
            get => _isIndeterminate;
            set => SetField(ref _isIndeterminate, value);
        }

        public ICommand CloseCommand { get; }
        public Action? CloseAction { get; set; }

        public ExportProgressViewModel()
        {
            CloseCommand = new RelayCommand(_ => CloseAction?.Invoke(), _ => IsComplete);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            if (propertyName == nameof(IsComplete))
            {
                (CloseCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            return true;
        }
    }
}
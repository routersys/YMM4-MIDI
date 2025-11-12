using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.UI.ViewModels.Models
{
    public class PresetViewModel : INotifyPropertyChanged
    {
        private string _name = "";
        public string Name { get => _name; set => SetField(ref _name, value); }

        private int _changesCount;
        public int ChangesCount { get => _changesCount; set => SetField(ref _changesCount, value); }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
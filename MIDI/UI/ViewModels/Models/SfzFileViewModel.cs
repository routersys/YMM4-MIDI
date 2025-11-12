using MIDI.Configuration.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.UI.ViewModels.Models
{
    public class SfzFileViewModel : INotifyPropertyChanged
    {
        public string FileName { get; }
        public bool IsMissing { get; }

        private SfzProgramMap? _map;
        public SfzProgramMap? Map
        {
            get => _map;
            set
            {
                if (_map != value)
                {
                    _map = value;
                    OnPropertyChanged(nameof(Map));
                    OnPropertyChanged(nameof(IsMapped));
                    OnPropertyChanged(nameof(Program));
                }
            }
        }

        public bool IsMapped => Map != null;

        public int Program
        {
            get => Map?.Program ?? 0;
            set
            {
                if (Map != null && Map.Program != value)
                {
                    Map.Program = value;
                    OnPropertyChanged(nameof(Program));
                }
            }
        }

        public SfzFileViewModel(string fileName, bool isMissing = false)
        {
            FileName = fileName;
            IsMissing = isMissing;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
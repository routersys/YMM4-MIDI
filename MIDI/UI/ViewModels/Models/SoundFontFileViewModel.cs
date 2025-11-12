using MIDI.Configuration.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.UI.ViewModels.Models
{
    public class SoundFontFileViewModel : INotifyPropertyChanged
    {
        public string FileName { get; }
        public bool IsMissing { get; }

        private SoundFontRule? _rule;
        public SoundFontRule? Rule
        {
            get => _rule;
            set
            {
                if (_rule != value)
                {
                    _rule = value;
                    OnPropertyChanged(nameof(Rule));
                    OnPropertyChanged(nameof(IsMapped));
                }
            }
        }

        public bool IsMapped => Rule != null;


        public SoundFontFileViewModel(string fileName, bool isMissing = false)
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
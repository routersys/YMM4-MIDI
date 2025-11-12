using System.IO;
using MIDI.UI.ViewModels.MidiEditor;
using MIDI.Voice.Models;

namespace MIDI.Voice.ViewModels
{
    public class UtauVoiceViewModel : ViewModelBase
    {
        public UtauVoiceInfo Info { get; }

        public string Name => Info.Name;
        public string DisplayInfo => Info.Info;
        public string ImagePath => Info.ImagePath;
        public string BasePath => Info.BasePath;

        public UtauVoiceViewModel(UtauVoiceInfo info)
        {
            Info = info;
            Info.PropertyChanged += (s, e) =>
            {
                OnPropertyChanged(e.PropertyName);
                if (e.PropertyName == nameof(Info.Info))
                    OnPropertyChanged(nameof(DisplayInfo));
            };
        }
    }
}
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static MIDI.MidiAudioSourcePlugin;

namespace MIDI.UI.ViewModels
{
    public class MidiInfoWindowViewModel : INotifyPropertyChanged
    {
        public MidiFileInfo FileInfo { get; }

        public string FileSize => $"{FileInfo.FileSize / 1024.0:F2} KB";
        public string FormatDescription => FileInfo.GetFormatDescription();
        public string ComplexityDescription => FileInfo.GetComplexityDescription();
        public string ConfigurationStatus => FileInfo.GetConfigurationStatusDescription();
        public string SoundFontSupport => FileInfo.SupportsSoundFont ? "有効" : "無効";
        public string RecommendedEffects => FileInfo.RecommendedSettings.EnableEffects ? "有効" : "無効";
        public string RecommendedParallel => FileInfo.RecommendedSettings.EnableParallelProcessing ? "有効" : "無効";
        public string RecommendedSoundFont => FileInfo.RecommendedSettings.UseSoundFont ? "使用を推奨" : "非推奨";

        public MidiInfoWindowViewModel(MidiFileInfo fileInfo)
        {
            FileInfo = fileInfo;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
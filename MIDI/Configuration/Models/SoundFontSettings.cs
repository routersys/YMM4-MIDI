using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public class SoundFontSettings : INotifyPropertyChanged
    {
        private bool _enableSoundFont = true;
        public bool EnableSoundFont { get => _enableSoundFont; set => SetField(ref _enableSoundFont, value); }

        private bool _useDefaultSoundFont = true;
        public bool UseDefaultSoundFont { get => _useDefaultSoundFont; set => SetField(ref _useDefaultSoundFont, value); }

        private string _defaultSoundFontDirectory = "SoundFonts";
        public string DefaultSoundFontDirectory { get => _defaultSoundFontDirectory; set => SetField(ref _defaultSoundFontDirectory, value); }

        private string _preferredSoundFont = string.Empty;
        public string PreferredSoundFont { get => _preferredSoundFont; set => SetField(ref _preferredSoundFont, value); }

        private bool _fallbackToSynthesis = true;
        public bool FallbackToSynthesis { get => _fallbackToSynthesis; set => SetField(ref _fallbackToSynthesis, value); }

        private ObservableCollection<SoundFontLayer> _layers = new();
        public ObservableCollection<SoundFontLayer> Layers { get => _layers; set => SetField(ref _layers, value); }

        private ObservableCollection<SoundFontRule> _rules = new();
        public ObservableCollection<SoundFontRule> Rules { get => _rules; set => SetField(ref _rules, value); }

        public void CopyFrom(SoundFontSettings source)
        {
            EnableSoundFont = source.EnableSoundFont;
            UseDefaultSoundFont = source.UseDefaultSoundFont;
            DefaultSoundFontDirectory = source.DefaultSoundFontDirectory;
            PreferredSoundFont = source.PreferredSoundFont;
            FallbackToSynthesis = source.FallbackToSynthesis;
            Layers = new ObservableCollection<SoundFontLayer>(source.Layers.Select(l => (SoundFontLayer)l.Clone()));
            Rules = new ObservableCollection<SoundFontRule>(source.Rules.Select(r => (SoundFontRule)r.Clone()));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
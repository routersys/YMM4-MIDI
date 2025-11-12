using System.ComponentModel;
using System.Runtime.CompilerServices;
using NAudio.Midi;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class TempoEventViewModel : ViewModelBase
    {
        public event PropertyChangedEventHandler? PlaybackPropertyChanged;

        private long _absoluteTime;
        public long AbsoluteTime
        {
            get => _absoluteTime;
            set
            {
                if (SetField(ref _absoluteTime, value))
                {
                    TempoEvent.AbsoluteTime = value;
                    PlaybackPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AbsoluteTime)));
                }
            }
        }

        private double _bpm;
        public double Bpm
        {
            get => _bpm;
            set
            {
                if (SetField(ref _bpm, value))
                {
                    TempoEvent.MicrosecondsPerQuarterNote = (int)(60000000 / value);
                    PlaybackPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Bpm)));
                }
            }
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetField(ref _isVisible, value);
        }

        private bool _isMatch;
        public bool IsMatch
        {
            get => _isMatch;
            set => SetField(ref _isMatch, value);
        }

        public TempoEvent TempoEvent { get; }

        public TempoEventViewModel(TempoEvent tempoEvent)
        {
            TempoEvent = tempoEvent;
            _absoluteTime = tempoEvent.AbsoluteTime;
            _bpm = 60000000.0 / tempoEvent.MicrosecondsPerQuarterNote;
        }

        public void UpdateEvent()
        {
            TempoEvent.AbsoluteTime = AbsoluteTime;
            TempoEvent.MicrosecondsPerQuarterNote = (int)(60000000 / Bpm);
        }
    }
}
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NAudio.Midi;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class ProgramChangeEventViewModel : ViewModelBase
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
                    PatchChangeEvent.AbsoluteTime = value;
                    PlaybackPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AbsoluteTime)));
                }
            }
        }

        private int _channel;
        public int Channel
        {
            get => _channel;
            set
            {
                if (SetField(ref _channel, value))
                {
                    PatchChangeEvent.Channel = value;
                    PlaybackPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Channel)));
                }
            }
        }

        private int _patch;
        public int Patch
        {
            get => _patch;
            set
            {
                if (SetField(ref _patch, value))
                {
                    PatchChangeEvent.Patch = value;
                    PlaybackPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Patch)));
                }
            }
        }

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set => SetField(ref _isVisible, value);
        }

        public PatchChangeEvent PatchChangeEvent { get; }

        public ProgramChangeEventViewModel(PatchChangeEvent patchChangeEvent)
        {
            PatchChangeEvent = patchChangeEvent;
            _absoluteTime = patchChangeEvent.AbsoluteTime;
            _channel = patchChangeEvent.Channel;
            _patch = patchChangeEvent.Patch;
        }
    }
}
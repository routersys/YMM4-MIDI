using System.ComponentModel;
using System.Runtime.CompilerServices;
using NAudio.Midi;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class ControlChangeEventViewModel : ViewModelBase
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
                    ControlChangeEvent.AbsoluteTime = value;
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
                    ControlChangeEvent.Channel = value;
                    PlaybackPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Channel)));
                }
            }
        }

        private MidiController _controller;
        public MidiController Controller
        {
            get => _controller;
            set
            {
                if (SetField(ref _controller, value))
                {
                    ControlChangeEvent.Controller = value;
                    PlaybackPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Controller)));
                }
            }
        }

        private int _controllerValue;
        public int ControllerValue
        {
            get => _controllerValue;
            set
            {
                if (SetField(ref _controllerValue, value))
                {
                    ControlChangeEvent.ControllerValue = value;
                    PlaybackPropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ControllerValue)));
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

        public ControlChangeEvent ControlChangeEvent { get; }

        public ControlChangeEventViewModel(ControlChangeEvent controlChangeEvent)
        {
            ControlChangeEvent = controlChangeEvent;
            _absoluteTime = controlChangeEvent.AbsoluteTime;
            _channel = controlChangeEvent.Channel;
            _controller = controlChangeEvent.Controller;
            _controllerValue = controlChangeEvent.ControllerValue;
        }

        public void UpdateEvent()
        {
            ControlChangeEvent.AbsoluteTime = AbsoluteTime;
            ControlChangeEvent.Channel = Channel;
            ControlChangeEvent.Controller = Controller;
            ControlChangeEvent.ControllerValue = ControllerValue;
        }
    }
}
using System;
using System.Windows.Input;
using MIDI.UI.Commands;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class MultiNoteEditorViewModel : ViewModelBase
    {
        private readonly MidiEditorViewModel _parentViewModel;

        private TimeSpan _durationChange = TimeSpan.Zero;
        public TimeSpan DurationChange
        {
            get => _durationChange;
            set
            {
                if (SetField(ref _durationChange, value))
                {
                    _durationChangeTicks = _parentViewModel.TimeToTicks(value);
                    OnPropertyChanged(nameof(DurationChangeTicks));
                    UpdateDurationChangeTimes();
                }
            }
        }

        private long _durationChangeTicks;
        public long DurationChangeTicks
        {
            get => _durationChangeTicks;
            set
            {
                if (SetField(ref _durationChangeTicks, value))
                {
                    _durationChange = _parentViewModel.TicksToTime(value);
                    OnPropertyChanged(nameof(DurationChange));
                    UpdateDurationChangeTimes();
                }
            }
        }

        private int _durationChangeMinutes;
        public int DurationChangeMinutes
        {
            get => _durationChangeMinutes;
            set
            {
                if (SetField(ref _durationChangeMinutes, value))
                {
                    UpdateDurationChangeFromParts();
                }
            }
        }

        private int _durationChangeSeconds;
        public int DurationChangeSeconds
        {
            get => _durationChangeSeconds;
            set
            {
                if (SetField(ref _durationChangeSeconds, value))
                {
                    UpdateDurationChangeFromParts();
                }
            }
        }

        private int _durationChangeMilliseconds;
        public int DurationChangeMilliseconds
        {
            get => _durationChangeMilliseconds;
            set
            {
                if (SetField(ref _durationChangeMilliseconds, value))
                {
                    UpdateDurationChangeFromParts();
                }
            }
        }

        private int _velocityChange = 0;
        public int VelocityChange
        {
            get => _velocityChange;
            set => SetField(ref _velocityChange, value);
        }

        private int _channelChange = 1;
        public int ChannelChange
        {
            get => _channelChange;
            set => SetField(ref _channelChange, value);
        }

        private double _staccatoPercentage = 50;
        public double StaccatoPercentage
        {
            get => _staccatoPercentage;
            set => SetField(ref _staccatoPercentage, value);
        }

        public ICommand ApplyDurationChangeCommand { get; }
        public ICommand ApplyVelocityChangeCommand { get; }
        public ICommand ApplyChannelChangeCommand { get; }
        public ICommand ApplyLegatoCommand { get; }
        public ICommand ApplyStaccatoCommand { get; }

        public ICommand IncrementDurationMinutesCommand { get; }
        public ICommand DecrementDurationMinutesCommand { get; }
        public ICommand IncrementDurationSecondsCommand { get; }
        public ICommand DecrementDurationSecondsCommand { get; }
        public ICommand IncrementDurationMillisecondsCommand { get; }
        public ICommand DecrementDurationMillisecondsCommand { get; }

        public MultiNoteEditorViewModel(MidiEditorViewModel parentViewModel)
        {
            _parentViewModel = parentViewModel;

            ApplyDurationChangeCommand = new RelayCommand(_ => _parentViewModel.ChangeDurationForSelectedNotes(DurationChangeTicks));
            ApplyVelocityChangeCommand = new RelayCommand(_ => _parentViewModel.ChangeVelocityForSelectedNotes(VelocityChange));
            ApplyChannelChangeCommand = new RelayCommand(_ => _parentViewModel.ChangeChannelForSelectedNotes(ChannelChange));
            ApplyLegatoCommand = new RelayCommand(_ => _parentViewModel.LegatoCommand.Execute(null));
            ApplyStaccatoCommand = new RelayCommand(_ => _parentViewModel.ApplyStaccato(null, StaccatoPercentage / 100.0));

            IncrementDurationMinutesCommand = new RelayCommand(_ => UpdateDurationChange(TimeSpan.FromMinutes(1)));
            DecrementDurationMinutesCommand = new RelayCommand(_ => UpdateDurationChange(TimeSpan.FromMinutes(-1)));
            IncrementDurationSecondsCommand = new RelayCommand(_ => UpdateDurationChange(TimeSpan.FromSeconds(1)));
            DecrementDurationSecondsCommand = new RelayCommand(_ => UpdateDurationChange(TimeSpan.FromSeconds(-1)));
            IncrementDurationMillisecondsCommand = new RelayCommand(_ => UpdateDurationChange(TimeSpan.FromMilliseconds(GetMillisecondDelta())));
            DecrementDurationMillisecondsCommand = new RelayCommand(_ => UpdateDurationChange(TimeSpan.FromMilliseconds(-GetMillisecondDelta())));
        }

        private int GetMillisecondDelta()
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return 10;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return 100;
            return 1;
        }

        private void UpdateDurationChange(TimeSpan delta)
        {
            DurationChange += delta;
        }

        private void UpdateDurationChangeTimes()
        {
            _durationChangeMinutes = _durationChange.Minutes;
            _durationChangeSeconds = _durationChange.Seconds;
            _durationChangeMilliseconds = _durationChange.Milliseconds;
            OnPropertyChanged(nameof(DurationChangeMinutes));
            OnPropertyChanged(nameof(DurationChangeSeconds));
            OnPropertyChanged(nameof(DurationChangeMilliseconds));
        }

        private void UpdateDurationChangeFromParts()
        {
            DurationChange = new TimeSpan(0, 0, DurationChangeMinutes, DurationChangeSeconds, DurationChangeMilliseconds);
        }
    }
}
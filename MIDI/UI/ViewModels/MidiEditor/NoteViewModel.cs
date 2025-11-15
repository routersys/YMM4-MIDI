using System;
using System.Collections.Generic;
using System.Windows.Input;
using System.Windows.Media;
using MIDI.UI.Commands;
using NAudioMidi = NAudio.Midi;
using MIDI.Configuration.Models;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class NoteViewModel : ViewModelBase
    {
        private readonly MidiEditorViewModel _parentViewModel;
        private readonly NAudioMidi.NoteOnEvent _noteOnEvent;
        private List<NAudioMidi.TempoEvent> _tempoMap;
        private readonly int _ticksPerQuarterNote;

        private TimeSpan _startTime;
        private TimeSpan _duration;

        private bool _isEditing;
        public bool IsEditing
        {
            get => _isEditing;
            set
            {
                if (SetField(ref _isEditing, value))
                {
                    OnPropertyChanged(nameof(FillBrush));
                    if (value)
                    {
                        _parentViewModel.EditingNotes.Add(this);
                    }
                    else
                    {
                        _parentViewModel.EditingNotes.Remove(this);
                    }
                    _parentViewModel.RequestNoteRedraw(this);
                }
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetField(ref _isSelected, value))
                {
                    OnPropertyChanged(nameof(FillBrush));
                    _parentViewModel.RequestNoteRedraw(this);
                }
            }
        }

        private int _centOffset;
        public int CentOffset
        {
            get => _centOffset;
            set
            {
                if (_centOffset != value)
                {
                    _parentViewModel.RequestNoteRedraw(this);
                    _centOffset = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Y));
                    OnPropertyChanged(nameof(CentOffsetText));
                    _parentViewModel.RequestNoteRedraw(this);
                }
            }
        }

        public string CentOffsetText => _centOffset != 0 ? $"{_centOffset:+#;-#;0}" : "";
        public bool ShowCentOffset => _parentViewModel.TuningSystem == Configuration.Models.TuningSystemType.Microtonal && _centOffset != 0;

        private Color _color;
        public Color Color
        {
            get => _color;
            set
            {
                if (SetField(ref _color, value))
                {
                    OnPropertyChanged(nameof(FillBrush));
                    _parentViewModel.RequestNoteRedraw(this);
                }
            }
        }

        public Brush FillBrush
        {
            get
            {
                if (IsEditing)
                {
                    return new SolidColorBrush(_parentViewModel.GetColorForChannel(Channel));
                }
                if (IsSelected)
                {
                    return new SolidColorBrush(MidiEditorSettings.Default.Note.SelectedNoteColor);
                }
                return new SolidColorBrush(_color);
            }
        }

        public ICommand IncrementStartTimeMinutesCommand { get; }
        public ICommand DecrementStartTimeMinutesCommand { get; }
        public ICommand IncrementStartTimeSecondsCommand { get; }
        public ICommand DecrementStartTimeSecondsCommand { get; }
        public ICommand IncrementStartTimeMillisecondsCommand { get; }
        public ICommand DecrementStartTimeMillisecondsCommand { get; }
        public ICommand IncrementDurationMinutesCommand { get; }
        public ICommand DecrementDurationMinutesCommand { get; }
        public ICommand IncrementDurationSecondsCommand { get; }
        public ICommand DecrementDurationSecondsCommand { get; }
        public ICommand IncrementDurationMillisecondsCommand { get; }
        public ICommand DecrementDurationMillisecondsCommand { get; }


        public NoteViewModel(NAudioMidi.NoteOnEvent noteOnEvent, int ticksPerQuarterNote, List<NAudioMidi.TempoEvent> tempoMap, MidiEditorViewModel parentViewModel)
        {
            _noteOnEvent = noteOnEvent;
            _ticksPerQuarterNote = ticksPerQuarterNote;
            _tempoMap = tempoMap;
            _parentViewModel = parentViewModel;
            _color = MidiEditorSettings.Default.Note.NoteColor;

            IncrementStartTimeMinutesCommand = new RelayCommand(_ => UpdateStartTime(TimeSpan.FromMinutes(1)));
            DecrementStartTimeMinutesCommand = new RelayCommand(_ => UpdateStartTime(TimeSpan.FromMinutes(-1)));
            IncrementStartTimeSecondsCommand = new RelayCommand(_ => UpdateStartTime(TimeSpan.FromSeconds(1)));
            DecrementStartTimeSecondsCommand = new RelayCommand(_ => UpdateStartTime(TimeSpan.FromSeconds(-1)));
            IncrementStartTimeMillisecondsCommand = new RelayCommand(_ => UpdateStartTime(TimeSpan.FromMilliseconds(GetMillisecondDelta())));
            DecrementStartTimeMillisecondsCommand = new RelayCommand(_ => UpdateStartTime(TimeSpan.FromMilliseconds(-GetMillisecondDelta())));

            IncrementDurationMinutesCommand = new RelayCommand(_ => UpdateDuration(TimeSpan.FromMinutes(1)));
            DecrementDurationMinutesCommand = new RelayCommand(_ => UpdateDuration(TimeSpan.FromMinutes(-1)));
            IncrementDurationSecondsCommand = new RelayCommand(_ => UpdateDuration(TimeSpan.FromSeconds(1)));
            DecrementDurationSecondsCommand = new RelayCommand(_ => UpdateDuration(TimeSpan.FromSeconds(-1)));
            IncrementDurationMillisecondsCommand = new RelayCommand(_ => UpdateDuration(TimeSpan.FromMilliseconds(GetMillisecondDelta())));
            DecrementDurationMillisecondsCommand = new RelayCommand(_ => UpdateDuration(TimeSpan.FromMilliseconds(-GetMillisecondDelta())));


            RecalculateTimes();
        }

        public MidiEditorViewModel GetParentViewModel() => _parentViewModel;

        private int GetMillisecondDelta()
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return 10;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return 100;
            return 1;
        }

        private void UpdateStartTime(TimeSpan delta)
        {
            var newTime = StartTime + delta;
            if (newTime < TimeSpan.Zero) newTime = TimeSpan.Zero;
            var newTicks = _parentViewModel.TimeToTicks(newTime);
            if (delta > TimeSpan.Zero && newTicks <= StartTicks)
            {
                newTicks = StartTicks + 1;
            }
            StartTicks = newTicks;
        }

        private void UpdateDuration(TimeSpan delta)
        {
            var newDuration = Duration + delta;
            if (newDuration < TimeSpan.FromMilliseconds(1))
            {
                newDuration = TimeSpan.FromMilliseconds(1);
            }
            var endTicks = _parentViewModel.TimeToTicks(_startTime + newDuration);
            var newDurationTicks = endTicks - StartTicks;
            if (delta > TimeSpan.Zero && newDurationTicks <= DurationTicks)
            {
                newDurationTicks = DurationTicks + 1;
            }
            if (newDurationTicks < 1) newDurationTicks = 1;
            DurationTicks = newDurationTicks;
        }

        public void RecalculateTimes(List<NAudioMidi.TempoEvent>? newTempoMap = null)
        {
            if (newTempoMap != null)
            {
                _tempoMap = newTempoMap;
            }
            _startTime = MidiProcessor.TicksToTimeSpan(StartTicks, _ticksPerQuarterNote, _tempoMap);
            _duration = MidiProcessor.TicksToTimeSpan(StartTicks + DurationTicks, _ticksPerQuarterNote, _tempoMap) - _startTime;
            OnPropertyChanged(nameof(StartTime));
            OnPropertyChanged(nameof(Duration));
            OnPropertyChanged(nameof(DurationInSeconds));
            OnPropertyChanged(nameof(StartTimeMinutes));
            OnPropertyChanged(nameof(StartTimeSeconds));
            OnPropertyChanged(nameof(StartTimeMilliseconds));
            OnPropertyChanged(nameof(DurationMinutes));
            OnPropertyChanged(nameof(DurationSeconds));
            OnPropertyChanged(nameof(DurationMilliseconds));
        }

        public void UpdateHorizontal()
        {
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Width));
        }

        public void UpdateVertical()
        {
            OnPropertyChanged(nameof(Y));
            OnPropertyChanged(nameof(Height));
        }

        private string GetNoteName(int noteNumber)
        {
            if (_parentViewModel.TuningSystem == Configuration.Models.TuningSystemType.TwentyFourToneEqualTemperament)
            {
                string[] noteNames = { "C", "C+", "C#", "C#+", "D", "D+", "D#", "D#+", "E", "E+", "F", "F+", "F#", "F#+", "G", "G+", "G#", "G#+", "A", "A+", "A#", "A#+", "B", "B+" };
                int octave = (noteNumber / 24) - 1;
                return $"{noteNames[noteNumber % 24]}{octave}";
            }
            else
            {
                string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
                int octave = (noteNumber / 12) - 1;
                return $"{noteNames[noteNumber % 12]}{octave}";
            }
        }

        public NAudioMidi.NoteOnEvent NoteOnEvent => _noteOnEvent;
        public string NoteName => GetNoteName(NoteNumber);
        public int NoteNumber
        {
            get => _noteOnEvent.NoteNumber;
            set
            {
                if (_noteOnEvent.NoteNumber != value)
                {
                    _parentViewModel.RequestNoteRedraw(this);
                    _noteOnEvent.NoteNumber = value;
                    if (_noteOnEvent.OffEvent != null)
                    {
                        _noteOnEvent.OffEvent.NoteNumber = value;
                    }
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Y));
                    OnPropertyChanged(nameof(NoteName));
                    _parentViewModel.RequestNoteRedraw(this);
                }
            }
        }
        public int Channel
        {
            get => _noteOnEvent.Channel;
            set
            {
                if (_noteOnEvent.Channel != value)
                {
                    _noteOnEvent.Channel = value;
                    if (_noteOnEvent.OffEvent != null)
                    {
                        _noteOnEvent.OffEvent.Channel = value;
                    }

                    if (_parentViewModel.IsColorizedByChannel)
                    {
                        Color = _parentViewModel.GetColorForChannel(value);
                    }
                    else
                    {
                        Color = MidiEditorSettings.Default.Note.NoteColor;
                    }

                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FillBrush));
                    _parentViewModel.RequestNoteRedraw(this);
                }
            }
        }
        public int Velocity
        {
            get => _noteOnEvent.Velocity;
            set
            {
                if (_noteOnEvent.Velocity != value)
                {
                    _noteOnEvent.Velocity = value;
                    OnPropertyChanged();
                }
            }
        }

        public long StartTicks
        {
            get => _noteOnEvent.AbsoluteTime;
            set
            {
                if (_noteOnEvent.AbsoluteTime != value)
                {
                    long oldEndTicks = _noteOnEvent.AbsoluteTime + _noteOnEvent.NoteLength;
                    _noteOnEvent.AbsoluteTime = value;
                    long newDurationTicks = oldEndTicks - value;
                    if (newDurationTicks < 0) newDurationTicks = 0;
                    _noteOnEvent.NoteLength = (int)newDurationTicks;

                    if (_noteOnEvent.OffEvent != null)
                    {
                        _noteOnEvent.OffEvent.AbsoluteTime = value + newDurationTicks;
                    }
                    RecalculateTimes();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StartTime));
                    OnPropertyChanged(nameof(X));
                    OnPropertyChanged(nameof(DurationTicks));
                }
            }
        }

        public long DurationTicks
        {
            get => _noteOnEvent.NoteLength;
            set
            {
                if (_noteOnEvent.NoteLength != (int)value)
                {
                    _noteOnEvent.NoteLength = (int)value;
                    if (_noteOnEvent.OffEvent != null)
                    {
                        _noteOnEvent.OffEvent.AbsoluteTime = _noteOnEvent.AbsoluteTime + value;
                    }
                    RecalculateTimes();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(Duration));
                    OnPropertyChanged(nameof(Width));
                }
            }
        }

        public void UpdateNote(long newStartTicks, long newDurationTicks)
        {
            _parentViewModel.RequestNoteRedraw(this);

            _noteOnEvent.AbsoluteTime = newStartTicks;
            _noteOnEvent.NoteLength = (int)newDurationTicks;
            if (_noteOnEvent.OffEvent != null)
            {
                _noteOnEvent.OffEvent.AbsoluteTime = newStartTicks + newDurationTicks;
            }
            RecalculateTimes();
            OnPropertyChanged(nameof(StartTicks));
            OnPropertyChanged(nameof(DurationTicks));
            OnPropertyChanged(nameof(StartTime));
            OnPropertyChanged(nameof(Duration));
            OnPropertyChanged(nameof(X));
            OnPropertyChanged(nameof(Width));

            _parentViewModel.RequestNoteRedraw(this);
        }

        public TimeSpan StartTime => _startTime;
        public TimeSpan Duration => _duration;
        public double DurationInSeconds => _duration.TotalSeconds;

        public int StartTimeMinutes
        {
            get => _startTime.Minutes;
            set
            {
                value = Math.Max(0, value);
                if (_startTime.Minutes != value)
                {
                    var newTime = new TimeSpan(0, _startTime.Hours, value, _startTime.Seconds, _startTime.Milliseconds);
                    StartTicks = _parentViewModel.TimeToTicks(newTime);
                }
            }
        }

        public int StartTimeSeconds
        {
            get => _startTime.Seconds;
            set
            {
                value = Math.Max(0, Math.Min(value, 59));
                if (_startTime.Seconds != value)
                {
                    var newTime = new TimeSpan(0, _startTime.Hours, _startTime.Minutes, value, _startTime.Milliseconds);
                    StartTicks = _parentViewModel.TimeToTicks(newTime);
                }
            }
        }

        public int StartTimeMilliseconds
        {
            get => _startTime.Milliseconds;
            set
            {
                value = Math.Max(0, Math.Min(value, 999));
                if (_startTime.Milliseconds != value)
                {
                    var newTime = new TimeSpan(0, _startTime.Hours, _startTime.Minutes, _startTime.Seconds, value);
                    StartTicks = _parentViewModel.TimeToTicks(newTime);
                }
            }
        }

        public int DurationMinutes
        {
            get => _duration.Minutes;
            set
            {
                value = Math.Max(0, value);
                if (_duration.Minutes != value)
                {
                    var newDuration = new TimeSpan(0, _duration.Hours, value, _duration.Seconds, _duration.Milliseconds);
                    var endTicks = _parentViewModel.TimeToTicks(_startTime + newDuration);
                    DurationTicks = endTicks - StartTicks;
                }
            }
        }

        public int DurationSeconds
        {
            get => _duration.Seconds;
            set
            {
                value = Math.Max(0, Math.Min(value, 59));
                if (_duration.Seconds != value)
                {
                    var newDuration = new TimeSpan(0, _duration.Hours, _duration.Minutes, value, _duration.Milliseconds);
                    var endTicks = _parentViewModel.TimeToTicks(_startTime + newDuration);
                    DurationTicks = endTicks - StartTicks;
                }
            }
        }

        public int DurationMilliseconds
        {
            get => _duration.Milliseconds;
            set
            {
                value = Math.Max(0, Math.Min(value, 999));
                if (_duration.Milliseconds != value)
                {
                    var newDuration = new TimeSpan(0, _duration.Hours, _duration.Minutes, _duration.Seconds, value);
                    var endTicks = _parentViewModel.TimeToTicks(_startTime + newDuration);
                    DurationTicks = endTicks - StartTicks;
                }
            }
        }


        public double X => StartTime.TotalSeconds * _parentViewModel.HorizontalZoom;
        public double Y => (_parentViewModel.MaxNoteNumber - NoteNumber - 1) * 20.0 * _parentViewModel.VerticalZoom / _parentViewModel.KeyYScale + (_centOffset / 100.0 * 20.0 * _parentViewModel.VerticalZoom / _parentViewModel.KeyYScale);
        public double Width => Math.Max(1.0, Duration.TotalSeconds * _parentViewModel.HorizontalZoom);
        public double Height => 20.0 * _parentViewModel.VerticalZoom / _parentViewModel.KeyYScale;
    }
}
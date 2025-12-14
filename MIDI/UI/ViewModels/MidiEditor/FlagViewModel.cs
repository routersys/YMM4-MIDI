using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MIDI.Configuration.Models;
using MIDI.UI.Commands;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class FlagViewModel : ViewModelBase
    {
        private string _name;
        public string Name
        {
            get => _name;
            set
            {
                if (SetField(ref _name, value))
                {
                    OnPropertyChanged(nameof(Y));
                }
            }
        }

        private TimeSpan _time;
        public TimeSpan Time
        {
            get => _time;
            set
            {
                if (SetField(ref _time, value))
                {
                    OnPropertyChanged(nameof(X));
                    OnPropertyChanged(nameof(Ticks));
                    OnPropertyChanged(nameof(TimeMinutes));
                    OnPropertyChanged(nameof(TimeSeconds));
                    OnPropertyChanged(nameof(TimeMilliseconds));
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
                    _parentViewModel.OnFlagSelectionChanged(this, value);
                }
            }
        }

        public double X => Time.TotalSeconds * _parentViewModel.HorizontalZoom;
        public double Y => 0;

        public long Ticks
        {
            get => _parentViewModel.TimeToTicks(Time);
            set
            {
                var maxTicks = _parentViewModel.TimeToTicks(_parentViewModel.MaxTime);
                var newTicks = Math.Max(0, Math.Min(value, maxTicks));
                var newTime = _parentViewModel.TicksToTime(newTicks);
                if (Time != newTime)
                {
                    Time = newTime;
                }
            }
        }

        public int TimeMinutes
        {
            get => _time.Minutes;
            set
            {
                if (_time.Minutes != value && value >= 0)
                {
                    var newTime = new TimeSpan(0, _time.Hours, value, _time.Seconds, _time.Milliseconds);
                    Ticks = _parentViewModel.TimeToTicks(newTime);
                }
            }
        }

        public int TimeSeconds
        {
            get => _time.Seconds;
            set
            {
                if (_time.Seconds != value && value >= 0 && value < 60)
                {
                    var newTime = new TimeSpan(0, _time.Hours, _time.Minutes, value, _time.Milliseconds);
                    Ticks = _parentViewModel.TimeToTicks(newTime);
                }
            }
        }

        public int TimeMilliseconds
        {
            get => _time.Milliseconds;
            set
            {
                value = Math.Max(0, Math.Min(value, 999));
                if (_time.Milliseconds != value)
                {
                    var newTime = new TimeSpan(0, _time.Hours, _time.Minutes, _time.Seconds, value);
                    Ticks = _parentViewModel.TimeToTicks(newTime);
                }
            }
        }

        public ICommand IncrementTimeMinutesCommand { get; }
        public ICommand DecrementTimeMinutesCommand { get; }
        public ICommand IncrementTimeSecondsCommand { get; }
        public ICommand DecrementTimeSecondsCommand { get; }
        public ICommand IncrementTimeMillisecondsCommand { get; }
        public ICommand DecrementTimeMillisecondsCommand { get; }

        private readonly MidiEditorViewModel _parentViewModel;

        public FlagViewModel(MidiEditorViewModel parentViewModel, TimeSpan time, string name)
        {
            _parentViewModel = parentViewModel;
            _time = time;
            _name = name;
            _parentViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MidiEditorViewModel.HorizontalZoom))
                {
                    OnPropertyChanged(nameof(X));
                }
            };

            OnPropertyChanged(nameof(Y));

            IncrementTimeMinutesCommand = new RelayCommand(_ => UpdateTime(TimeSpan.FromMinutes(1)));
            DecrementTimeMinutesCommand = new RelayCommand(_ => UpdateTime(TimeSpan.FromMinutes(-1)));
            IncrementTimeSecondsCommand = new RelayCommand(_ => UpdateTime(TimeSpan.FromSeconds(1)));
            DecrementTimeSecondsCommand = new RelayCommand(_ => UpdateTime(TimeSpan.FromSeconds(-1)));
            IncrementTimeMillisecondsCommand = new RelayCommand(_ => UpdateTime(TimeSpan.FromMilliseconds(GetMillisecondDelta())));
            DecrementTimeMillisecondsCommand = new RelayCommand(_ => UpdateTime(TimeSpan.FromMilliseconds(-GetMillisecondDelta())));
        }

        private int GetMillisecondDelta()
        {
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return 10;
            if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return 100;
            return 1;
        }

        private void UpdateTime(TimeSpan delta)
        {
            var newTime = Time + delta;
            if (newTime < TimeSpan.Zero) newTime = TimeSpan.Zero;
            var maxTime = _parentViewModel.MaxTime;
            if (newTime > maxTime) newTime = maxTime;

            Time = newTime;
        }
    }
}
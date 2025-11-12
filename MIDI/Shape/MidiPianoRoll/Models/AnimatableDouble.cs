using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace MIDI.Shape.MidiPianoRoll.Models
{
    public class AnimatableDouble : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public AnimationMode Mode
        {
            get => _mode;
            set
            {
                if (SetProperty(ref _mode, value))
                {
                    OnPropertyChanged(nameof(IsFixed));
                    OnPropertyChanged(nameof(IsAnimated));
                    OnPropertyChanged(nameof(IsLinear));
                    OnPropertyChanged(nameof(IsRandom));
                    OnPropertyChanged(nameof(IsRepeat));
                }
            }
        }
        private AnimationMode _mode = AnimationMode.Fixed;

        [JsonIgnore]
        public bool IsFixed => Mode == AnimationMode.Fixed;
        [JsonIgnore]
        public bool IsAnimated => Mode != AnimationMode.Fixed;
        [JsonIgnore]
        public bool IsLinear => Mode == AnimationMode.Linear;
        [JsonIgnore]
        public bool IsRandom => Mode == AnimationMode.Random;
        [JsonIgnore]
        public bool IsRepeat => Mode == AnimationMode.Repeat;

        public double Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
        private double _value;

        public double StartValue
        {
            get => _startValue;
            set => SetProperty(ref _startValue, value);
        }
        private double _startValue;

        public double EndValue
        {
            get => _endValue;
            set => SetProperty(ref _endValue, value);
        }
        private double _endValue;

        public double RepeatPeriod
        {
            get => _repeatPeriod;
            set => SetProperty(ref _repeatPeriod, value);
        }
        private double _repeatPeriod = 0.2;

        public double RandomPeriod
        {
            get => _randomPeriod;
            set => SetProperty(ref _randomPeriod, value);
        }
        private double _randomPeriod = 0.1;

        public AnimatableDouble(double value)
        {
            _value = value;
            _startValue = value;
            _endValue = value;
        }

        public double GetValue(double progress)
        {
            progress = Math.Max(0.0, Math.Min(1.0, progress));
            switch (Mode)
            {
                case AnimationMode.Fixed:
                    return Value;
                case AnimationMode.Linear:
                    return StartValue + (EndValue - StartValue) * progress;
                case AnimationMode.Repeat:
                    {
                        if (RepeatPeriod <= 0) return StartValue;
                        double phase = (progress / RepeatPeriod) % 2.0;
                        if (phase > 1.0) phase = 2.0 - phase;
                        return StartValue + (EndValue - StartValue) * phase;
                    }
                case AnimationMode.Random:
                    {
                        if (RandomPeriod <= 0) return StartValue;
                        int seedBase = (int)Math.Floor(progress / RandomPeriod);
                        Random r = new Random(seedBase);
                        return StartValue + r.NextDouble() * (EndValue - StartValue);
                    }
                default:
                    return Value;
            }
        }

        public void CopyFrom(AnimatableDouble source)
        {
            Mode = source.Mode;
            Value = source.Value;
            StartValue = source.StartValue;
            EndValue = source.EndValue;
            RepeatPeriod = source.RepeatPeriod;
            RandomPeriod = source.RandomPeriod;
        }

        public class SharedData
        {
            public AnimationMode Mode { get; set; }
            public double Value { get; set; }
            public double StartValue { get; set; }
            public double EndValue { get; set; }
            public double RepeatPeriod { get; set; }
            public double RandomPeriod { get; set; }

            public SharedData() { }

            public SharedData(AnimatableDouble p)
            {
                Mode = p.Mode;
                Value = p.Value;
                StartValue = p.StartValue;
                EndValue = p.EndValue;
                RepeatPeriod = p.RepeatPeriod;
                RandomPeriod = p.RandomPeriod;
            }

            public void Apply(AnimatableDouble p)
            {
                p.Mode = Mode;
                p.Value = Value;
                p.StartValue = StartValue;
                p.EndValue = EndValue;
                p.RepeatPeriod = RepeatPeriod;
                p.RandomPeriod = RandomPeriod;
            }
        }
    }
}
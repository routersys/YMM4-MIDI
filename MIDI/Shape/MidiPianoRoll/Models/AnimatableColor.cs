using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace MIDI.Shape.MidiPianoRoll.Models
{
    public class AnimatableColor : INotifyPropertyChanged
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

        public Color Value
        {
            get => _value;
            set => SetProperty(ref _value, value);
        }
        private Color _value;

        public Color StartValue
        {
            get => _startValue;
            set => SetProperty(ref _startValue, value);
        }
        private Color _startValue;

        public Color EndValue
        {
            get => _endValue;
            set => SetProperty(ref _endValue, value);
        }
        private Color _endValue;

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

        public AnimatableColor(Color color)
        {
            _value = color;
            _startValue = color;
            _endValue = color;
        }

        public Color GetValue(double progress)
        {
            progress = Math.Max(0.0, Math.Min(1.0, progress));
            switch (Mode)
            {
                case AnimationMode.Fixed:
                    return Value;
                case AnimationMode.Linear:
                    return Lerp(StartValue, EndValue, progress);
                case AnimationMode.Repeat:
                    {
                        if (RepeatPeriod <= 0) return StartValue;
                        double phase = (progress / RepeatPeriod) % 2.0;
                        if (phase > 1.0) phase = 2.0 - phase;
                        return Lerp(StartValue, EndValue, phase);
                    }
                case AnimationMode.Random:
                    {
                        if (RandomPeriod <= 0) return StartValue;
                        int seedBase = (int)Math.Floor(progress / RandomPeriod);
                        Random r = new Random(seedBase);
                        return Lerp(StartValue, EndValue, r.NextDouble());
                    }
                default:
                    return Value;
            }
        }

        private Color Lerp(Color start, Color end, double amount)
        {
            byte a = (byte)(start.A + (end.A - start.A) * amount);
            byte r = (byte)(start.R + (end.R - start.R) * amount);
            byte g = (byte)(start.G + (end.G - start.G) * amount);
            byte b = (byte)(start.B + (end.B - start.B) * amount);
            return Color.FromArgb(a, r, g, b);
        }

        public void CopyFrom(AnimatableColor source)
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
            public uint Value { get; set; }
            public uint StartValue { get; set; }
            public uint EndValue { get; set; }
            public double RepeatPeriod { get; set; }
            public double RandomPeriod { get; set; }

            public SharedData() { }

            public SharedData(AnimatableColor p)
            {
                Mode = p.Mode;
                Value = (uint)p.Value.A << 24 | (uint)p.Value.R << 16 | (uint)p.Value.G << 8 | (uint)p.Value.B;
                StartValue = (uint)p.StartValue.A << 24 | (uint)p.StartValue.R << 16 | (uint)p.StartValue.G << 8 | (uint)p.StartValue.B;
                EndValue = (uint)p.EndValue.A << 24 | (uint)p.EndValue.R << 16 | (uint)p.EndValue.G << 8 | (uint)p.EndValue.B;
                RepeatPeriod = p.RepeatPeriod;
                RandomPeriod = p.RandomPeriod;
            }

            public void Apply(AnimatableColor p)
            {
                p.Mode = Mode;
                p.Value = Color.FromArgb((byte)(Value >> 24), (byte)(Value >> 16), (byte)(Value >> 8), (byte)Value);
                p.StartValue = Color.FromArgb((byte)(StartValue >> 24), (byte)(StartValue >> 16), (byte)(StartValue >> 8), (byte)StartValue);
                p.EndValue = Color.FromArgb((byte)(EndValue >> 24), (byte)(EndValue >> 16), (byte)(EndValue >> 8), (byte)EndValue);
                p.RepeatPeriod = RepeatPeriod;
                p.RandomPeriod = RandomPeriod;
            }
        }
    }
}
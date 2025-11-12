using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public class InstrumentPreset : INotifyPropertyChanged, ICloneable
    {
        private string _name = string.Empty;
        public string Name { get => _name; set => SetField(ref _name, value); }
        private int _startProgram;
        public int StartProgram { get => _startProgram; set => SetField(ref _startProgram, value); }

        private int _endProgram;
        public int EndProgram { get => _endProgram; set => SetField(ref _endProgram, value); }

        private string _waveform = "Sine";
        public string Waveform { get => _waveform; set => SetField(ref _waveform, value); }

        private double _attack = 0.01;
        public double Attack { get => _attack; set => SetField(ref _attack, value); }

        private double _decay = 0.2;
        public double Decay { get => _decay; set => SetField(ref _decay, value); }

        private double _sustain = 0.7;
        public double Sustain { get => _sustain; set => SetField(ref _sustain, value); }

        private double _release = 0.5;
        public double Release { get => _release; set => SetField(ref _release, value); }

        private float _volume;
        public float Volume { get => _volume; set => SetField(ref _volume, value); }

        private FilterSettings _filter = new();
        public FilterSettings Filter { get => _filter; set => SetField(ref _filter, value); }

        public InstrumentPreset()
        {
            Filter.PropertyChanged += OnNestedPropertyChanged;
        }

        public object Clone()
        {
            return new InstrumentPreset
            {
                Name = this.Name,
                StartProgram = this.StartProgram,
                EndProgram = this.EndProgram,
                Waveform = this.Waveform,
                Attack = this.Attack,
                Decay = this.Decay,
                Sustain = this.Sustain,
                Release = this.Release,
                Volume = this.Volume,
                Filter = (FilterSettings)this.Filter.Clone()
            };
        }

        private void OnNestedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Filter)));
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

    public class CustomInstrument : INotifyPropertyChanged, ICloneable
    {
        private int _program;
        public int Program { get => _program; set => SetField(ref _program, value); }

        private string _waveform = string.Empty;
        public string Waveform { get => _waveform; set => SetField(ref _waveform, value); }

        private string _userWavetableFile = string.Empty;
        public string UserWavetableFile { get => _userWavetableFile; set => SetField(ref _userWavetableFile, value); }

        private ObservableCollection<EnvelopePoint> _amplitudeEnvelope = new ObservableCollection<EnvelopePoint>
        {
            new EnvelopePoint(0.0, 0.0), new EnvelopePoint(0.01, 1.0), new EnvelopePoint(0.2, 0.7)
        };
        public ObservableCollection<EnvelopePoint> AmplitudeEnvelope { get => _amplitudeEnvelope; set => SetField(ref _amplitudeEnvelope, value); }

        private double _release;
        public double Release { get => _release; set => SetField(ref _release, value); }

        private float _volume;
        public float Volume { get => _volume; set => SetField(ref _volume, value); }

        private FilterSettings _filter = new();
        public FilterSettings Filter { get => _filter; set => SetField(ref _filter, value); }

        private LfoSettings _pitchLfo = new();
        public LfoSettings PitchLfo { get => _pitchLfo; set => SetField(ref _pitchLfo, value); }

        private LfoSettings _amplitudeLfo = new();
        public LfoSettings AmplitudeLfo { get => _amplitudeLfo; set => SetField(ref _amplitudeLfo, value); }

        public CustomInstrument()
        {
            Filter.PropertyChanged += OnNestedPropertyChanged;
        }

        public object Clone()
        {
            return new CustomInstrument
            {
                Program = this.Program,
                Waveform = this.Waveform,
                UserWavetableFile = this.UserWavetableFile,
                AmplitudeEnvelope = new ObservableCollection<EnvelopePoint>(this.AmplitudeEnvelope.Select(e => (EnvelopePoint)e.Clone())),
                Release = this.Release,
                Volume = this.Volume,
                Filter = (FilterSettings)this.Filter.Clone(),
                PitchLfo = (LfoSettings)this.PitchLfo.Clone(),
                AmplitudeLfo = (LfoSettings)this.AmplitudeLfo.Clone()
            };
        }

        private void OnNestedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Filter)));
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
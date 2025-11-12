using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public class FilterSettings : INotifyPropertyChanged, ICloneable
    {
        private string _type = "None";
        public string Type { get => _type; set => SetField(ref _type, value); }

        private double _cutoff = 22050;
        public double Cutoff { get => _cutoff; set => SetField(ref _cutoff, value); }

        private double _resonance = 1.0;
        public double Resonance { get => _resonance; set => SetField(ref _resonance, value); }

        private LfoSettings _lfo = new();
        public LfoSettings Lfo { get => _lfo; set => SetField(ref _lfo, value); }

        public object Clone()
        {
            return new FilterSettings
            {
                Type = this.Type,
                Cutoff = this.Cutoff,
                Resonance = this.Resonance,
                Lfo = (LfoSettings)this.Lfo.Clone()
            };
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
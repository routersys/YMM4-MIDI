using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public class AudioSettings : INotifyPropertyChanged
    {
        private int _sampleRate = 44100;
        public int SampleRate { get => _sampleRate; set => SetField(ref _sampleRate, value); }

        private float _masterVolume = 1.0f;
        public float MasterVolume { get => _masterVolume; set => SetField(ref _masterVolume, value); }

        private bool _enableNormalization = true;
        public bool EnableNormalization { get => _enableNormalization; set => SetField(ref _enableNormalization, value); }

        private float _normalizationThreshold = 1.0f;
        public float NormalizationThreshold { get => _normalizationThreshold; set => SetField(ref _normalizationThreshold, value); }

        private float _normalizationLevel = 0.95f;
        public float NormalizationLevel { get => _normalizationLevel; set => SetField(ref _normalizationLevel, value); }

        private bool _enableGlobalFadeOut;
        public bool EnableGlobalFadeOut { get => _enableGlobalFadeOut; set => SetField(ref _enableGlobalFadeOut, value); }

        private double _globalFadeOutSeconds = 0.05;
        public double GlobalFadeOutSeconds { get => _globalFadeOutSeconds; set => SetField(ref _globalFadeOutSeconds, value); }

        public void CopyFrom(AudioSettings source)
        {
            SampleRate = source.SampleRate;
            MasterVolume = source.MasterVolume;
            EnableNormalization = source.EnableNormalization;
            NormalizationThreshold = source.NormalizationThreshold;
            NormalizationLevel = source.NormalizationLevel;
            EnableGlobalFadeOut = source.EnableGlobalFadeOut;
            GlobalFadeOutSeconds = source.GlobalFadeOutSeconds;
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
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public class SynthesisSettings : INotifyPropertyChanged
    {
        private string _defaultWaveform = "Sine";
        public string DefaultWaveform { get => _defaultWaveform; set => SetField(ref _defaultWaveform, value); }

        private string _wavetableDirectory = "Wavetables";
        public string WavetableDirectory { get => _wavetableDirectory; set => SetField(ref _wavetableDirectory, value); }

        private double _a4Frequency = 440.0;
        public double A4Frequency { get => _a4Frequency; set => SetField(ref _a4Frequency, value); }

        private double _minFrequency = 20.0;
        public double MinFrequency { get => _minFrequency; set => SetField(ref _minFrequency, value); }

        private double _maxFrequency = 20000.0;
        public double MaxFrequency { get => _maxFrequency; set => SetField(ref _maxFrequency, value); }

        private double _envelopeScale = 1.0;
        public double EnvelopeScale { get => _envelopeScale; set => SetField(ref _envelopeScale, value); }

        private double _defaultAttack = 0.005;
        public double DefaultAttack { get => _defaultAttack; set => SetField(ref _defaultAttack, value); }

        private double _defaultDecay = 0.2;
        public double DefaultDecay { get => _defaultDecay; set => SetField(ref _defaultDecay, value); }

        private double _defaultSustain = 0.7;
        public double DefaultSustain { get => _defaultSustain; set => SetField(ref _defaultSustain, value); }

        private double _defaultRelease = 0.01;
        public double DefaultRelease { get => _defaultRelease; set => SetField(ref _defaultRelease, value); }

        private double _fmModulatorFrequency = 5.0;
        public double FmModulatorFrequency { get => _fmModulatorFrequency; set => SetField(ref _fmModulatorFrequency, value); }

        private double _fmModulationIndex = 1.0;
        public double FmModulationIndex { get => _fmModulationIndex; set => SetField(ref _fmModulationIndex, value); }

        private bool _enableAntiPop = true;
        public bool EnableAntiPop { get => _enableAntiPop; set => SetField(ref _enableAntiPop, value); }

        private double _antiPopAttackSeconds = 0.002;
        public double AntiPopAttackSeconds { get => _antiPopAttackSeconds; set => SetField(ref _antiPopAttackSeconds, value); }

        private double _antiPopReleaseSeconds = 0.01;
        public double AntiPopReleaseSeconds { get => _antiPopReleaseSeconds; set => SetField(ref _antiPopReleaseSeconds, value); }

        private bool _enableNoteCrossfade = true;
        public bool EnableNoteCrossfade { get => _enableNoteCrossfade; set => SetField(ref _enableNoteCrossfade, value); }

        private double _noteCrossfadeDuration = 0.01;
        public double NoteCrossfadeDuration { get => _noteCrossfadeDuration; set => SetField(ref _noteCrossfadeDuration, value); }

        private bool _enableBandlimitedSynthesis = true;
        public bool EnableBandlimitedSynthesis { get => _enableBandlimitedSynthesis; set => SetField(ref _enableBandlimitedSynthesis, value); }

        public void CopyFrom(SynthesisSettings source)
        {
            DefaultWaveform = source.DefaultWaveform;
            WavetableDirectory = source.WavetableDirectory;
            A4Frequency = source.A4Frequency;
            MinFrequency = source.MinFrequency;
            MaxFrequency = source.MaxFrequency;
            EnvelopeScale = source.EnvelopeScale;
            DefaultAttack = source.DefaultAttack;
            DefaultDecay = source.DefaultDecay;
            DefaultSustain = source.DefaultSustain;
            DefaultRelease = source.DefaultRelease;
            FmModulatorFrequency = source.FmModulatorFrequency;
            FmModulationIndex = source.FmModulationIndex;
            EnableAntiPop = source.EnableAntiPop;
            AntiPopAttackSeconds = source.AntiPopAttackSeconds;
            AntiPopReleaseSeconds = source.AntiPopReleaseSeconds;
            EnableNoteCrossfade = source.EnableNoteCrossfade;
            NoteCrossfadeDuration = source.NoteCrossfadeDuration;
            EnableBandlimitedSynthesis = source.EnableBandlimitedSynthesis;
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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public class SoundFontRule : INotifyPropertyChanged, System.ICloneable
    {
        private string _soundFontFile = string.Empty;
        public string SoundFontFile { get => _soundFontFile; set => SetField(ref _soundFontFile, value); }

        private double? _minDurationSeconds;
        public double? MinDurationSeconds { get => _minDurationSeconds; set => SetField(ref _minDurationSeconds, value); }

        private double? _maxDurationSeconds;
        public double? MaxDurationSeconds { get => _maxDurationSeconds; set => SetField(ref _maxDurationSeconds, value); }

        private int? _minTrackCount;
        public int? MinTrackCount { get => _minTrackCount; set => SetField(ref _minTrackCount, value); }

        private int? _maxTrackCount;
        public int? MaxTrackCount { get => _maxTrackCount; set => SetField(ref _maxTrackCount, value); }

        private ObservableCollection<int> _requiredPrograms = new();
        public ObservableCollection<int> RequiredPrograms { get => _requiredPrograms; set => SetField(ref _requiredPrograms, value); }

        public object Clone()
        {
            return new SoundFontRule
            {
                SoundFontFile = this.SoundFontFile,
                MinDurationSeconds = this.MinDurationSeconds,
                MaxDurationSeconds = this.MaxDurationSeconds,
                MinTrackCount = this.MinTrackCount,
                MaxTrackCount = this.MaxTrackCount,
                RequiredPrograms = new ObservableCollection<int>(this.RequiredPrograms)
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

    public class SoundFontLayer : INotifyPropertyChanged, System.ICloneable
    {
        private string _soundFontFile = string.Empty;
        public string SoundFontFile { get => _soundFontFile; set => SetField(ref _soundFontFile, value); }

        public object Clone() => new SoundFontLayer { SoundFontFile = this.SoundFontFile };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class SfzProgramMap : INotifyPropertyChanged, System.ICloneable
    {
        private int _program;
        public int Program { get => _program; set => SetField(ref _program, value); }

        private string _filePath = string.Empty;
        public string FilePath { get => _filePath; set => SetField(ref _filePath, value); }

        public object Clone() => new SfzProgramMap { Program = this.Program, FilePath = this.FilePath };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }

    public class AlgorithmicReverbSettings : INotifyPropertyChanged
    {
        private bool _enable = false;
        public bool Enable { get => _enable; set => SetField(ref _enable, value); }

        private float _roomSize = 0.7f;
        public float RoomSize { get => _roomSize; set => SetField(ref _roomSize, value); }

        private float _damping = 0.5f;
        public float Damping { get => _damping; set => SetField(ref _damping, value); }

        private float _wetLevel = 0.3f;
        public float WetLevel { get => _wetLevel; set => SetField(ref _wetLevel, value); }

        private float _dryLevel = 0.7f;
        public float DryLevel { get => _dryLevel; set => SetField(ref _dryLevel, value); }

        private float _width = 0.5f;
        public float Width { get => _width; set => SetField(ref _width, value); }

        public void CopyFrom(AlgorithmicReverbSettings source)
        {
            Enable = source.Enable;
            RoomSize = source.RoomSize;
            Damping = source.Damping;
            WetLevel = source.WetLevel;
            DryLevel = source.DryLevel;
            Width = source.Width;
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

    public class DistortionSettings : INotifyPropertyChanged
    {
        private bool _enable = false;
        public bool Enable { get => _enable; set => SetField(ref _enable, value); }

        private DistortionType _type = DistortionType.SoftClip;
        public DistortionType Type { get => _type; set => SetField(ref _type, value); }

        private float _drive = 0.5f;
        public float Drive { get => _drive; set => SetField(ref _drive, value); }

        private float _mix = 0.5f;
        public float Mix { get => _mix; set => SetField(ref _mix, value); }

        public void CopyFrom(DistortionSettings source)
        {
            Enable = source.Enable;
            Type = source.Type;
            Drive = source.Drive;
            Mix = source.Mix;
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

    public class BitCrusherSettings : INotifyPropertyChanged
    {
        private bool _enable = false;
        public bool Enable { get => _enable; set => SetField(ref _enable, value); }

        private int _bitDepth = 16;
        public int BitDepth { get => _bitDepth; set => SetField(ref _bitDepth, value); }

        private float _rateReduction = 1.0f;
        public float RateReduction { get => _rateReduction; set => SetField(ref _rateReduction, value); }

        public void CopyFrom(BitCrusherSettings source)
        {
            Enable = source.Enable;
            BitDepth = source.BitDepth;
            RateReduction = source.RateReduction;
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


    public class EqualizerSettings : INotifyPropertyChanged
    {
        private float _bassGain = 1.0f;
        public float BassGain { get => _bassGain; set => SetField(ref _bassGain, value); }

        private float _midGain = 1.0f;
        public float MidGain { get => _midGain; set => SetField(ref _midGain, value); }

        private float _trebleGain = 1.0f;
        public float TrebleGain { get => _trebleGain; set => SetField(ref _trebleGain, value); }

        public void CopyFrom(EqualizerSettings source)
        {
            BassGain = source.BassGain;
            MidGain = source.MidGain;
            TrebleGain = source.TrebleGain;
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
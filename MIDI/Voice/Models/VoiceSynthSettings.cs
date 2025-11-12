using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using MIDI.Configuration.Models;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;

namespace MIDI.Voice.Models
{
    public enum ModelType
    {
        SoundFont,
        InternalSynth,
        UTAU
    }

    public class InternalSynthModel : INotifyPropertyChanged, System.ICloneable
    {
        private string _defaultWaveform = "Sine";
        public string DefaultWaveform { get => _defaultWaveform; set => SetField(ref _defaultWaveform, value); }

        private double _attack = 0.01;
        public double Attack { get => _attack; set => SetField(ref _attack, value); }

        private double _decay = 0.1;
        public double Decay { get => _decay; set => SetField(ref _decay, value); }

        private double _sustain = 0.8;
        public double Sustain { get => _sustain; set => SetField(ref _sustain, value); }

        private double _release = 0.2;
        public double Release { get => _release; set => SetField(ref _release, value); }

        private bool _enableBandlimited = true;
        public bool EnableBandlimited { get => _enableBandlimited; set => SetField(ref _enableBandlimited, value); }

        public void CopyFrom(InternalSynthModel source)
        {
            DefaultWaveform = source.DefaultWaveform;
            Attack = source.Attack;
            Decay = source.Decay;
            Sustain = source.Sustain;
            Release = source.Release;
            EnableBandlimited = source.EnableBandlimited;
        }

        public object Clone()
        {
            var clone = new InternalSynthModel();
            clone.CopyFrom(this);
            return clone;
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
            return true;
        }
    }

    public class VoiceModel : INotifyPropertyChanged, System.ICloneable
    {
        private string _name = "Default";
        public string Name { get => _name; set => SetField(ref _name, value); }

        private ModelType _modelType = ModelType.InternalSynth;
        public ModelType ModelType { get => _modelType; set => SetField(ref _modelType, value); }

        private ObservableCollection<SoundFontLayer> _layers = new();
        public ObservableCollection<SoundFontLayer> Layers { get => _layers; set => SetField(ref _layers, value); }

        private InternalSynthModel? _internalSynthSettings = new InternalSynthModel();
        public InternalSynthModel? InternalSynthSettings { get => _internalSynthSettings; set => SetField(ref _internalSynthSettings, value); }

        private string? _utauVoicePath;
        public string? UtauVoicePath { get => _utauVoicePath; set => SetField(ref _utauVoicePath, value); }


        public VoiceModel()
        {
            Layers.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Layers));
            if (InternalSynthSettings != null)
            {
                InternalSynthSettings.PropertyChanged += (s, e) => OnPropertyChanged(nameof(InternalSynthSettings));
            }
        }

        public void CopyFrom(VoiceModel source)
        {
            Name = source.Name;
            ModelType = source.ModelType;
            Layers = new ObservableCollection<SoundFontLayer>(source.Layers.Select(l => (SoundFontLayer)l.Clone()));
            UtauVoicePath = source.UtauVoicePath;

            if (source.InternalSynthSettings != null)
            {
                if (InternalSynthSettings == null) InternalSynthSettings = new InternalSynthModel();
                InternalSynthSettings.CopyFrom(source.InternalSynthSettings);
            }
            else
            {
                InternalSynthSettings = null;
            }
        }

        public object Clone()
        {
            var clone = new VoiceModel
            {
                Name = this.Name,
                ModelType = this.ModelType,
                Layers = new ObservableCollection<SoundFontLayer>(this.Layers.Select(l => (SoundFontLayer)l.Clone())),
                InternalSynthSettings = this.InternalSynthSettings != null ? (InternalSynthModel)this.InternalSynthSettings.Clone() : null,
                UtauVoicePath = this.UtauVoicePath
            };
            return clone;
        }


        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;

            if (propertyName == nameof(InternalSynthSettings) && value is InternalSynthModel newSettings)
            {
                if (field is InternalSynthModel oldSettings)
                {
                    oldSettings.PropertyChanged -= InternalSynthSettings_PropertyChanged;
                }
                newSettings.PropertyChanged += InternalSynthSettings_PropertyChanged;
            }


            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
            return true;
        }

        private void InternalSynthSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(nameof(InternalSynthSettings));
        }


        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
        }
    }

    public class VoiceSynthSettings : INotifyPropertyChanged
    {
        private int _sampleRate = 44100;
        public int SampleRate { get => _sampleRate; set => SetField(ref _sampleRate, value); }

        private string _currentModelName = "DefaultSynth";
        public string CurrentModelName { get => _currentModelName; set => SetField(ref _currentModelName, value); }

        private ObservableCollection<VoiceModel> _voiceModels = new();
        public ObservableCollection<VoiceModel> VoiceModels { get => _voiceModels; set => SetField(ref _voiceModels, value); }

        private ObservableCollection<string> _utauVoiceBaseFolders = new();
        public ObservableCollection<string> UtauVoiceBaseFolders { get => _utauVoiceBaseFolders; set => SetField(ref _utauVoiceBaseFolders, value); }

        [JsonIgnore]
        private ObservableCollection<SoundFontLayer> _soundFontLayers = new();
        [JsonIgnore]
        public ObservableCollection<SoundFontLayer> SoundFontLayers { get => _soundFontLayers; set => SetField(ref _soundFontLayers, value); }


        public VoiceSynthSettings()
        {
            VoiceModels.CollectionChanged += (s, e) => OnPropertyChanged(nameof(VoiceModels));
            SoundFontLayers.CollectionChanged += (s, e) => OnPropertyChanged(nameof(SoundFontLayers));
            UtauVoiceBaseFolders.CollectionChanged += (s, e) => OnPropertyChanged(nameof(UtauVoiceBaseFolders));
        }

        public void CopyFrom(VoiceSynthSettings source)
        {
            SampleRate = source.SampleRate;
            CurrentModelName = source.CurrentModelName;
            VoiceModels = new ObservableCollection<VoiceModel>(source.VoiceModels.Select(m => (VoiceModel)m.Clone()));
            UtauVoiceBaseFolders = new ObservableCollection<string>(source.UtauVoiceBaseFolders);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
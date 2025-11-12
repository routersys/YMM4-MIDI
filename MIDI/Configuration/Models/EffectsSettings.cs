using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public class EffectsSettings : INotifyPropertyChanged
    {
        private bool _enableEffects = true;
        public bool EnableEffects { get => _enableEffects; set => SetField(ref _enableEffects, value); }

        private bool _enableCompression;
        public bool EnableCompression { get => _enableCompression; set => SetField(ref _enableCompression, value); }

        private float _compressionThreshold = 0.8f;
        public float CompressionThreshold { get => _compressionThreshold; set => SetField(ref _compressionThreshold, value); }

        private float _compressionRatio = 4.0f;
        public float CompressionRatio { get => _compressionRatio; set => SetField(ref _compressionRatio, value); }

        private float _compressionAttack = 0.003f;
        public float CompressionAttack { get => _compressionAttack; set => SetField(ref _compressionAttack, value); }

        private float _compressionRelease = 0.1f;
        public float CompressionRelease { get => _compressionRelease; set => SetField(ref _compressionRelease, value); }

        private AlgorithmicReverbSettings _algorithmicReverb = new();
        public AlgorithmicReverbSettings AlgorithmicReverb { get => _algorithmicReverb; set => SetField(ref _algorithmicReverb, value); }

        private bool _enableChorus;
        public bool EnableChorus { get => _enableChorus; set => SetField(ref _enableChorus, value); }

        private float _chorusDelay = 0.02f;
        public float ChorusDelay { get => _chorusDelay; set => SetField(ref _chorusDelay, value); }

        private float _chorusDepth = 0.005f;
        public float ChorusDepth { get => _chorusDepth; set => SetField(ref _chorusDepth, value); }

        private float _chorusRate = 1.5f;
        public float ChorusRate { get => _chorusRate; set => SetField(ref _chorusRate, value); }

        private float _chorusStrength = 0.3f;
        public float ChorusStrength { get => _chorusStrength; set => SetField(ref _chorusStrength, value); }

        private bool _enableEqualizer;
        public bool EnableEqualizer { get => _enableEqualizer; set => SetField(ref _enableEqualizer, value); }

        private EqualizerSettings _eq = new();
        public EqualizerSettings EQ { get => _eq; set => SetField(ref _eq, value); }

        private bool _enableConvolutionReverb;
        public bool EnableConvolutionReverb { get => _enableConvolutionReverb; set => SetField(ref _enableConvolutionReverb, value); }

        private string _impulseResponseFilePath = string.Empty;
        public string ImpulseResponseFilePath { get => _impulseResponseFilePath; set => SetField(ref _impulseResponseFilePath, value); }

        private double _maxImpulseResponseDurationSeconds = 5.0;
        public double MaxImpulseResponseDurationSeconds { get => _maxImpulseResponseDurationSeconds; set => SetField(ref _maxImpulseResponseDurationSeconds, value); }

        private bool _enablePhaser;
        public bool EnablePhaser { get => _enablePhaser; set => SetField(ref _enablePhaser, value); }

        private float _phaserRate = 0.5f;
        public float PhaserRate { get => _phaserRate; set => SetField(ref _phaserRate, value); }

        private int _phaserStages = 4;
        public int PhaserStages { get => _phaserStages; set => SetField(ref _phaserStages, value); }

        private float _phaserFeedback = 0.5f;
        public float PhaserFeedback { get => _phaserFeedback; set => SetField(ref _phaserFeedback, value); }

        private bool _enableFlanger;
        public bool EnableFlanger { get => _enableFlanger; set => SetField(ref _enableFlanger, value); }

        private float _flangerDelay = 0.005f;
        public float FlangerDelay { get => _flangerDelay; set => SetField(ref _flangerDelay, value); }

        private float _flangerRate = 0.1f;
        public float FlangerRate { get => _flangerRate; set => SetField(ref _flangerRate, value); }

        private float _flangerDepth = 0.7f;
        public float FlangerDepth { get => _flangerDepth; set => SetField(ref _flangerDepth, value); }

        private bool _enableLimiter;
        public bool EnableLimiter { get => _enableLimiter; set => SetField(ref _enableLimiter, value); }

        private float _limiterThreshold = 0.95f;
        public float LimiterThreshold { get => _limiterThreshold; set => SetField(ref _limiterThreshold, value); }

        private bool _enableDCOffsetRemoval = true;
        public bool EnableDCOffsetRemoval { get => _enableDCOffsetRemoval; set => SetField(ref _enableDCOffsetRemoval, value); }

        private bool _enablePingPongDelay;
        public bool EnablePingPongDelay { get => _enablePingPongDelay; set => SetField(ref _enablePingPongDelay, value); }

        private float _delayTime = 0.5f;
        public float DelayTime { get => _delayTime; set => SetField(ref _delayTime, value); }

        private float _feedback = 0.5f;
        public float Feedback { get => _feedback; set => SetField(ref _feedback, value); }

        private float _wetDryMix = 0.5f;
        public float WetDryMix { get => _wetDryMix; set => SetField(ref _wetDryMix, value); }

        private DistortionSettings _distortion = new();
        public DistortionSettings Distortion { get => _distortion; set => SetField(ref _distortion, value); }

        private BitCrusherSettings _bitCrusher = new();
        public BitCrusherSettings BitCrusher { get => _bitCrusher; set => SetField(ref _bitCrusher, value); }

        public void CopyFrom(EffectsSettings source)
        {
            EnableEffects = source.EnableEffects;
            EnableCompression = source.EnableCompression;
            CompressionThreshold = source.CompressionThreshold;
            CompressionRatio = source.CompressionRatio;
            CompressionAttack = source.CompressionAttack;
            CompressionRelease = source.CompressionRelease;
            AlgorithmicReverb.CopyFrom(source.AlgorithmicReverb);
            EnableChorus = source.EnableChorus;
            ChorusDelay = source.ChorusDelay;
            ChorusDepth = source.ChorusDepth;
            ChorusRate = source.ChorusRate;
            ChorusStrength = source.ChorusStrength;
            EnableEqualizer = source.EnableEqualizer;
            EQ.CopyFrom(source.EQ);
            EnableConvolutionReverb = source.EnableConvolutionReverb;
            ImpulseResponseFilePath = source.ImpulseResponseFilePath;
            MaxImpulseResponseDurationSeconds = source.MaxImpulseResponseDurationSeconds;
            EnablePhaser = source.EnablePhaser;
            PhaserRate = source.PhaserRate;
            PhaserStages = source.PhaserStages;
            PhaserFeedback = source.PhaserFeedback;
            EnableFlanger = source.EnableFlanger;
            FlangerDelay = source.FlangerDelay;
            FlangerRate = source.FlangerRate;
            FlangerDepth = source.FlangerDepth;
            EnableLimiter = source.EnableLimiter;
            LimiterThreshold = source.LimiterThreshold;
            EnableDCOffsetRemoval = source.EnableDCOffsetRemoval;
            EnablePingPongDelay = source.EnablePingPongDelay;
            DelayTime = source.DelayTime;
            Feedback = source.Feedback;
            WetDryMix = source.WetDryMix;
            Distortion.CopyFrom(source.Distortion);
            BitCrusher.CopyFrom(source.BitCrusher);
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
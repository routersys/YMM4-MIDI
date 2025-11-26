using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public enum RenderingMode
    {
        [Description("CPU (高品質)")]
        HighQualityCPU,
        [Description("GPU (高品質)")]
        HighQualityGPU,
        [Description("CPU (リアルタイム)")]
        RealtimeCPU,
        [Description("GPU (リアルタイム)")]
        RealtimeGPU,
        [Description("CPU (プログレッシブ)")]
        ProgressiveHighQualityCPU,
        [Description("GPU (プログレッシブ)")]
        ProgressiveHighQualityGPU
    }

    public class PerformanceSettings : INotifyPropertyChanged
    {
        private bool _initializeGpuOnStartup = true;
        public bool InitializeGpuOnStartup { get => _initializeGpuOnStartup; set => SetField(ref _initializeGpuOnStartup, value); }

        private int _bufferSize = 1024;
        public int BufferSize { get => _bufferSize; set => SetField(ref _bufferSize, value); }

        private bool _enableParallelProcessing = true;
        public bool EnableParallelProcessing { get => _enableParallelProcessing; set => SetField(ref _enableParallelProcessing, value); }

        private int _maxThreads = Environment.ProcessorCount;
        public int MaxThreads { get => _maxThreads; set => SetField(ref _maxThreads, value); }

        private int _maxPolyphony = 256;
        public int MaxPolyphony { get => _maxPolyphony; set => SetField(ref _maxPolyphony, value); }

        private double _initialSyncDurationSeconds = 15.0;
        public double InitialSyncDurationSeconds { get => _initialSyncDurationSeconds; set => SetField(ref _initialSyncDurationSeconds, value); }

        private RenderingMode _renderingMode = RenderingMode.ProgressiveHighQualityCPU;
        public RenderingMode RenderingMode { get => _renderingMode; set => SetField(ref _renderingMode, value); }

        private GpuSettings _gpu = new();
        public GpuSettings GPU { get => _gpu; set => SetField(ref _gpu, value); }

        private DistributedProcessingSettings _distributed = new();
        public DistributedProcessingSettings Distributed { get => _distributed; set => SetField(ref _distributed, value); }

        public PerformanceSettings()
        {
            GPU.PropertyChanged += OnNestedPropertyChanged;
            Distributed.PropertyChanged += OnNestedPropertyChanged;
        }

        public void CopyFrom(PerformanceSettings source)
        {
            InitializeGpuOnStartup = source.InitializeGpuOnStartup;
            BufferSize = source.BufferSize;
            EnableParallelProcessing = source.EnableParallelProcessing;
            MaxThreads = source.MaxThreads;
            MaxPolyphony = source.MaxPolyphony;
            InitialSyncDurationSeconds = source.InitialSyncDurationSeconds;
            RenderingMode = source.RenderingMode;
            GPU.CopyFrom(source.GPU);
            Distributed.CopyFrom(source.Distributed);
        }

        private void OnNestedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(e.PropertyName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
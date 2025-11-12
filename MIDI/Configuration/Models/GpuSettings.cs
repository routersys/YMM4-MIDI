using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public class GpuSettings : INotifyPropertyChanged
    {
        private bool _enableGpuSynthesis;
        public bool EnableGpuSynthesis { get => _enableGpuSynthesis; set => SetField(ref _enableGpuSynthesis, value); }

        private bool _enableGpuEqualizer;
        public bool EnableGpuEqualizer { get => _enableGpuEqualizer; set => SetField(ref _enableGpuEqualizer, value); }

        private bool _enableGpuEffectsChain;
        public bool EnableGpuEffectsChain { get => _enableGpuEffectsChain; set => SetField(ref _enableGpuEffectsChain, value); }

        private bool _enableGpuConvolutionReverb;
        public bool EnableGpuConvolutionReverb { get => _enableGpuConvolutionReverb; set => SetField(ref _enableGpuConvolutionReverb, value); }

        public void CopyFrom(GpuSettings source)
        {
            EnableGpuSynthesis = source.EnableGpuSynthesis;
            EnableGpuEqualizer = source.EnableGpuEqualizer;
            EnableGpuEffectsChain = source.EnableGpuEffectsChain;
            EnableGpuConvolutionReverb = source.EnableGpuConvolutionReverb;
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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.FileWriter
{
    public enum QuantizeDuration
    {
        None,
        Whole,
        Half,
        Quarter,
        Eighth,
        Sixteenth,
        ThirtySecond
    }

    public enum PitchDetectionAlgorithm
    {
        SimpleFFT,
        Yin
    }

    public class MidiFileWriterConfigViewModel : INotifyPropertyChanged
    {
        private QuantizeDuration _quantizeValue = QuantizeDuration.Sixteenth;
        public QuantizeDuration QuantizeValue
        {
            get => _quantizeValue;
            set => SetField(ref _quantizeValue, value);
        }

        private PitchDetectionAlgorithm _pitchAlgorithm = PitchDetectionAlgorithm.SimpleFFT;
        public PitchDetectionAlgorithm PitchAlgorithm
        {
            get => _pitchAlgorithm;
            set => SetField(ref _pitchAlgorithm, value);
        }

        private double _pitchSensitivity = 0.5;
        public double PitchSensitivity
        {
            get => _pitchSensitivity;
            set
            {
                double clampedValue = Math.Clamp(value, 0.0, 1.0);
                if (SetField(ref _pitchSensitivity, clampedValue))
                {
                    if (Math.Abs(value - clampedValue) > 0.001)
                    {
                        OnPropertyChanged(nameof(PitchSensitivity));
                    }
                }
            }
        }

        private int _fftSize = 4096;
        public int FftSize
        {
            get => _fftSize;
            set
            {
                if (!IsValidFftSize(value))
                {
                    value = 4096;
                }
                SetField(ref _fftSize, value);
            }
        }

        private double _minNoteDurationMs = 50;
        public double MinNoteDurationMs
        {
            get => _minNoteDurationMs;
            set
            {
                double clampedValue = Math.Clamp(value, 10, 1000);
                if (SetField(ref _minNoteDurationMs, clampedValue))
                {
                    if (Math.Abs(value - clampedValue) > 0.1)
                    {
                        OnPropertyChanged(nameof(MinNoteDurationMs));
                    }
                }
            }
        }

        private double _velocitySensitivity = 0.5;
        public double VelocitySensitivity
        {
            get => _velocitySensitivity;
            set
            {
                double clampedValue = Math.Clamp(value, 0.0, 1.0);
                if (SetField(ref _velocitySensitivity, clampedValue))
                {
                    if (Math.Abs(value - clampedValue) > 0.001)
                    {
                        OnPropertyChanged(nameof(VelocitySensitivity));
                    }
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static bool IsValidFftSize(int size)
        {
            if (size < 128 || size > 32768)
                return false;

            return (size & (size - 1)) == 0;
        }

        public void ResetToDefaults()
        {
            QuantizeValue = QuantizeDuration.Sixteenth;
            PitchAlgorithm = PitchDetectionAlgorithm.SimpleFFT;
            PitchSensitivity = 0.5;
            FftSize = 4096;
            MinNoteDurationMs = 50;
            VelocitySensitivity = 0.5;
        }

        public bool ValidateConfiguration()
        {
            if (!IsValidFftSize(FftSize))
                return false;

            if (PitchSensitivity < 0.0 || PitchSensitivity > 1.0)
                return false;

            if (MinNoteDurationMs < 10 || MinNoteDurationMs > 1000)
                return false;

            if (VelocitySensitivity < 0.0 || VelocitySensitivity > 1.0)
                return false;

            return true;
        }
    }
}
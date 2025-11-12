using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MIDI.UI.Commands;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public enum MeterType
    {
        [Description("VU Meter")]
        VU,
        [Description("Peak Meter")]
        Peak,
        [Description("RMS Meter")]
        RMS,
        [Description("Loudness Meter")]
        Loudness
    }

    public class SoundPeakViewModel : ViewModelBase
    {
        private MeterType _currentMeterType = MeterType.Peak;
        public MeterType CurrentMeterType
        {
            get => _currentMeterType;
            set => SetField(ref _currentMeterType, value);
        }

        private double _peakLeftDb;
        public double PeakLeftDb { get => _peakLeftDb; set => SetField(ref _peakLeftDb, value); }

        private double _peakRightDb;
        public double PeakRightDb { get => _peakRightDb; set => SetField(ref _peakRightDb, value); }

        private double _rmsLeftDb;
        public double RmsLeftDb { get => _rmsLeftDb; set => SetField(ref _rmsLeftDb, value); }

        private double _rmsRightDb;
        public double RmsRightDb { get => _rmsRightDb; set => SetField(ref _rmsRightDb, value); }

        private double _vuLeft;
        public double VuLeft { get => _vuLeft; set => SetField(ref _vuLeft, value); }

        private double _vuRight;
        public double VuRight { get => _vuRight; set => SetField(ref _vuRight, value); }

        private double _momentaryLoudness;
        public double MomentaryLoudness { get => _momentaryLoudness; set => SetField(ref _momentaryLoudness, value); }

        private double _shortTermLoudness;
        public double ShortTermLoudness { get => _shortTermLoudness; set => SetField(ref _shortTermLoudness, value); }

        private double _integratedLoudness;
        public double IntegratedLoudness { get => _integratedLoudness; set => SetField(ref _integratedLoudness, value); }

        public ICommand SetMeterTypeCommand { get; }
        public ICommand ResetLoudnessCommand { get; }
        public Action? ResetLoudnessAction { get; set; }

        public SoundPeakViewModel()
        {
            PeakLeftDb = -60;
            PeakRightDb = -60;
            RmsLeftDb = -60;
            RmsRightDb = -60;
            VuLeft = -60;
            VuRight = -60;
            MomentaryLoudness = -70;
            ShortTermLoudness = -70;
            IntegratedLoudness = -70;

            SetMeterTypeCommand = new RelayCommand(p =>
            {
                if (p is MeterType meterType)
                {
                    CurrentMeterType = meterType;
                }
            });

            ResetLoudnessCommand = new RelayCommand(_ => ResetLoudnessAction?.Invoke());
        }

        public void UpdateValues(double peakL, double peakR, double rmsL, double rmsR, double vuL, double vuR, double momentary, double shortTerm, double integrated)
        {
            PeakLeftDb = peakL > 0 ? 20 * Math.Log10(peakL) : -144;
            PeakRightDb = peakR > 0 ? 20 * Math.Log10(peakR) : -144;
            RmsLeftDb = rmsL > 0 ? 20 * Math.Log10(rmsL) : -144;
            RmsRightDb = rmsR > 0 ? 20 * Math.Log10(rmsR) : -144;
            VuLeft = vuL;
            VuRight = vuR;
            MomentaryLoudness = momentary;
            ShortTermLoudness = shortTerm;
            IntegratedLoudness = integrated;
        }
    }
}
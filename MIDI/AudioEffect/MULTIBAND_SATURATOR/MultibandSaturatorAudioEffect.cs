using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;
using MIDI.AudioEffect.MULTIBAND_SATURATOR.UI;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR
{
    [AudioEffect("MULTIBAND SATURATOR", ["MIDI"], ["マルチバンド", "サチュレーター", "歪み"], IsAviUtlSupported = false)]
    public class MultibandSaturatorAudioEffect : AudioEffectBase
    {
        public override string Label => "MB SATURATOR";

        [Display(GroupName = "MB SATURATOR", Name = "")]
        [MultibandSaturatorControl(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public bool DummyPropertyForUI { get; } = false;

        public double FreqLowMid { get => freqLowMid; set => Set(ref freqLowMid, value); }
        private double freqLowMid = 200;

        public double FreqMidHigh { get => freqMidHigh; set => Set(ref freqMidHigh, value); }
        private double freqMidHigh = 2000;

        public double LowDrive { get => lowDrive; set => Set(ref lowDrive, value); }
        private double lowDrive = 0;
        public double LowLevel { get => lowLevel; set => Set(ref lowLevel, value); }
        private double lowLevel = 0;

        public double MidDrive { get => midDrive; set => Set(ref midDrive, value); }
        private double midDrive = 0;
        public double MidLevel { get => midLevel; set => Set(ref midLevel, value); }
        private double midLevel = 0;

        public double HighDrive { get => highDrive; set => Set(ref highDrive, value); }
        private double highDrive = 0;
        public double HighLevel { get => highLevel; set => Set(ref highLevel, value); }
        private double highLevel = 0;

        public double MasterMix { get => masterMix; set => Set(ref masterMix, value); }
        private double masterMix = 100;

        public double MasterGain { get => masterGain; set => Set(ref masterGain, value); }
        private double masterGain = 0;

        public string SelectedPresetCategory { get => selectedPresetCategory; set => Set(ref selectedPresetCategory, value); }
        private string selectedPresetCategory = string.Empty;

        public string SelectedPreset { get => selectedPreset; set => Set(ref selectedPreset, value); }
        private string selectedPreset = string.Empty;

        public double InputLevel { get; set; } = -60.0;
        public double OutputLevel { get; set; } = -60.0;

        public double LowMeter { get; set; } = -60.0;
        public double MidMeter { get; set; } = -60.0;
        public double HighMeter { get; set; } = -60.0;

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new MultibandSaturatorAudioEffectProcessor(this, duration);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
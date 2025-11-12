using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;
using MIDI.AudioEffect.DELAY.UI;

namespace MIDI.AudioEffect.DELAY
{
    [AudioEffect("DELAY", ["MIDI"], ["ディレイ", "エコー"], IsAviUtlSupported = false)]
    public class DelayAudioEffect : AudioEffectBase
    {
        public override string Label => "DELAY";

        [Display(GroupName = "DELAY", Name = "")]
        [DelayEffectControl(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public bool DummyPropertyForUI { get; } = false;

        public double Mix { get => mix; set => Set(ref mix, value); }
        private double mix = 29;

        public double DelayTime { get => delayTime; set => Set(ref delayTime, value); }
        private double delayTime = 250;

        public double Feedback { get => feedback; set => Set(ref feedback, value); }
        private double feedback = 30;

        public string SelectedPresetCategory { get => selectedPresetCategory; set => Set(ref selectedPresetCategory, value); }
        private string selectedPresetCategory = string.Empty;

        public string SelectedPreset { get => selectedPreset; set => Set(ref selectedPreset, value); }
        private string selectedPreset = string.Empty;

        public double InputLevel { get; set; } = -60.0;
        public double OutputLevel { get; set; } = -60.0;

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new DelayAudioEffectProcessor(this, duration);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
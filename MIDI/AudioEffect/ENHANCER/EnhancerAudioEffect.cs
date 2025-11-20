using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;
using MIDI.AudioEffect.ENHANCER.UI;

namespace MIDI.AudioEffect.ENHANCER
{
    [AudioEffect("ENHANCER", ["MIDI"], ["エンハンサー", "エキサイター", "高音強調"], IsAviUtlSupported = false)]
    public class EnhancerAudioEffect : AudioEffectBase
    {
        public override string Label => "ENHANCER";

        [Display(GroupName = "ENHANCER", Name = "")]
        [EnhancerEffectControl(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public bool DummyPropertyForUI { get; } = false;

        public double Mix { get => mix; set => Set(ref mix, value); }
        private double mix = 50;

        public double Drive { get => drive; set => Set(ref drive, value); }
        private double drive = 20;

        public double Frequency { get => frequency; set => Set(ref frequency, value); }
        private double frequency = 3000;

        public double Gain { get => gain; set => Set(ref gain, value); }
        private double gain = 0;

        public string SelectedPresetCategory { get => selectedPresetCategory; set => Set(ref selectedPresetCategory, value); }
        private string selectedPresetCategory = string.Empty;

        public string SelectedPreset { get => selectedPreset; set => Set(ref selectedPreset, value); }
        private string selectedPreset = string.Empty;

        public double InputLevel { get; set; } = -60.0;
        public double OutputLevel { get; set; } = -60.0;

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new EnhancerAudioEffectProcessor(this, duration);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
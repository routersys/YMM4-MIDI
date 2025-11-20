using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;
using MIDI.AudioEffect.SpatialAudioEffect.UI;

namespace MIDI.AudioEffect.SpatialAudioEffect
{
    [AudioEffect("SPATIAL AUDIO", ["MIDI"], ["音響", "IR", "畳み込み"], IsAviUtlSupported = false)]
    public class SpatialAudioEffect : AudioEffectBase
    {
        public override string Label => "SPATIAL AUDIO";

        [Display(GroupName = "立体音響", Name = "")]
        [SpatialAudioEffectControl(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public bool DummyPropertyForUI { get; } = false;

        public string IrFileLeft { get => irFileLeft; set => Set(ref irFileLeft, value); }
        string irFileLeft = "";

        public string IrFileRight { get => irFileRight; set => Set(ref irFileRight, value); }
        string irFileRight = "";

        public double Gain { get => gain; set => Set(ref gain, value); }
        double gain = 0;

        public double Mix { get => mix; set => Set(ref mix, value); }
        double mix = 100;

        public string SelectedPresetCategory { get => selectedPresetCategory; set => Set(ref selectedPresetCategory, value); }
        private string selectedPresetCategory = string.Empty;

        public string SelectedPreset { get => selectedPreset; set => Set(ref selectedPreset, value); }
        private string selectedPreset = string.Empty;

        public double InputLevel { get; set; } = -60.0;
        public double OutputLevel { get; set; } = -60.0;

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new SpatialAudioEffectProcessor(this, duration);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
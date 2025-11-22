using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Audio.Effects;
using YukkuriMovieMaker.Plugin.Effects;
using MIDI.AudioEffect.AMP_SIMULATOR.UI;

namespace MIDI.AudioEffect.AMP_SIMULATOR
{
    [AudioEffect("AMP SIMULATOR (使用の非推奨)", ["MIDI"], ["アンプ", "ギター", "歪み"], IsAviUtlSupported = false)]
    public class AmpSimulatorAudioEffect : AudioEffectBase
    {
        public override string Label => "AMP SIMULATOR (使用の非推奨)";

        [Display(GroupName = "AMP SIMULATOR", Name = "")]
        [AmpSimulatorControl(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public bool DummyPropertyForUI { get; } = false;

        public double InputGain { get => inputGain; set => Set(ref inputGain, value); }
        private double inputGain = 50;

        public double Bass { get => bass; set => Set(ref bass, value); }
        private double bass = 50;
        public double Middle { get => middle; set => Set(ref middle, value); }
        private double middle = 50;
        public double Treble { get => treble; set => Set(ref treble, value); }
        private double treble = 50;
        public double Presence { get => presence; set => Set(ref presence, value); }
        private double presence = 50;

        public double MasterVolume { get => masterVolume; set => Set(ref masterVolume, value); }
        private double masterVolume = 50;

        public double Sag { get => sag; set => Set(ref sag, value); }
        private double sag = 20;
        public double Bias { get => bias; set => Set(ref bias, value); }
        private double bias = 50;

        public double CabinetResonance { get => cabinetResonance; set => Set(ref cabinetResonance, value); }
        private double cabinetResonance = 50;
        public double CabinetBright { get => cabinetBright; set => Set(ref cabinetBright, value); }
        private double cabinetBright = 50;

        public string SelectedPresetCategory { get => selectedPresetCategory; set => Set(ref selectedPresetCategory, value); }
        private string selectedPresetCategory = string.Empty;

        public string SelectedPreset { get => selectedPreset; set => Set(ref selectedPreset, value); }
        private string selectedPreset = string.Empty;

        public double InputLevel { get; set; } = -60.0;
        public double OutputLevel { get; set; } = -60.0;

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new AmpSimulatorAudioEffectProcessor(this, duration);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [];
    }
}
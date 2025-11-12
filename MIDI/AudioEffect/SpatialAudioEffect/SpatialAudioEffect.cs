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
    [AudioEffect("立体音響(畳み込み)", ["MIDI"], ["音響", "IR", "畳み込み"], IsAviUtlSupported = false)]
    public class SpatialAudioEffect : AudioEffectBase
    {
        public override string Label => "立体音響(畳み込み)";

        [Display(GroupName = "立体音響", Name = "IRファイル(L / ステレオ)", Description = "左耳用IR(モノラル) または L/R両方を含むIR(ステレオ)")]
        [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.AudioItem)]
        public string IrFileLeft { get => irFileLeft; set => Set(ref irFileLeft, value); }
        string irFileLeft = "";

        [Display(GroupName = "立体音響", Name = "IRファイル(R)", Description = "右耳用IR(モノラル)。 (L側でステレオIRを指定した場合は無視されます)")]
        [FileSelector(YukkuriMovieMaker.Settings.FileGroupType.AudioItem)]
        public string IrFileRight { get => irFileRight; set => Set(ref irFileRight, value); }
        string irFileRight = "";

        [Display(GroupName = "立体音響", Name = "IR状態", Description = "インパルス応答ファイルの状態（サンプリングレートの検証は処理実行時に行われます）")]
        [IrStatusDisplay]
        public bool IrStatus { get; } = false;

        [Display(GroupName = "立体音響", Name = "ゲイン", Description = "音量")]
        [AnimationSlider("F0", "dB", -60, 0)]
        public Animation Gain { get; } = new Animation(0, -60, 0);

        public override IAudioEffectProcessor CreateAudioEffect(TimeSpan duration)
        {
            return new SpatialAudioEffectProcessor(this, duration);
        }

        public override IEnumerable<string> CreateExoAudioFilters(int keyFrameIndex, ExoOutputDescription exoOutputDescription)
        {
            return [];
        }

        protected override IEnumerable<IAnimatable> GetAnimatables() => [Gain];
    }
}
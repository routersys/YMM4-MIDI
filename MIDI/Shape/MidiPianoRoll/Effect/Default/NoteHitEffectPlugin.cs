using System;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace MIDI.Shape.MidiPianoRoll.Effects.Default
{
    public class NoteHitEffectPlugin : IEffectPlugin
    {
        public string Name => "ノートヒットエフェクト";
        public string GroupName => "ビジュアル";
        public Type ParameterType => typeof(NoteHitEffectParameter);
        public EffectParameterBase CreateParameter() => new NoteHitEffectParameter();
        public IEffect CreateEffect(IGraphicsDevicesAndContext devices) => new NoteHitEffect(devices);
    }
}
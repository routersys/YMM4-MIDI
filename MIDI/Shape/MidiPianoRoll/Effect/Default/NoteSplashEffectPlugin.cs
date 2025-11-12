using System;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace MIDI.Shape.MidiPianoRoll.Effects.Default
{
    public class NoteSplashEffectPlugin : IEffectPlugin
    {
        public string Name => "ノートスプラッシュ";
        public string GroupName => "ビジュアル";
        public Type ParameterType => typeof(NoteSplashEffectParameter);
        public EffectParameterBase CreateParameter() => new NoteSplashEffectParameter();
        public IEffect CreateEffect(IGraphicsDevicesAndContext devices) => new NoteSplashEffect(devices);
    }
}
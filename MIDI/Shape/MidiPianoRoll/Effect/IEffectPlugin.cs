using System;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace MIDI.Shape.MidiPianoRoll.Effects
{
    public interface IEffectPlugin
    {
        string Name { get; }
        string GroupName { get; }
        Type ParameterType { get; }
        EffectParameterBase CreateParameter();
        IEffect CreateEffect(IGraphicsDevicesAndContext devices);
    }
}
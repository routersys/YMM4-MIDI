using System;
using MIDI.Shape.MidiPianoRoll.Models;
using MIDI.Shape.MidiPianoRoll.Rendering;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Player.Video;

namespace MIDI.Shape.MidiPianoRoll.Effects
{
    public interface IEffect : IDisposable
    {
        void Update(TimelineItemSourceDescription desc, EffectParameterBase parameter);

        void Draw(
            ID2D1DeviceContext dc,
            TimelineItemSourceDescription desc,
            TimeSpan midiTime,
            MidiPianoRollParameter globalParameter,
            MidiDataManager midiDataManager,
            Direct2DResourceProvider resourceProvider,
            EffectParameterBase effectParameter
        );
    }
}
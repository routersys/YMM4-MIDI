using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;
using MIDI.Shape.MidiPianoRoll.Models;

namespace MIDI.Shape.MidiPianoRoll
{
    public class MidiPianoRollPlugin : IShapePlugin
    {
        public string Name => "MIDI ピアノロール";
        public bool IsExoShapeSupported => false;
        public bool IsExoMaskSupported => false;

        public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
        {
            return new MidiPianoRollParameter(sharedData, new ResourcePackService());
        }
    }
}
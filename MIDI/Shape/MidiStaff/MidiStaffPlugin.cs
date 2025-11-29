using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace MIDI.Shape.MidiStaff
{
    public class MidiStaffPlugin : IShapePlugin
    {
        public string Name => "MIDI 五線譜";
        public bool IsExoShapeSupported => false;
        public bool IsExoMaskSupported => false;

        public IShapeParameter CreateShapeParameter(SharedDataStore? sharedData)
        {
            return new MidiStaffParameter(sharedData);
        }
    }
}
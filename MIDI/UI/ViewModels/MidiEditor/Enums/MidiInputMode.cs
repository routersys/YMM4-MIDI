using System.ComponentModel;

namespace MIDI.UI.ViewModels.MidiEditor.Enums
{
    public enum MidiInputMode
    {
        [Description("MIDIキーボード")]
        Keyboard,
        [Description("リアルタイム")]
        Realtime,
        [Description("PCキーボード")]
        ComputerKeyboard
    }
}
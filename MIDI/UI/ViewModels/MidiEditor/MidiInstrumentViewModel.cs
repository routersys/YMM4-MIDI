namespace MIDI.UI.ViewModels.MidiEditor
{
    public class MidiInstrumentViewModel
    {
        public string Name { get; set; } = string.Empty;
        public int PatchNumber { get; set; }
        public int BankNumber { get; set; }

        public string DisplayName => $"{PatchNumber}: {Name}";
    }
}
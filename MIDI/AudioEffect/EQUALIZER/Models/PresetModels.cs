namespace MIDI.AudioEffect.EQUALIZER.Models
{
    public class PresetInfo
    {
        public string Name { get; set; } = "";
        public string Group { get; set; } = "";
        public bool IsFavorite { get; set; }
    }

    public class PresetMetadata
    {
        public string Group { get; set; } = "other";
        public bool IsFavorite { get; set; } = false;
    }
}
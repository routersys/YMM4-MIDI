namespace MIDI.AudioEffect.ENHANCER.Models
{
    public class EnhancerPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Drive { get; set; } = 0;
        public double Frequency { get; set; } = 1000;
        public double Mix { get; set; } = 0;

        public EnhancerPreset(string name, string category, double drive, double frequency, double mix)
        {
            Name = name;
            Category = category;
            Drive = drive;
            Frequency = frequency;
            Mix = mix;
        }

        public EnhancerPreset() { }
    }
}
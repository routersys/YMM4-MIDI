namespace MIDI.AudioEffect.DELAY.Models
{
    public class DelayPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double DelayTimeMs { get; set; } = 0;
        public double Feedback { get; set; } = 0;

        public DelayPreset(string name, string category, double delayTimeMs, double feedback)
        {
            Name = name;
            Category = category;
            DelayTimeMs = delayTimeMs;
            Feedback = feedback;
        }
    }
}
using MIDI.AudioEffect.SpatialAudioEffect.Models;

namespace MIDI.AudioEffect.SpatialAudioEffect.Models
{
    public class SpatialPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string IrFileLeft { get; set; } = string.Empty;
        public string IrFileRight { get; set; } = string.Empty;
        public double Gain { get; set; } = 0;
        public double Mix { get; set; } = 100;

        public SpatialPreset() { }

        public SpatialPreset(string name, string category, string irL, string irR, double gain, double mix)
        {
            Name = name;
            Category = category;
            IrFileLeft = irL;
            IrFileRight = irR;
            Gain = gain;
            Mix = mix;
        }
    }
}
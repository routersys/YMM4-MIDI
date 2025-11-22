namespace MIDI.AudioEffect.AMP_SIMULATOR.Models
{
    public class AmpPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        public double InputGain { get; set; } = 50;
        public double Bass { get; set; } = 50;
        public double Middle { get; set; } = 50;
        public double Treble { get; set; } = 50;
        public double Presence { get; set; } = 50;

        public double MasterVolume { get; set; } = 50;
        public double Sag { get; set; } = 20;
        public double Bias { get; set; } = 50;

        public double CabinetResonance { get; set; } = 50;
        public double CabinetBright { get; set; } = 50;

        public AmpPreset() { }

        public AmpPreset(string name, string category, double gain, double bass, double mid, double treble, double pres, double master, double sag, double bias, double cabRes, double cabBright)
        {
            Name = name;
            Category = category;
            InputGain = gain;
            Bass = bass;
            Middle = mid;
            Treble = treble;
            Presence = pres;
            MasterVolume = master;
            Sag = sag;
            Bias = bias;
            CabinetResonance = cabRes;
            CabinetBright = cabBright;
        }
    }
}
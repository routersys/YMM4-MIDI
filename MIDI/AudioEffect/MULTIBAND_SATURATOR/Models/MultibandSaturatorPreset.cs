namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.Models
{
    public class MultibandSaturatorPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        public double FreqLowMid { get; set; } = 200;
        public double FreqMidHigh { get; set; } = 2000;

        public double LowDrive { get; set; } = 0;
        public double LowLevel { get; set; } = 0;

        public double MidDrive { get; set; } = 0;
        public double MidLevel { get; set; } = 0;

        public double HighDrive { get; set; } = 0;
        public double HighLevel { get; set; } = 0;

        public double MasterMix { get; set; } = 100;
        public double MasterGain { get; set; } = 0;

        public MultibandSaturatorPreset() { }

        public MultibandSaturatorPreset(string name, string category,
            double freqLM, double freqMH,
            double lDrive, double lLevel,
            double mDrive, double mLevel,
            double hDrive, double hLevel,
            double mix, double gain)
        {
            Name = name;
            Category = category;
            FreqLowMid = freqLM;
            FreqMidHigh = freqMH;
            LowDrive = lDrive;
            LowLevel = lLevel;
            MidDrive = mDrive;
            MidLevel = mLevel;
            HighDrive = hDrive;
            HighLevel = hLevel;
            MasterMix = mix;
            MasterGain = gain;
        }
    }
}
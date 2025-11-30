using System;

namespace MIDI.AudioEffect.EQUALIZER.Interfaces
{
    public interface IConfigService
    {
        bool HighQualityMode { get; set; }
        double EditorHeight { get; set; }
        string DefaultPreset { get; set; }
        void Load();
        void Save();
    }
}
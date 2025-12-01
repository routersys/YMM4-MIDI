using MIDI.AudioEffect.EQUALIZER.Interfaces;
using MIDI.Utils;
using System;
using System.IO;
using System.Reflection;

namespace MIDI.AudioEffect.EQUALIZER.Services
{
    public class ConfigService : IConfigService
    {
        private readonly string _configPath;
        private readonly IniFile _iniFile;
        private const string SectionGeneral = "General";
        private const string SectionView = "View";

        public ConfigService()
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            _configPath = Path.Combine(assemblyLocation, "Config", "settings.ini");
            _iniFile = new IniFile();
            Load();
        }

        public bool HighQualityMode { get; set; }
        public double EditorHeight { get; set; } = 240;
        public string DefaultPreset { get; set; } = "";
        public EqualizerAlgorithm Algorithm { get; set; } = EqualizerAlgorithm.Biquad;

        public void Load()
        {
            _iniFile.Load(_configPath);
            HighQualityMode = bool.TryParse(_iniFile.GetValue(SectionGeneral, "HighQualityMode", "false"), out var hq) && hq;
            DefaultPreset = _iniFile.GetValue(SectionGeneral, "DefaultPreset", "");

            if (Enum.TryParse<EqualizerAlgorithm>(_iniFile.GetValue(SectionGeneral, "Algorithm", "Biquad"), out var alg))
            {
                Algorithm = alg;
            }

            if (double.TryParse(_iniFile.GetValue(SectionView, "EditorHeight", "240"), out var height))
            {
                EditorHeight = height;
            }
        }

        public void Save()
        {
            _iniFile.SetValue(SectionGeneral, "HighQualityMode", HighQualityMode.ToString());
            _iniFile.SetValue(SectionGeneral, "DefaultPreset", DefaultPreset);
            _iniFile.SetValue(SectionGeneral, "Algorithm", Algorithm.ToString());
            _iniFile.SetValue(SectionView, "EditorHeight", EditorHeight.ToString());
            _iniFile.Save(_configPath);
        }
    }
}
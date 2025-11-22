using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MIDI.AudioEffect.AMP_SIMULATOR.Models;

namespace MIDI.AudioEffect.AMP_SIMULATOR.Services
{
    public class AmpPresetService
    {
        private readonly List<AmpPreset> presets;
        private static string BaseDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        private static string PresetsDir => Path.Combine(BaseDir, "presets");

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        public AmpPresetService()
        {
            presets = LoadDefaultPresets();
            LoadExternalPresets();
        }

        private List<AmpPreset> LoadDefaultPresets()
        {
            return
            [
                new AmpPreset("Clean American", "Clean", 20, 60, 40, 70, 60, 80, 30, 50, 40, 70),
                new AmpPreset("Glassy Clean", "Clean", 15, 40, 30, 80, 90, 90, 10, 45, 30, 90),
                new AmpPreset("Jazz Tone", "Clean", 25, 70, 60, 30, 20, 70, 5, 50, 60, 20),

                new AmpPreset("Crunch British", "Crunch", 55, 50, 70, 60, 50, 60, 50, 60, 50, 60),
                new AmpPreset("Blues Driver", "Crunch", 45, 60, 50, 50, 40, 70, 60, 55, 50, 50),
                new AmpPreset("Edge Breakup", "Crunch", 35, 55, 55, 65, 70, 80, 40, 50, 45, 60),

                new AmpPreset("Lead Modern", "High Gain", 80, 70, 30, 60, 70, 40, 70, 70, 80, 40),
                new AmpPreset("Tight Metal", "High Gain", 90, 50, 40, 80, 80, 50, 20, 80, 90, 80),
                new AmpPreset("Solo Sustain", "High Gain", 85, 45, 80, 50, 40, 50, 80, 65, 60, 30),

                new AmpPreset("Vintage Stack", "Vintage", 65, 80, 60, 40, 30, 90, 90, 40, 70, 20),
                new AmpPreset("Fuzz Face", "Vintage", 100, 90, 50, 20, 10, 40, 100, 20, 50, 10),

                new AmpPreset("Bass Clean", "Bass", 30, 80, 50, 40, 30, 80, 20, 50, 90, 40),
                new AmpPreset("Bass Drive", "Bass", 60, 70, 60, 50, 40, 70, 40, 60, 80, 30),
            ];
        }

        private void LoadExternalPresets()
        {
            if (!Directory.Exists(PresetsDir))
            {
                try { Directory.CreateDirectory(PresetsDir); } catch { return; }
            }

            try
            {
                var files = Directory.GetFiles(PresetsDir, "*.mamp", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var loadedPreset = JsonSerializer.Deserialize<AmpPreset>(json, jsonOptions);
                        if (loadedPreset != null && !string.IsNullOrEmpty(loadedPreset.Name))
                        {
                            presets.Add(loadedPreset);
                        }
                    }
                    catch { }
                }
            }
            catch { }
        }

        public IEnumerable<string> GetCategories()
        {
            var categories = presets.Select(p => p.Category).Distinct().ToList();
            var allCategories = new List<string> { "すべて" };
            allCategories.AddRange(categories);
            return allCategories.Any() ? allCategories : ["すべて", "デフォルト"];
        }

        public IEnumerable<AmpPreset> GetPresets(string category)
        {
            if (category == "すべて") return presets;
            var result = presets.Where(p => p.Category == category);
            return result.Any() ? result : [new AmpPreset("デフォルト", "基本", 50, 50, 50, 50, 50, 50, 50, 50, 50, 50)];
        }

        public AmpPreset? GetPreset(string name)
        {
            return presets.FirstOrDefault(p => p.Name == name);
        }
    }
}
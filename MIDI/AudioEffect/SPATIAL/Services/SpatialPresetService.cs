using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MIDI.AudioEffect.SPATIAL.Models;

namespace MIDI.AudioEffect.SPATIAL.Services
{
    public class SpatialPresetService
    {
        private readonly List<SpatialPreset> presets;
        private static string BaseDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        private static string PresetsDir => Path.Combine(BaseDir, "presets");

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        public SpatialPresetService()
        {
            presets = LoadDefaultPresets();
            LoadExternalPresets();
        }

        private List<SpatialPreset> LoadDefaultPresets()
        {
            return
            [
                new SpatialPreset("デフォルト", "基本", "", "", 0, 100),
                new SpatialPreset("隠し味", "基本", "", "", -6, 20),
                new SpatialPreset("深い残響", "基本", "", "", 0, 100),
                new SpatialPreset("ブースト", "調整", "", "", 6, 100),
            ];
        }

        private void LoadExternalPresets()
        {
            if (!Directory.Exists(PresetsDir))
            {
                try
                {
                    Directory.CreateDirectory(PresetsDir);
                }
                catch (Exception)
                {
                    return;
                }
            }

            try
            {
                var files = Directory.GetFiles(PresetsDir, "*.sspatial", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var loadedPreset = JsonSerializer.Deserialize<SpatialPreset>(json, jsonOptions);
                        if (loadedPreset != null && !string.IsNullOrEmpty(loadedPreset.Name))
                        {
                            presets.Add(loadedPreset);
                        }
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public IEnumerable<string> GetCategories()
        {
            var categories = presets.Select(p => p.Category).Distinct().ToList();
            var allCategories = new List<string> { "すべて" };
            allCategories.AddRange(categories);

            if (!categories.Any())
            {
                return ["すべて", "デフォルト"];
            }

            return allCategories;
        }

        public IEnumerable<SpatialPreset> GetPresets(string category)
        {
            IEnumerable<SpatialPreset> result;

            if (category == "すべて")
            {
                result = presets;
            }
            else
            {
                result = presets.Where(p => p.Category == category);
            }

            if (!result.Any())
            {
                return [new SpatialPreset("デフォルト", "デフォルト", "", "", 0, 100)];
            }

            return result;
        }
    }
}
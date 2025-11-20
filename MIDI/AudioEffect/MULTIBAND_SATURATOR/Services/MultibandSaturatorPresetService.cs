using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MIDI.AudioEffect.MULTIBAND_SATURATOR.Models;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.Services
{
    public class MultibandSaturatorPresetService
    {
        private readonly List<MultibandSaturatorPreset> presets;
        private static string BaseDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        private static string PresetsDir => Path.Combine(BaseDir, "presets");

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        public MultibandSaturatorPresetService()
        {
            presets = LoadDefaultPresets();
            LoadExternalPresets();
        }

        private List<MultibandSaturatorPreset> LoadDefaultPresets()
        {
            return
            [
                new MultibandSaturatorPreset("デフォルト", "基本",
                    200, 2500,
                    0, 0, 0, 0, 0, 0,
                    100, 0),

                new MultibandSaturatorPreset("ウォーム・ベース", "ベース",
                    300, 2500,
                    40, 2, 10, 0, 0, 0,
                    100, 0),

                new MultibandSaturatorPreset("ドラム・パンチ", "ドラム",
                    150, 4000,
                    30, 1, 20, 0, 50, 2,
                    100, -1),

                new MultibandSaturatorPreset("ボーカル・プレゼンス", "ボーカル",
                    250, 3000,
                    0, 0, 30, 1, 40, 2,
                    80, 0),

                new MultibandSaturatorPreset("マスタリング・ヒート", "マスタリング",
                    120, 6000,
                    10, 0, 5, 0, 15, 0,
                    40, 0),

                new MultibandSaturatorPreset("ローファイ・ラジオ", "特殊効果",
                    500, 2000,
                    80, -5, 100, 0, 0, -20,
                    100, 0),
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
                var files = Directory.GetFiles(PresetsDir, "*.mband", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var loadedPreset = JsonSerializer.Deserialize<MultibandSaturatorPreset>(json, jsonOptions);
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

        public IEnumerable<MultibandSaturatorPreset> GetPresets(string category)
        {
            if (category == "すべて") return presets;
            var result = presets.Where(p => p.Category == category);
            return result.Any() ? result : [new MultibandSaturatorPreset("デフォルト", "基本", 200, 2500, 0, 0, 0, 0, 0, 0, 100, 0)];
        }

        public MultibandSaturatorPreset? GetPreset(string name)
        {
            return presets.FirstOrDefault(p => p.Name == name);
        }
    }
}
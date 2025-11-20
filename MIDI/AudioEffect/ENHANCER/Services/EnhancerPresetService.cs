using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MIDI.AudioEffect.ENHANCER.Models;

namespace MIDI.AudioEffect.ENHANCER.Services
{
    public class EnhancerPresetService
    {
        private readonly List<EnhancerPreset> presets;
        private static string BaseDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        private static string PresetsDir => Path.Combine(BaseDir, "presets");

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        public EnhancerPresetService()
        {
            presets = LoadDefaultPresets();
            LoadExternalPresets();
        }

        private List<EnhancerPreset> LoadDefaultPresets()
        {
            return
            [
                new EnhancerPreset("デフォルト", "基本", 20, 3000, 50),
                new EnhancerPreset("きらめき (弱)", "基本", 15, 5000, 30),
                new EnhancerPreset("きらめき (強)", "基本", 60, 4000, 60),
                new EnhancerPreset("輪郭強調", "基本", 40, 2000, 40),

                new EnhancerPreset("ボーカル (ブライト)", "ボーカル", 30, 2500, 40),
                new EnhancerPreset("ボーカル (エアリー)", "ボーカル", 50, 6000, 50),
                new EnhancerPreset("ナレーション明瞭化", "ボーカル", 25, 1500, 35),

                new EnhancerPreset("ドラム (スネア)", "ドラム", 40, 3000, 45),
                new EnhancerPreset("ドラム (シンバル)", "ドラム", 60, 8000, 60),
                new EnhancerPreset("アタック強調", "ドラム", 50, 1000, 50),

                new EnhancerPreset("アコギ (艶出し)", "楽器", 35, 3500, 40),
                new EnhancerPreset("ピアノ (クリア)", "楽器", 20, 4000, 30),
                new EnhancerPreset("ベース (エッジ)", "楽器", 45, 800, 50),

                new EnhancerPreset("マスタリング (隠し味)", "マスタリング", 10, 10000, 20),
                new EnhancerPreset("全体を明るく", "マスタリング", 20, 5000, 30),
                new EnhancerPreset("ラジオボイス風", "特殊効果", 100, 500, 100),
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
                catch
                {
                    return;
                }
            }

            try
            {
                var files = Directory.GetFiles(PresetsDir, "*.menhancer", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var loadedPreset = JsonSerializer.Deserialize<EnhancerPreset>(json, jsonOptions);
                        if (loadedPreset != null && !string.IsNullOrEmpty(loadedPreset.Name))
                        {
                            presets.Add(loadedPreset);
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
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

        public IEnumerable<EnhancerPreset> GetPresets(string category)
        {
            IEnumerable<EnhancerPreset> result;

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
                return [new EnhancerPreset("デフォルト", "デフォルト", 20, 3000, 50)];
            }

            return result;
        }

        public EnhancerPreset? GetPreset(string name)
        {
            return presets.FirstOrDefault(p => p.Name == name);
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using MIDI.AudioEffect.DELAY.Models;

namespace MIDI.AudioEffect.DELAY.Services
{
    public class PresetService
    {
        private readonly List<DelayPreset> presets;
        private static string BaseDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        private static string PresetsDir => Path.Combine(BaseDir, "presets");

        private static readonly JsonSerializerOptions jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        public PresetService()
        {
            presets = LoadDefaultPresets();
            LoadExternalPresets();
        }

        private List<DelayPreset> LoadDefaultPresets()
        {
            return
            [
                new DelayPreset("1/4 ドラムダブ", "お気に入り", 500, 30),
                new DelayPreset("1/4 コーラスリピート", "お気に入り", 500, 45),
                new DelayPreset("1/8d ギターテープディレイ", "お気に入り", 375, 40),
                new DelayPreset("1/32 フラッターエコー", "お気に入り", 62.5, 60),
                new DelayPreset("フランジ (ハード)", "お気に入り", 10, 85),

                new DelayPreset("シンプルエコー", "基本", 300, 25),
                new DelayPreset("スラップバック", "基本", 80, 10),
                new DelayPreset("短いディレイ", "基本", 150, 20),
                new DelayPreset("中程度のディレイ", "基本", 350, 35),
                new DelayPreset("長いディレイ", "基本", 700, 40),

                new DelayPreset("ダブ (軽め)", "ダブ", 400, 55),
                new DelayPreset("ダブ (深め)", "ダブ", 500, 70),
                new DelayPreset("レゲエキック", "ダブ", 250, 60),
                new DelayPreset("スペースエコー", "ダブ", 600, 65),

                new DelayPreset("ボーカル (ショート)", "ボーカル", 120, 15),
                new DelayPreset("ボーカル (バラード)", "ボーカル", 450, 30),
                new DelayPreset("ボーカル (ダブリング)", "ボーカル", 30, 5),

                new DelayPreset("ギター (ロック)", "ギター", 300, 28),
                new DelayPreset("ギター (ソロリード)", "ギター", 420, 35),
                new DelayPreset("ギター (U2風)", "ギター", 375, 33),
                new DelayPreset("ギター (カントリー)", "ギター", 130, 10),

                new DelayPreset("ショートフランジ", "モジュレーション", 5, 70),
                new DelayPreset("スローフランジ", "モジュレーション", 15, 80),
                new DelayPreset("コーラス (軽め)", "モジュレーション", 25, 40),
                new DelayPreset("コーラス (深め)", "モジュレーション", 35, 50),

                new DelayPreset("無限リピート (注意)", "特殊効果", 500, 99),
                new DelayPreset("ピンポン (模擬)", "特殊効果", 250, 40),
                new DelayPreset("電話エコー", "特殊効果", 100, 60),
                new DelayPreset("古いテープ", "特殊効果", 300, 50),
                new DelayPreset("リズミック 1/8", "特殊効果", 250, 0),
                new DelayPreset("リズミック 1/16", "特殊効果", 125, 0),

                new DelayPreset("部屋 (小)", "リバーブ風", 50, 15),
                new DelayPreset("部屋 (中)", "リバーブ風", 100, 25),
                new DelayPreset("ホール (小)", "リバーブ風", 200, 30),
                new DelayPreset("ホール (大)", "リバーブ風", 350, 35),
                new DelayPreset("アリーナ", "リバーブ風", 500, 40),
                new DelayPreset("洞窟", "リバーブ風", 800, 50),

                new DelayPreset("シンセ (ショート)", "シンセサイザー", 150, 30),
                new DelayPreset("シンセ (アルペジオ)", "シンセサイザー", 375, 45),
                new DelayPreset("シンセ (パッド)", "シンセサイザー", 600, 55),
                new DelayPreset("シンセ (リード)", "シンセサイザー", 400, 40),

                new DelayPreset("ドラム (スネア)", "ドラム", 200, 20),
                new DelayPreset("ドラム (タム)", "ドラム", 300, 25),
                new DelayPreset("ドラム (ハイハット)", "ドラム", 125, 10),

                new DelayPreset("ベース (ショート)", "ベース", 100, 15),
                new DelayPreset("ベース (ファンク)", "ベース", 250, 25),
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
                var files = Directory.GetFiles(PresetsDir, "*.mdelay", SearchOption.TopDirectoryOnly);
                foreach (var file in files)
                {
                    try
                    {
                        var json = File.ReadAllText(file);
                        var loadedPreset = JsonSerializer.Deserialize<DelayPreset>(json, jsonOptions);
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

        public IEnumerable<DelayPreset> GetPresets(string category)
        {
            IEnumerable<DelayPreset> result;

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
                return [new DelayPreset("デフォルト", "デフォルト", 300, 30)];
            }

            return result;
        }

        public DelayPreset? GetPreset(string name)
        {
            return presets.FirstOrDefault(p => p.Name == name);
        }
    }
}
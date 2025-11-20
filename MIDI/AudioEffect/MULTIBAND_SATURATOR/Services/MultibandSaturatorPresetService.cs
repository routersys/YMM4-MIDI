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
                new MultibandSaturatorPreset("デフォルト", "基本", 200, 2500, 0, 0, 0, 0, 0, 0, 100, 0),
                new MultibandSaturatorPreset("ウォーム (微調整)", "基本", 200, 2500, 10, 0.5, 5, 0, 0, 0, 100, 0),
                new MultibandSaturatorPreset("真空管の温かみ", "基本", 300, 2000, 20, 1, 15, 0.5, 5, 0, 100, 0),
                new MultibandSaturatorPreset("テープ・サチュレーション", "基本", 150, 3500, 15, 0.5, 10, 0, 15, 0.5, 80, 0),
                new MultibandSaturatorPreset("エキサイター (高域強調)", "基本", 200, 4000, 0, 0, 0, 0, 30, 1, 50, 0),
                new MultibandSaturatorPreset("プレゼンス・ブースト", "基本", 250, 3000, 0, 0, 20, 1, 40, 2, 100, -1),
                new MultibandSaturatorPreset("ローエンド増強", "基本", 120, 2000, 35, 2, 0, 0, 0, 0, 100, 0),
                new MultibandSaturatorPreset("ドンシャリ", "基本", 200, 3000, 10, 1, 0, -2, 10, 1, 100, 1),
                new MultibandSaturatorPreset("ミッド・フォーカス", "基本", 300, 2000, 0, -1, 25, 1.5, 0, -1, 100, 0),
                new MultibandSaturatorPreset("オールラウンダー", "基本", 180, 2800, 10, 0.5, 8, 0, 12, 0.5, 100, 0),

                new MultibandSaturatorPreset("ベース (ウォーム)", "ベース", 300, 2000, 40, 1.5, 15, 0, 0, 0, 100, 0),
                new MultibandSaturatorPreset("ベース (エッジ)", "ベース", 400, 2500, 20, 0, 50, 2, 30, 1, 100, 0),
                new MultibandSaturatorPreset("サブベース・ブースト", "ベース", 100, 1000, 50, 3, 0, 0, 0, 0, 100, -1),
                new MultibandSaturatorPreset("ファズ・ベース", "ベース", 250, 2000, 80, 0, 60, 0, 20, 0, 80, -2),
                new MultibandSaturatorPreset("スラップベース", "ベース", 150, 3000, 30, 2, 0, -1, 40, 2, 100, 0),
                new MultibandSaturatorPreset("シンセベース (唸り)", "ベース", 200, 1500, 40, 1, 60, 2, 20, 0, 100, 0),

                new MultibandSaturatorPreset("ドラム・バス (まとまり)", "ドラム", 150, 3000, 15, 1, 10, 0, 5, 0, 100, 0),
                new MultibandSaturatorPreset("キック (パンチ)", "ドラム", 120, 2500, 40, 2, 10, 0, 30, 1, 100, -1),
                new MultibandSaturatorPreset("スネア (スナップ)", "ドラム", 200, 3500, 10, 0, 35, 2, 20, 1, 100, 0),
                new MultibandSaturatorPreset("金物 (きらめき)", "ドラム", 300, 5000, 0, 0, 10, 0, 40, 2, 100, 0),
                new MultibandSaturatorPreset("クラッシュ・ドラム", "ドラム", 150, 3000, 60, 1, 50, 1, 40, 0, 60, 0),
                new MultibandSaturatorPreset("エレクトロ・ビート", "ドラム", 100, 4000, 30, 1.5, 20, 0, 30, 1, 100, 0),

                new MultibandSaturatorPreset("ボーカル (ウォーム)", "ボーカル", 250, 3000, 20, 1, 10, 0.5, 0, 0, 100, 0),
                new MultibandSaturatorPreset("ボーカル (エアリー)", "ボーカル", 300, 5000, 0, 0, 5, 0, 35, 2, 100, 0),
                new MultibandSaturatorPreset("ラジオ・ボイス", "ボーカル", 500, 2000, 90, -5, 80, 2, 90, -10, 100, 0),
                new MultibandSaturatorPreset("ロック・ボーカル", "ボーカル", 250, 3500, 15, 0, 40, 2, 20, 1, 100, 0),
                new MultibandSaturatorPreset("コーラス (馴染ませ)", "ボーカル", 300, 3000, 10, 0, 10, 0, 0, -2, 100, 0),
                new MultibandSaturatorPreset("ウィスパー (強調)", "ボーカル", 200, 4000, 20, 1, 30, 2, 40, 3, 100, -2),

                new MultibandSaturatorPreset("アコギ (艶)", "ギター", 200, 3500, 10, 1, 15, 0.5, 25, 1, 100, 0),
                new MultibandSaturatorPreset("エレキ (クリーン)", "ギター", 250, 3000, 20, 1, 10, 0, 15, 1, 100, 0),
                new MultibandSaturatorPreset("エレキ (クランチ)", "ギター", 300, 2500, 10, 0, 45, 2, 30, 1, 100, 0),
                new MultibandSaturatorPreset("ギター・ソロ", "ギター", 350, 3000, 0, 0, 50, 2, 40, 1.5, 100, 0),
                new MultibandSaturatorPreset("太いバッキング", "ギター", 250, 2000, 40, 2, 30, 1, 10, 0, 100, 0),

                new MultibandSaturatorPreset("シンセパッド (温かみ)", "シンセ", 300, 2500, 25, 1, 15, 0, 10, 0, 100, 0),
                new MultibandSaturatorPreset("シンセリード (抜け)", "シンセ", 300, 4000, 10, 0, 40, 1.5, 50, 2, 100, 0),
                new MultibandSaturatorPreset("アシッド・シンセ", "シンセ", 200, 2000, 30, 0, 70, 2, 60, 2, 80, -1),
                new MultibandSaturatorPreset("ビンテージ・キー", "シンセ", 250, 3000, 20, 1, 20, 0.5, 10, -1, 100, 0),
                new MultibandSaturatorPreset("プラック (強調)", "シンセ", 200, 4000, 10, 0, 30, 1, 40, 2, 100, 0),

                new MultibandSaturatorPreset("マスタリング (軽め)", "マスタリング", 150, 3000, 5, 0, 3, 0, 5, 0, 100, 0),
                new MultibandSaturatorPreset("マスタリング (ウォーム)", "マスタリング", 200, 2500, 10, 0.5, 5, 0, 2, 0, 100, 0),
                new MultibandSaturatorPreset("マスタリング (ブライト)", "マスタリング", 180, 4000, 5, 0, 5, 0, 10, 0.5, 100, 0),
                new MultibandSaturatorPreset("マスタリング (接着)", "マスタリング", 150, 3000, 8, 0, 8, 0, 8, 0, 100, 0),
                new MultibandSaturatorPreset("ラウドネス・マキシマイズ", "マスタリング", 120, 3500, 15, 1, 10, 0.5, 15, 1, 100, -1),
                new MultibandSaturatorPreset("アナログ・ヴァイブ", "マスタリング", 200, 3000, 12, 0.5, 8, 0, 6, 0, 90, 0),

                new MultibandSaturatorPreset("Lo-Fi ラジオ", "特殊効果", 400, 2500, 70, -5, 60, 0, 80, -10, 100, 0),
                new MultibandSaturatorPreset("壊れたスピーカー", "特殊効果", 300, 2000, 100, -2, 100, 0, 0, -20, 100, 0),
                new MultibandSaturatorPreset("電話ボイス", "特殊効果", 500, 3000, 90, -10, 50, 2, 90, -10, 100, 0),
                new MultibandSaturatorPreset("ビットクラッシュ風", "特殊効果", 100, 5000, 80, 1, 70, 1, 90, 1, 60, 0),
                new MultibandSaturatorPreset("インダストリアル", "特殊効果", 150, 2500, 60, 2, 50, 1, 60, 2, 80, -1),
                new MultibandSaturatorPreset("メガホン", "特殊効果", 600, 2000, 0, -20, 80, 5, 0, -20, 100, -2),
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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MIDI.Shape.MidiPianoRoll.Models
{
    internal static class PluginConfigManager
    {
        private static readonly string ConfigFileName = "MidiPianoRoll.ini";
        private static readonly string ConfigDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "Config");
        private static readonly string ConfigFilePath = Path.Combine(ConfigDir, ConfigFileName);

        private static readonly string SectionEffectPopup = "EffectAddPopup";
        private static readonly string KeyWidth = "Width";
        private static readonly string KeyHeight = "Height";
        private static readonly string KeySplitterPos = "SplitterPosition";

        private static readonly string SectionEffectFavorites = "EffectFavorites";
        private static readonly string KeyFavoritePlugins = "Plugins";

        public static double EffectAddPopupWidth { get; set; } = 400;
        public static double EffectAddPopupHeight { get; set; } = 300;
        public static double EffectAddPopupSplitterPosition { get; set; } = 120;

        public static HashSet<string> FavoritePlugins { get; private set; } = new HashSet<string>();

        private static bool _isLoaded = false;

        public static void Load()
        {
            if (_isLoaded) return;
            _isLoaded = true;

            if (!File.Exists(ConfigFilePath)) return;

            var configData = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, string>? currentSection = null;

            try
            {
                var lines = File.ReadAllLines(ConfigFilePath);
                foreach (var line in lines)
                {
                    var trimmedLine = line.Trim();
                    if (trimmedLine.StartsWith(";") || string.IsNullOrEmpty(trimmedLine)) continue;

                    if (trimmedLine.StartsWith("[") && trimmedLine.EndsWith("]"))
                    {
                        var sectionName = trimmedLine.Substring(1, trimmedLine.Length - 2);
                        currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        configData[sectionName] = currentSection;
                    }
                    else if (currentSection != null)
                    {
                        var parts = trimmedLine.Split(new[] { '=' }, 2);
                        if (parts.Length == 2)
                        {
                            currentSection[parts[0].Trim()] = parts[1].Trim();
                        }
                    }
                }

                if (configData.TryGetValue(SectionEffectPopup, out var popupConfig))
                {
                    if (popupConfig.TryGetValue(KeyWidth, out var widthStr) && double.TryParse(widthStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var width))
                        EffectAddPopupWidth = width;

                    if (popupConfig.TryGetValue(KeyHeight, out var heightStr) && double.TryParse(heightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var height))
                        EffectAddPopupHeight = height;

                    if (popupConfig.TryGetValue(KeySplitterPos, out var posStr) && double.TryParse(posStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var pos))
                        EffectAddPopupSplitterPosition = pos;
                }

                if (configData.TryGetValue(SectionEffectFavorites, out var favoritesConfig))
                {
                    if (favoritesConfig.TryGetValue(KeyFavoritePlugins, out var favoritesStr))
                    {
                        FavoritePlugins = new HashSet<string>(favoritesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public static void Save()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }

                var sb = new StringBuilder();
                sb.AppendLine($"; {ConfigFileName} - Config for MidiPianoRoll Plugin");
                sb.AppendLine();
                sb.AppendLine($"[{SectionEffectPopup}]");
                sb.AppendLine($"{KeyWidth}={EffectAddPopupWidth.ToString(CultureInfo.InvariantCulture)}");
                sb.AppendLine($"{KeyHeight}={EffectAddPopupHeight.ToString(CultureInfo.InvariantCulture)}");
                sb.AppendLine($"{KeySplitterPos}={EffectAddPopupSplitterPosition.ToString(CultureInfo.InvariantCulture)}");
                sb.AppendLine();
                sb.AppendLine($"[{SectionEffectFavorites}]");
                sb.AppendLine($"{KeyFavoritePlugins}={string.Join(",", FavoritePlugins)}");

                File.WriteAllText(ConfigFilePath, sb.ToString());
            }
            catch (Exception)
            {
            }
        }
    }
}
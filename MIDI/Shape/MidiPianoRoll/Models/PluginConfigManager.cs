using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using MIDI.Utils;

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

            var ini = new IniFile();
            ini.Load(ConfigFilePath);

            var widthStr = ini.GetValue(SectionEffectPopup, KeyWidth);
            if (double.TryParse(widthStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var width))
                EffectAddPopupWidth = width;

            var heightStr = ini.GetValue(SectionEffectPopup, KeyHeight);
            if (double.TryParse(heightStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var height))
                EffectAddPopupHeight = height;

            var posStr = ini.GetValue(SectionEffectPopup, KeySplitterPos);
            if (double.TryParse(posStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var pos))
                EffectAddPopupSplitterPosition = pos;

            var favoritesStr = ini.GetValue(SectionEffectFavorites, KeyFavoritePlugins);
            if (!string.IsNullOrEmpty(favoritesStr))
            {
                FavoritePlugins = new HashSet<string>(favoritesStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
            }
        }

        public static void Save()
        {
            try
            {
                var ini = new IniFile();

                ini.SetValue(SectionEffectPopup, KeyWidth, EffectAddPopupWidth.ToString(CultureInfo.InvariantCulture));
                ini.SetValue(SectionEffectPopup, KeyHeight, EffectAddPopupHeight.ToString(CultureInfo.InvariantCulture));
                ini.SetValue(SectionEffectPopup, KeySplitterPos, EffectAddPopupSplitterPosition.ToString(CultureInfo.InvariantCulture));

                ini.SetValue(SectionEffectFavorites, KeyFavoritePlugins, string.Join(",", FavoritePlugins));

                ini.Save(ConfigFilePath);
            }
            catch (Exception)
            {
            }
        }
    }
}
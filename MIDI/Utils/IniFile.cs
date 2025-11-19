using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace MIDI.Utils
{
    public class IniFile
    {
        private readonly Dictionary<string, Dictionary<string, string>> _data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private string NormalizeValue(string value)
        {
            if (value.Length >= 2 && ((value.StartsWith("\"") && value.EndsWith("\"")) || (value.StartsWith("'") && value.EndsWith("'"))))
            {
                return value.Substring(1, value.Length - 2);
            }
            return value;
        }

        public void Load(string filePath)
        {
            _data.Clear();

            if (!File.Exists(filePath)) return;

            var lines = File.ReadAllLines(filePath);
            Dictionary<string, string> currentSection = null;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                if (string.IsNullOrEmpty(trimmedLine)) continue;

                var content = trimmedLine;

                var commentIndex = content.IndexOf(';');
                if (commentIndex >= 0)
                {
                    content = content.Substring(0, commentIndex).Trim();
                }

                if (string.IsNullOrEmpty(content)) continue;

                if (content.StartsWith("[") && content.EndsWith("]"))
                {
                    var sectionName = content.Substring(1, content.Length - 2);
                    currentSection = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    _data[sectionName] = currentSection;
                }
                else if (currentSection != null)
                {
                    var parts = content.Split(new[] { '=' }, 2);
                    if (parts.Length == 2)
                    {
                        var key = parts[0].Trim();
                        var rawValue = parts[1].Trim();
                        currentSection[key] = NormalizeValue(rawValue);
                    }
                }
            }
        }

        public string GetValue(string section, string key, string defaultValue = null)
        {
            if (_data.TryGetValue(section, out var sectionData) && sectionData.TryGetValue(key, out var value))
            {
                return value;
            }
            return defaultValue;
        }

        public void SetValue(string section, string key, string value)
        {
            if (!_data.TryGetValue(section, out var sectionData))
            {
                sectionData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _data[section] = sectionData;
            }
            sectionData[key] = value;
        }

        public void Save(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var sb = new StringBuilder();
            foreach (var section in _data)
            {
                sb.AppendLine($"[{section.Key}]");
                foreach (var kvp in section.Value)
                {
                    var value = kvp.Value;
                    if (value.Contains(" ") || value.Contains(","))
                    {
                        value = $"\"{value}\"";
                    }
                    sb.AppendLine($"{kvp.Key}={value}");
                }
                sb.AppendLine();
            }

            File.WriteAllText(filePath, sb.ToString());
        }
    }
}
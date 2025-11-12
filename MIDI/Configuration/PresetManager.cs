using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.Json.Nodes;
using MIDI.Utils;

namespace MIDI
{
    public class PresetCertificate
    {
        public string Issuer { get; set; } = "YMM4-MIDI Plugin";
        public string ComputerName { get; set; } = Environment.MachineName;
        public string UserName { get; set; } = Environment.UserName;
        public string UserDomainName { get; set; } = Environment.UserDomainName;
        public string OSVersion { get; set; } = Environment.OSVersion.ToString();
        public string CreatedAt { get; set; } = DateTime.UtcNow.ToString("o");
    }

    public class PresetManager : IPresetManager
    {
        public static readonly string PresetDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "presets");
        private const string PresetExtension = ".mpp";
        private const string CertificateStartDelimiter = "---BEGIN PRESET CERTIFICATE---";
        private const string CertificateEndDelimiter = "---END PRESET CERTIFICATE---";

        public PresetManager()
        {
            if (!Directory.Exists(PresetDirectory))
            {
                try
                {
                    Directory.CreateDirectory(PresetDirectory);
                }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.PresetDirectoryCreateFailed, ex);
                }
            }
        }

        public async Task<List<string>> GetPresetListAsync()
        {
            return await Task.Run(() =>
            {
                if (!Directory.Exists(PresetDirectory))
                {
                    return new List<string>();
                }
                return Directory.GetFiles(PresetDirectory, $"*{PresetExtension}")
                                .Select(Path.GetFileNameWithoutExtension)
                                .Where(f => f != null)
                                .ToList()!;
            });
        }

        public async Task SavePresetAsync(string presetName, MidiConfiguration settings, List<string>? propertiesToSave = null)
        {
            await Task.Run(async () =>
            {
                var filePath = Path.Combine(PresetDirectory, $"{presetName}{PresetExtension}");

                var certificate = new PresetCertificate();
                var certJson = JsonSerializer.Serialize(certificate, new JsonSerializerOptions { WriteIndented = true });

                var options = new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

                string settingsJson;

                if (propertiesToSave == null || !propertiesToSave.Any())
                {
                    settingsJson = JsonSerializer.Serialize(settings, options);
                }
                else
                {
                    var fullSettingsJson = JsonSerializer.Serialize(settings, options);
                    var fullSettingsNode = JsonNode.Parse(fullSettingsJson)!.AsObject();
                    var settingsToSave = new JsonObject();

                    var namingPolicy = JsonNamingPolicy.CamelCase;

                    foreach (var propName in propertiesToSave)
                    {
                        var jsonPropName = namingPolicy.ConvertName(propName);
                        if (fullSettingsNode.ContainsKey(jsonPropName))
                        {
                            settingsToSave[jsonPropName] = fullSettingsNode[jsonPropName]!.DeepClone();
                        }
                    }
                    settingsJson = settingsToSave.ToJsonString(options);
                }

                var builder = new StringBuilder();
                builder.AppendLine(CertificateStartDelimiter);
                builder.AppendLine(certJson);
                builder.AppendLine(CertificateEndDelimiter);
                builder.AppendLine(settingsJson);

                await File.WriteAllTextAsync(filePath, builder.ToString());
            });
        }

        public (PresetCertificate? certificate, JsonObject? settingsNode) ParsePresetContent(string content)
        {
            var certStartIndex = content.IndexOf(CertificateStartDelimiter);
            var certEndIndex = content.IndexOf(CertificateEndDelimiter);

            if (certStartIndex == -1 || certEndIndex == -1 || certEndIndex < certStartIndex)
            {
                try
                {
                    var settingsNode = JsonNode.Parse(content)?.AsObject();
                    return (null, settingsNode);
                }
                catch
                {
                    return (null, null);
                }
            }

            certStartIndex += CertificateStartDelimiter.Length;
            var certJson = content.Substring(certStartIndex, certEndIndex - certStartIndex).Trim();
            var settingsJson = content.Substring(certEndIndex + CertificateEndDelimiter.Length).Trim();

            try
            {
                PresetCertificate? certificate = JsonSerializer.Deserialize<PresetCertificate>(certJson);
                var finalSettingsNode = JsonNode.Parse(settingsJson)?.AsObject();
                return (certificate, finalSettingsNode);
            }
            catch
            {
                return (null, null);
            }
        }

        public async Task<(PresetCertificate? certificate, JsonObject? settingsNode)> LoadPresetAsync(string presetName)
        {
            var filePath = Path.Combine(PresetDirectory, $"{presetName}{PresetExtension}");
            if (!File.Exists(filePath))
            {
                return (null, null);
            }

            var content = await File.ReadAllTextAsync(filePath);
            return ParsePresetContent(content);
        }

        public async Task DeletePresetAsync(string presetName)
        {
            await Task.Run(() =>
            {
                var filePath = Path.Combine(PresetDirectory, $"{presetName}{PresetExtension}");
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            });
        }
    }
}
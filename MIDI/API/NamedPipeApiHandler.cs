using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MIDI.Configuration.Models;
using MIDI.Utils;

namespace MIDI.API
{
    public class NamedPipeApiHandler
    {
        private readonly MidiSettingsViewModel viewModel;
        private readonly MidiConfiguration config;
        private readonly MidiEditorSettings editorConfig;

        public NamedPipeApiHandler(MidiSettingsViewModel viewModel, MidiConfiguration configuration)
        {
            this.viewModel = viewModel;
            config = configuration;
            editorConfig = MidiEditorSettings.Default;
        }

        public async Task<string> HandleRequest(string jsonRequest)
        {
            string? command = null;
            try
            {
                var request = JsonNode.Parse(jsonRequest);
                command = request?["command"]?.GetValue<string>();
                var parameters = request?["parameters"];

                if (string.IsNullOrEmpty(command))
                {
                    return CreateErrorResponse("Command not specified.");
                }

                object? result = command.ToLower() switch
                {
                    "set_config_value" => SetConfigValue(parameters),
                    "get_config" => GetConfig(),
                    "reload_config" => ReloadConfig(),
                    "save_config" => SaveConfig(),
                    "update_soundfont_rule" => UpdateSoundFontRule(parameters),
                    "update_sfz_map" => UpdateSfzMap(parameters),
                    "get_available_soundfonts" => GetAvailableSoundFonts(),
                    "get_available_sfz" => GetAvailableSfz(),
                    "get_available_wavetables" => GetAvailableWavetables(),
                    "get_plugin_version" => GetPluginVersion(),
                    "clear_audio_cache" => ClearAudioCache(),
                    "refresh_file_lists" => await RefreshFileLists(),
                    _ => CreateErrorResponse($"Unknown command: {command}")
                };

                return CreateSuccessResponse(result);
            }
            catch (JsonException jsonEx)
            {
                return CreateErrorResponse($"Invalid JSON request: {jsonEx.Message}");
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.NamedPipeApiHandlerError, ex, command ?? "N/A");
                return CreateErrorResponse($"Internal server error: {ex.Message}");
            }
        }

        private object SetConfigValue(JsonNode? parameters)
        {
            var key = parameters?["key"]?.GetValue<string>();
            var valueNode = parameters?["value"];

            if (string.IsNullOrEmpty(key) || valueNode == null)
            {
                return new { success = false, message = "Invalid parameters for set_config_value." };
            }

            try
            {
                object targetConfig = key.StartsWith("editor.", StringComparison.OrdinalIgnoreCase) ? editorConfig : config;
                var effectiveKey = key.StartsWith("editor.", StringComparison.OrdinalIgnoreCase) ? key.Substring(7) : key;

                var pathParts = effectiveKey.Split('.');
                object currentObject = targetConfig;
                PropertyInfo? propInfo = null;

                for (int i = 0; i < pathParts.Length; i++)
                {
                    propInfo = currentObject.GetType().GetProperty(pathParts[i], BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                    if (propInfo == null)
                        return new { success = false, message = $"Property not found: {pathParts[i]} in {currentObject.GetType().Name}" };

                    if (i < pathParts.Length - 1)
                    {
                        var nextObject = propInfo.GetValue(currentObject);
                        if (nextObject == null)
                        {
                            try
                            {
                                nextObject = Activator.CreateInstance(propInfo.PropertyType);
                                propInfo.SetValue(currentObject, nextObject);
                            }
                            catch (Exception ex)
                            {
                                return new { success = false, message = $"Failed to create instance for property {pathParts[i]}: {ex.Message}" };
                            }
                        }
                        currentObject = nextObject!;
                    }
                }

                if (propInfo != null)
                {
                    var value = JsonSerializer.Deserialize(valueNode.ToJsonString(), propInfo.PropertyType, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    propInfo.SetValue(currentObject, value);

                    if (targetConfig == config)
                    {
                        config.Save();
                    }
                    else
                    {
                        editorConfig.Save();
                    }
                    return new { success = true };
                }
                return new { success = false, message = "Failed to find the final property to set." };
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.NamedPipeSetConfigError, ex, key);
                return new { success = false, message = $"Error setting value for '{key}': {ex.Message}" };
            }
        }

        private object GetConfig()
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
            var configNode = JsonSerializer.SerializeToNode(config, options)?.AsObject();
            var editorConfigNode = JsonSerializer.SerializeToNode(editorConfig, options)?.AsObject();

            if (configNode != null && editorConfigNode != null)
            {
                configNode["editor"] = editorConfigNode;
            }
            return configNode ?? new JsonObject();
        }
        private object ReloadConfig() { config.Reload(); editorConfig.Reload(); return new { success = true }; }
        private object SaveConfig() { config.SaveSynchronously(); editorConfig.SaveSynchronously(); return new { success = true }; }

        private object UpdateSoundFontRule(JsonNode? parameters)
        {
            var fileName = parameters?["fileName"]?.GetValue<string>();
            var ruleNode = parameters?["rule"];

            if (string.IsNullOrEmpty(fileName) || ruleNode == null)
                return new { success = false, message = "Invalid parameters for update_soundfont_rule." };

            var rule = JsonSerializer.Deserialize<SoundFontRule>(ruleNode.ToJsonString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rule == null)
                return new { success = false, message = "Failed to deserialize rule." };

            rule.SoundFontFile = fileName;

            var existingRule = config.SoundFont.Rules.FirstOrDefault(r => r.SoundFontFile.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (existingRule != null)
            {
                config.SoundFont.Rules.Remove(existingRule);
            }
            config.SoundFont.Rules.Add(rule);
            config.Save();
            return new { success = true };
        }

        private object UpdateSfzMap(JsonNode? parameters)
        {
            var fileName = parameters?["fileName"]?.GetValue<string>();
            var mapNode = parameters?["map"];

            if (string.IsNullOrEmpty(fileName) || mapNode == null)
                return new { success = false, message = "Invalid parameters for update_sfz_map." };

            var map = JsonSerializer.Deserialize<SfzProgramMap>(mapNode.ToJsonString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (map == null)
                return new { success = false, message = "Failed to deserialize map." };

            map.FilePath = fileName;

            var existingMap = config.SFZ.ProgramMaps.FirstOrDefault(m => m.FilePath.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (existingMap != null)
            {
                config.SFZ.ProgramMaps.Remove(existingMap);
            }
            config.SFZ.ProgramMaps.Add(map);
            config.Save();
            return new { success = true };
        }

        private object GetAvailableSoundFonts() => viewModel.SoundFontFiles.Select(f => f.FileName);
        private object GetAvailableSfz() => viewModel.SfzFiles.Select(f => f.FileName);
        private object GetAvailableWavetables() => viewModel.WavetableFiles;
        private object GetPluginVersion() => new { version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() };
        private object ClearAudioCache() { MidiAudioSource.ClearCache(); return new { success = true }; }

        private async Task<object> RefreshFileLists()
        {
            await viewModel.RefreshAllFilesAsync();
            return new { success = true };
        }

        private string CreateSuccessResponse(object? data)
        {
            var response = new { status = "success", data };
            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }

        private string CreateErrorResponse(string message)
        {
            var response = new { status = "error", message };
            return JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        }
    }
}
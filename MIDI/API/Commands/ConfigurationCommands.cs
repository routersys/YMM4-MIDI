using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using MIDI.API.Attributes;
using MIDI.API.Context;
using MIDI.Configuration.Models;
using MIDI.Utils;

namespace MIDI.API.Commands
{
    [ApiCommandGroup("Configuration")]
    public class ConfigurationCommands
    {
        private readonly ApiContext _context;

        public ConfigurationCommands(ApiContext context)
        {
            _context = context;
        }

        [ApiCommand("set_config_value")]
        public object SetConfigValue([ApiParameter("key")] string key, [ApiParameter("value")] JsonNode valueNode)
        {
            if (string.IsNullOrEmpty(key) || valueNode == null)
            {
                return new { success = false, message = "Invalid parameters for set_config_value." };
            }

            try
            {
                object targetConfig = key.StartsWith("editor.", StringComparison.OrdinalIgnoreCase) ? _context.EditorSettings : _context.Configuration;
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

                    if (targetConfig == _context.Configuration)
                    {
                        _context.Configuration.Save();
                    }
                    else
                    {
                        _context.EditorSettings.Save();
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

        [ApiCommand("get_config")]
        public object GetConfig()
        {
            var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
            var configNode = JsonSerializer.SerializeToNode(_context.Configuration, options)?.AsObject();
            var editorConfigNode = JsonSerializer.SerializeToNode(_context.EditorSettings, options)?.AsObject();

            if (configNode != null && editorConfigNode != null)
            {
                configNode["editor"] = editorConfigNode;
            }
            return configNode ?? new JsonObject();
        }

        [ApiCommand("reload_config")]
        public object ReloadConfig()
        {
            _context.Configuration.Reload();
            _context.EditorSettings.Reload();
            return new { success = true };
        }

        [ApiCommand("save_config")]
        public object SaveConfig()
        {
            _context.Configuration.SaveSynchronously();
            _context.EditorSettings.SaveSynchronously();
            return new { success = true };
        }
    }
}
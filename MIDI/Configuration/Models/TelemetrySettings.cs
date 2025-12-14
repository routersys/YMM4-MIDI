using System;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Encodings.Web;
using System.ComponentModel;
using YukkuriMovieMaker.Plugin;
using MIDI.Utils;
using System.Threading;
using System.Threading.Tasks;

namespace MIDI.Configuration.Models
{
    public class TelemetrySettings : SettingsBase<TelemetrySettings>
    {
        public override string Name => "Telemetry設定";
        public override SettingsCategory Category => SettingsCategory.None;
        public override bool HasSettingView => false;

        [JsonIgnore]
        public override object? SettingView => null;

        private static string PluginDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        private static string ConfigDir => Path.Combine(PluginDir, "Config");
        private static string ConfigPath => Path.Combine(ConfigDir, "TelemetryConfig.json");

        [JsonIgnore]
        private static readonly Timer _saveTimer = new Timer(_ => _ = SaveAsync());

        private bool _isEnabled = false;
        [DisplayName("テレメトリを有効にする")]
        [Description("匿名の使用状況データを送信します。")]
        public bool IsEnabled { get => _isEnabled; set => Set(ref _isEnabled, value); }

        private bool _hasAskedConsent = false;
        [Browsable(false)]
        public bool HasAskedConsent { get => _hasAskedConsent; set => Set(ref _hasAskedConsent, value); }

        public override void Initialize()
        {
            Load();
        }

        public void Load()
        {
            if (!File.Exists(ConfigPath))
            {
                var defaultSettings = new TelemetrySettings();
                defaultSettings.SaveSynchronously();
                CopyFrom(defaultSettings);
            }
            else
            {
                try
                {
                    Logger.Info(LogMessages.LoadConfigFile, ConfigPath);
                    var jsonString = File.ReadAllText(ConfigPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        AllowTrailingCommas = true,
                        PropertyNameCaseInsensitive = true
                    };

                    var loadedSettings = JsonSerializer.Deserialize<TelemetrySettings>(jsonString, options);
                    if (loadedSettings != null)
                    {
                        CopyFrom(loadedSettings);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.ConfigReadError, ex, ex.Message);
                }
            }
        }

        public void CopyFrom(TelemetrySettings source)
        {
            IsEnabled = source.IsEnabled;
            HasAskedConsent = source.HasAskedConsent;
        }

        public new void Save()
        {
            _saveTimer.Change(500, Timeout.Infinite);
        }

        private static async Task SaveAsync()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var jsonString = JsonSerializer.Serialize(Default, options);
                await File.WriteAllTextAsync(ConfigPath, jsonString);
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.ConfigSaveError, ex, ex.Message);
            }
        }

        public void SaveSynchronously()
        {
            _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var jsonString = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigPath, jsonString);
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.ConfigSaveError, ex, ex.Message);
            }
        }
    }
}
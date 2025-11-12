using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using MIDI.TextCompletion.Models;
using MIDI.TextCompletion.Services;
using MIDI.TextCompletion;
using YukkuriMovieMaker.Plugin;
using MIDI.Utils;

namespace MIDI.TextCompletion
{
    public class MidiMusicCompletionSettings : SettingsBase<MidiMusicCompletionSettings>
    {
        public override SettingsCategory Category => SettingsCategory.Voice;
        public override string Name => "MIDI音楽用語補完 設定";
        public override bool HasSettingView => false;

        public override object SettingView => new MidiMusicCompletionSettingsView { DataContext = new ViewModels.MidiMusicCompletionSettingsViewModel(this) };

        private bool enableFuzzySearch = true;
        public bool EnableFuzzySearch { get => enableFuzzySearch; set => Set(ref enableFuzzySearch, value); }

        private int maxSuggestions = 5;
        public int MaxSuggestions { get => maxSuggestions; set => Set(ref maxSuggestions, Math.Clamp(value, 1, 10)); }

        private bool enableDescriptionCompletion = false;
        public bool EnableDescriptionCompletion { get => enableDescriptionCompletion; set => Set(ref enableDescriptionCompletion, value); }


        private static readonly string PluginDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        private static readonly string ConfigFileName = "MidiMusicTerminology.json";
        private static readonly string OldJsonFilePath = Path.Combine(PluginDir, ConfigFileName);

        private static string ConfigDir
        {
            get
            {
                return Path.Combine(PluginDir, "Config");
            }
        }
        private static readonly string JsonFilePath = Path.Combine(ConfigDir, ConfigFileName);

        private readonly ITerminologyPersistence _persistenceService = new JsonTerminologyPersistence();

        private ObservableCollection<MusicTerm> termsList = new ObservableCollection<MusicTerm>();

        [System.Text.Json.Serialization.JsonIgnore]
        public ObservableCollection<MusicTerm> TermsList => termsList;

        public override void Initialize()
        {
            MigrateSettingsFile();
            LoadTermsListFromFile();
        }

        private void MigrateSettingsFile()
        {
            if (File.Exists(OldJsonFilePath))
            {
                try
                {
                    if (!Directory.Exists(ConfigDir))
                    {
                        Directory.CreateDirectory(ConfigDir);
                    }

                    if (!File.Exists(JsonFilePath))
                    {
                        File.Move(OldJsonFilePath, JsonFilePath);
                        Logger.Info($"Moved terminology file from '{OldJsonFilePath}' to '{JsonFilePath}'.");
                    }
                    else
                    {
                        File.Delete(OldJsonFilePath);
                        Logger.Info($"Deleted old terminology file at '{OldJsonFilePath}' as new one already exists.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to migrate terminology file from '{OldJsonFilePath}' to '{JsonFilePath}'.", ex);
                }
            }
        }

        private void LoadTermsListFromFile()
        {
            List<MusicTerm>? loadedTerms = null;
            if (File.Exists(JsonFilePath))
            {
                try
                {
                    loadedTerms = _persistenceService.Load(JsonFilePath);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to load terminology file from '{JsonFilePath}'.", ex);
                    loadedTerms = null;
                }
            }

            if (loadedTerms != null && loadedTerms.Any())
            {
                termsList = new ObservableCollection<MusicTerm>(loadedTerms);
            }
            else
            {
                termsList = TerminologyData.GetDefaultTerms();
                SaveTermsListToFile();
            }
        }

        public void SaveTermsListToFile()
        {
            try
            {
                if (termsList != null)
                {
                    if (!Directory.Exists(ConfigDir))
                    {
                        Directory.CreateDirectory(ConfigDir);
                    }
                    _persistenceService.Save(JsonFilePath, termsList.ToList());
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to save terminology file to '{JsonFilePath}'.", ex);
            }
        }
    }
}
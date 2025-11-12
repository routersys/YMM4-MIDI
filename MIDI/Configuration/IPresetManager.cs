using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace MIDI
{
    public interface IPresetManager
    {
        Task<List<string>> GetPresetListAsync();
        Task SavePresetAsync(string presetName, MidiConfiguration settings, List<string>? propertiesToSave = null);
        (PresetCertificate? certificate, JsonObject? settingsNode) ParsePresetContent(string content);
        Task<(PresetCertificate? certificate, JsonObject? settingsNode)> LoadPresetAsync(string presetName);
        Task DeletePresetAsync(string presetName);
    }
}
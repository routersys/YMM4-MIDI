using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using MIDI.API.Attributes;
using MIDI.API.Context;
using MIDI.Configuration.Models;
using MIDI.Utils;

namespace MIDI.API.Commands
{
    [ApiCommandGroup("FileResources")]
    public class FileCommands
    {
        private readonly ApiContext _context;

        public FileCommands(ApiContext context)
        {
            _context = context;
        }

        [ApiCommand("update_soundfont_rule")]
        public object UpdateSoundFontRule([ApiParameter("fileName")] string fileName, [ApiParameter("rule")] JsonNode ruleNode)
        {
            if (string.IsNullOrEmpty(fileName) || ruleNode == null)
                return new { success = false, message = "Invalid parameters for update_soundfont_rule." };

            var rule = JsonSerializer.Deserialize<SoundFontRule>(ruleNode.ToJsonString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (rule == null)
                return new { success = false, message = "Failed to deserialize rule." };

            rule.SoundFontFile = fileName;

            var existingRule = _context.Configuration.SoundFont.Rules.FirstOrDefault(r => r.SoundFontFile.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (existingRule != null)
            {
                _context.Configuration.SoundFont.Rules.Remove(existingRule);
            }
            _context.Configuration.SoundFont.Rules.Add(rule);
            _context.Configuration.Save();
            return new { success = true };
        }

        [ApiCommand("update_sfz_map")]
        public object UpdateSfzMap([ApiParameter("fileName")] string fileName, [ApiParameter("map")] JsonNode mapNode)
        {
            if (string.IsNullOrEmpty(fileName) || mapNode == null)
                return new { success = false, message = "Invalid parameters for update_sfz_map." };

            var map = JsonSerializer.Deserialize<SfzProgramMap>(mapNode.ToJsonString(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (map == null)
                return new { success = false, message = "Failed to deserialize map." };

            map.FilePath = fileName;

            var existingMap = _context.Configuration.SFZ.ProgramMaps.FirstOrDefault(m => m.FilePath.Equals(fileName, StringComparison.OrdinalIgnoreCase));
            if (existingMap != null)
            {
                _context.Configuration.SFZ.ProgramMaps.Remove(existingMap);
            }
            _context.Configuration.SFZ.ProgramMaps.Add(map);
            _context.Configuration.Save();
            return new { success = true };
        }

        [ApiCommand("get_available_soundfonts")]
        public object GetAvailableSoundFonts() => _context.ViewModel.SoundFontFiles.Select(f => f.FileName);

        [ApiCommand("get_available_sfz")]
        public object GetAvailableSfz() => _context.ViewModel.SfzFiles.Select(f => f.FileName);

        [ApiCommand("get_available_wavetables")]
        public object GetAvailableWavetables() => _context.ViewModel.WavetableFiles;

        [ApiCommand("refresh_file_lists")]
        public async Task<object> RefreshFileLists()
        {
            await _context.ViewModel.RefreshAllFilesAsync();
            return new { success = true };
        }
    }
}
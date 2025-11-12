using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace MIDI.Shape.MidiPianoRoll.Models
{
    public interface IResourcePackService
    {
        Task<PianoRollResourcePack> LoadAsync(string filePath);
        Task SaveAsync(string filePath, PianoRollResourcePack config);
    }

    public class ResourcePackService : IResourcePackService
    {
        private static readonly JsonSerializerOptions _options = new()
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public async Task<PianoRollResourcePack> LoadAsync(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return new PianoRollResourcePack();
            }

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                return JsonSerializer.Deserialize<PianoRollResourcePack>(json, _options) ?? new PianoRollResourcePack();
            }
            catch
            {

                return new PianoRollResourcePack();
            }
        }

        public async Task SaveAsync(string filePath, PianoRollResourcePack config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, _options);
                await File.WriteAllTextAsync(filePath, json);
            }
            catch
            {

            }
        }
    }
}
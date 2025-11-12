using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using MIDI.TextCompletion.Models;

namespace MIDI.TextCompletion.Services
{
    public class JsonTerminologyPersistence : ITerminologyPersistence
    {
        private readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public List<MusicTerm> Load(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var terms = JsonSerializer.Deserialize<List<MusicTerm>>(json, _options);
            return terms ?? new List<MusicTerm>();
        }

        public void Save(string filePath, List<MusicTerm> terms)
        {
            var json = JsonSerializer.Serialize(terms, _options);
            File.WriteAllText(filePath, json);
        }
    }
}
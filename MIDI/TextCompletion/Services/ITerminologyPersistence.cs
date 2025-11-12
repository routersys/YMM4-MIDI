using System.Collections.Generic;
using MIDI.TextCompletion.Models;

namespace MIDI.TextCompletion.Services
{
    public interface ITerminologyPersistence
    {
        List<MusicTerm> Load(string filePath);
        void Save(string filePath, List<MusicTerm> terms);
    }
}
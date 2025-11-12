using System.Collections.Generic;

namespace MIDI.Voice.Languages.Interface
{
    public interface ILanguageParser
    {
        string LanguageName { get; }
        int CheckConfidence(string inputText);
        IParseResult Parse(string inputText);
        IReadOnlyDictionary<long, string> GetErrorDefinitions();
    }
}
using System.Collections.Generic;

namespace MIDI.Voice.Languages.Interface
{
    public interface IParseResult
    {
        bool IsSuccess { get; }
        object? Output { get; }
        IReadOnlyList<IParseError> Errors { get; }
        string HandledByParserName { get; }
    }
}
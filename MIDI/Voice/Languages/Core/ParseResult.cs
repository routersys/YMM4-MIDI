using MIDI.Voice.Languages.Interface;
using System.Collections.Generic;

namespace MIDI.Voice.Languages.Core
{
    public class ParseResult : IParseResult
    {
        public bool IsSuccess => Output != null && Errors.Count == 0;
        public object? Output { get; }
        public IReadOnlyList<IParseError> Errors { get; }
        public string HandledByParserName { get; }

        public ParseResult(object? output, string parserName, IReadOnlyList<IParseError> errors)
        {
            Output = output;
            HandledByParserName = parserName;
            Errors = errors ?? new List<IParseError>();
        }
    }
}
using MIDI.Voice.Languages.Interface;

namespace MIDI.Voice.Languages.Core
{
    public class ParseError : IParseError
    {
        public string ErrorCode { get; }
        public string ErrorKey { get; }
        public object[] MessageParameters { get; }
        public int Line { get; }
        public int Column { get; }
        public int Length { get; }

        public ParseError(string errorKey, char languagePrefix, int line, int column, int length, params object[] messageParameters)
        {
            ErrorKey = errorKey;
            ErrorCode = ErrorCodeGenerator.Generate(languagePrefix, errorKey);
            Line = line;
            Column = column;
            Length = length;
            MessageParameters = messageParameters ?? System.Array.Empty<object>();
        }
    }
}
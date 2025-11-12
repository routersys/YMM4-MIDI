using System;

namespace MIDI.Voice.SUSL.Errors
{
    public class SuslException : Exception
    {
        public string ErrorCode { get; }
        public int Line { get; }
        public int Column { get; }

        public SuslException((string Code, string Message) error, int line, int column, params object[] args)
            : base(FormatMessage(error.Code, error.Message, line, column, args))
        {
            ErrorCode = error.Code;
            Line = line;
            Column = column;
        }

        private static string FormatMessage(string code, string message, int line, int column, params object[] args)
        {
            string formattedMessage;
            try
            {
                formattedMessage = string.Format(message, args);
            }
            catch (FormatException)
            {
                formattedMessage = message;
            }
            return $"[{code}] (Line: {line}, Col: {column}) {formattedMessage}";
        }
    }
}
using System;

namespace MIDI.Voice.EMEL.Errors
{
    public class EmelException : Exception
    {
        public int Line { get; }
        public int Column { get; }
        public EmelErrorCode ErrorCode { get; }

        public EmelException(EmelErrorCode code, int line, int column, params object[] args)
            : base(EmelErrors.Format(code, args))
        {
            ErrorCode = code;
            Line = line;
            Column = column;
        }

        public EmelException(string message, int line, int column)
            : base(message)
        {
            ErrorCode = EmelErrorCode.Runtime_Unexpected;
            Line = line;
            Column = column;
        }
    }
}
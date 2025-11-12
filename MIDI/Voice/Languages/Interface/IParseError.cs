namespace MIDI.Voice.Languages.Interface
{
    public interface IParseError
    {
        string ErrorCode { get; }
        string ErrorKey { get; }
        object[] MessageParameters { get; }
        int Line { get; }
        int Column { get; }
        int Length { get; }
    }
}
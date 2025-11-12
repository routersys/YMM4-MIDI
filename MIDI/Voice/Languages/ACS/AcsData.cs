namespace MIDI.Voice.Languages.ACS
{
    public enum CommandType { ControlChange, ProgramChange, PitchBend, ChannelPressure, TimingClock, Start, Continue, Stop }

    public class MidiCommandData
    {
        public CommandType Type { get; set; }
        public int Channel { get; set; } = 1;
        public int Data1 { get; set; }
        public int Data2 { get; set; }
        public int Track { get; set; } = 1;
        public double AbsoluteTimeSeconds { get; set; } = 0.0;
    }

    public class NoteData
    {
        public string Pitch { get; set; } = "C4";
        public float DurationSeconds { get; set; } = 1.0f;
        public float Volume { get; set; } = 0.8f;
        public int MidiNoteNumber { get; set; } = 60;
        public float DelaySeconds { get; set; } = 0.0f;
        public int Channel { get; set; } = 1;
        public int Track { get; set; } = 1;
        public double AbsoluteTimeSeconds { get; set; } = 0.0;
        public int PitchBend { get; set; } = 8192;
        public int Modulation { get; set; } = 0;
        public int Expression { get; set; } = 127;
        public int Pan { get; set; } = 64;
        public int ChannelPressure { get; set; } = 0;
        public bool IsError { get; set; } = false;

        public static NoteData CreateErrorNote(int track = 1, double time = 0.0, float duration = 0.1f)
        {
            return new NoteData { Track = track, AbsoluteTimeSeconds = time, Pitch = "ERR", MidiNoteNumber = 69, DurationSeconds = duration, Volume = 0.7f, IsError = true };
        }
    }
}
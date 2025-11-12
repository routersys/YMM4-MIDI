using System;

namespace MIDI.Shape.MidiPianoRoll.Models
{
    public record NoteEventInfo(
        int NoteNumber,
        int Channel,
        long StartTicks,
        long DurationTicks,
        TimeSpan StartTime,
        TimeSpan Duration
    );
}
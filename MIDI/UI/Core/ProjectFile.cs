using MessagePack;
using System;
using System.Collections.Generic;

namespace MIDI.Core
{
    [MessagePackObject]
    public class ProjectFile
    {
        [Key(0)]
        public string MidiFilePath { get; set; } = string.Empty;

        [Key(1)]
        public List<NoteChange> NoteChanges { get; set; } = new List<NoteChange>();

        [Key(2)]
        public List<TempoChange> TempoChanges { get; set; } = new List<TempoChange>();

        [Key(3)]
        public List<ControlChangeOperation> ControlChangeOperations { get; set; } = new List<ControlChangeOperation>();

        [Key(4)]
        public List<FlagOperation> FlagOperations { get; set; } = new List<FlagOperation>();
    }

    [MessagePackObject]
    public class NoteChange
    {
        [Key(0)]
        public long OriginalStartTicks { get; set; }
        [Key(1)]
        public int OriginalNoteNumber { get; set; }

        [Key(2)]
        public long? NewStartTicks { get; set; }
        [Key(3)]
        public long? NewDurationTicks { get; set; }
        [Key(4)]
        public int? NewNoteNumber { get; set; }
        [Key(5)]
        public int? NewVelocity { get; set; }
        [Key(6)]
        public int? NewChannel { get; set; }
        [Key(7)]
        public int? NewCentOffset { get; set; }
        [Key(8)]
        public bool IsAdded { get; set; }
        [Key(9)]
        public bool IsDeleted { get; set; }
    }

    [MessagePackObject]
    public class TempoChange
    {
        [Key(0)]
        public long OriginalAbsoluteTime { get; set; }
        [Key(1)]
        public double OriginalBpm { get; set; }

        [Key(2)]
        public long? NewAbsoluteTime { get; set; }
        [Key(3)]
        public double? NewBpm { get; set; }
        [Key(4)]
        public bool IsAdded { get; set; }
        [Key(5)]
        public bool IsDeleted { get; set; }
    }

    [MessagePackObject]
    public class ControlChangeOperation
    {
        [Key(0)]
        public long AbsoluteTime { get; set; }
        [Key(1)]
        public int Channel { get; set; }
        [Key(2)]
        public int Controller { get; set; }
        [Key(3)]
        public int OriginalValue { get; set; }
        [Key(4)]
        public int NewValue { get; set; }
        [Key(5)]
        public bool IsAdded { get; set; }
        [Key(6)]
        public bool IsDeleted { get; set; }
    }

    [MessagePackObject]
    public class FlagOperation
    {
        [Key(0)]
        public string Name { get; set; } = string.Empty;
        [Key(1)]
        public TimeSpan OriginalTime { get; set; }
        [Key(2)]
        public TimeSpan NewTime { get; set; }
        [Key(3)]
        public string? NewName { get; set; }
        [Key(4)]
        public bool IsAdded { get; set; }
        [Key(5)]
        public bool IsDeleted { get; set; }
    }
}
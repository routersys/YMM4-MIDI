using System;
using System.Collections.Generic;
using System.Linq;
using MIDI.Shape.MidiPianoRoll.Models;
using NAudio.Midi;
using YukkuriMovieMaker.Commons;

namespace MIDI.Shape.MidiPianoRoll.Models
{
    public class MidiDataManager
    {
        private readonly object _midiDataLock = new object();
        private MidiFile? _midiFile;
        private List<TempoEvent>? _tempoMap;
        private List<NoteEventInfo> _notes = new();
        private string _loadedMidiPath = "";

        public MidiFile? MidiFile => _midiFile;
        public List<TempoEvent>? TempoMap => _tempoMap;
        public List<NoteEventInfo> Notes => _notes;
        public TimeSpan MidiDuration { get; private set; } = TimeSpan.Zero;

        public void LoadMidiData(string midiFilePath, bool forceReload = false)
        {
            if (!forceReload && (_loadedMidiPath == midiFilePath || string.IsNullOrEmpty(midiFilePath)))
            {
                if (string.IsNullOrEmpty(midiFilePath))
                {
                    lock (_midiDataLock)
                    {
                        _midiFile = null;
                        _tempoMap = null;
                        _notes.Clear();
                        MidiDuration = TimeSpan.Zero;
                        _loadedMidiPath = "";
                    }
                }
                return;
            }

            if (string.IsNullOrEmpty(midiFilePath))
            {
                lock (_midiDataLock)
                {
                    _midiFile = null;
                    _tempoMap = null;
                    _notes.Clear();
                    MidiDuration = TimeSpan.Zero;
                    _loadedMidiPath = "";
                }
                return;
            }

            try
            {
                var midiFile = new MidiFile(midiFilePath, false);
                var tempoMap = MidiProcessor.ExtractTempoMap(midiFile, MidiConfiguration.Default);
                var notes = ExtractNotes(midiFile, tempoMap);
                var loadedMidiPath = midiFilePath;

                long maxTicks = 0;
                if (midiFile.Events != null)
                {
                    maxTicks = midiFile.Events.SelectMany(track => track)
                                            .Select(ev => ev.AbsoluteTime)
                                            .DefaultIfEmpty(0)
                                            .Max();
                }
                var midiDuration = MidiProcessor.TicksToTimeSpan(maxTicks, midiFile.DeltaTicksPerQuarterNote, tempoMap);

                lock (_midiDataLock)
                {
                    _midiFile = midiFile;
                    _tempoMap = tempoMap;
                    _notes = notes;
                    MidiDuration = midiDuration;
                    _loadedMidiPath = loadedMidiPath;
                }
            }
            catch (Exception)
            {
                lock (_midiDataLock)
                {
                    _midiFile = null;
                    _tempoMap = null;
                    _notes.Clear();
                    MidiDuration = TimeSpan.Zero;
                    _loadedMidiPath = "";
                }
            }
        }

        private List<NoteEventInfo> ExtractNotes(MidiFile midiFile, List<TempoEvent> tempoMap)
        {
            var noteList = new List<NoteEventInfo>();
            foreach (var track in midiFile.Events)
            {
                foreach (var ev in track.OfType<NoteOnEvent>())
                {
                    if (ev.OffEvent != null && ev.NoteLength > 0)
                    {
                        var startTime = MidiProcessor.TicksToTimeSpan(ev.AbsoluteTime, midiFile.DeltaTicksPerQuarterNote, tempoMap);
                        var endTime = MidiProcessor.TicksToTimeSpan(ev.OffEvent.AbsoluteTime, midiFile.DeltaTicksPerQuarterNote, tempoMap);
                        noteList.Add(new NoteEventInfo(ev.NoteNumber, ev.Channel, ev.AbsoluteTime, ev.NoteLength, startTime, endTime - startTime));
                    }
                }
            }
            return noteList.OrderBy(n => n.StartTicks).ToList();
        }

        public HashSet<int> GetPlayingNotes(TimeSpan currentTime)
        {
            List<NoteEventInfo> currentNotes;
            lock (_midiDataLock)
            {
                currentNotes = _notes;
            }

            return currentNotes
               .Where(n => currentTime >= n.StartTime && currentTime < n.StartTime + n.Duration)
               .Select(n => n.NoteNumber)
               .ToHashSet();
        }

        public List<NoteEventInfo> GetPlayingNoteInfos(TimeSpan currentTime)
        {
            List<NoteEventInfo> currentNotes;
            lock (_midiDataLock)
            {
                currentNotes = _notes;
            }

            return currentNotes
               .Where(n => currentTime >= n.StartTime && currentTime < n.StartTime + n.Duration)
               .ToList();
        }

        public List<NoteEventInfo> GetNotesInRange(TimeSpan renderStartTime, TimeSpan renderEndTime)
        {
            List<NoteEventInfo> currentNotes;
            lock (_midiDataLock)
            {
                currentNotes = _notes;
            }

            return currentNotes
                .Where(n => (n.StartTime + n.Duration) >= renderStartTime && n.StartTime <= renderEndTime)
                .ToList();
        }
    }
}
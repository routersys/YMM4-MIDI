using System;
using System.Collections.Generic;
using System.Linq;
using Manufaktura.Controls.Model;
using Manufaktura.Music.Model;
using NAudio.Midi;

namespace MIDI.Shape.MidiStaff.Models
{
    public interface IMidiElement
    {
        double StartTime { get; }
        double EndTime { get; }
    }

    public class MidiNote : Note, IMidiElement
    {
        public double StartTime { get; set; }
        public double EndTime { get; set; }

        public MidiNote(Pitch pitch, RhythmicDuration duration) : base(pitch, duration)
        {
        }
    }

    public class MidiRest : Rest, IMidiElement
    {
        public double StartTime { get; set; }
        public double EndTime { get; set; }

        public MidiRest(RhythmicDuration duration) : base(duration)
        {
        }
    }

    public class MidiToScoreConverter
    {
        private string _lastPath = "";
        private int _lastTrackIndex = -1;
        private int _lastMeasuresPerLine = -1;
        private bool _lastIsGrandStaff = false;
        private int _lastSplitPoint = -1;
        private Score? _cachedScore;

        public Score? Convert(string midiPath, int trackIndex, int measuresPerLine, bool isGrandStaff, int splitPoint)
        {
            if (string.IsNullOrEmpty(midiPath) || !System.IO.File.Exists(midiPath)) return null;
            if (_lastPath == midiPath && _lastTrackIndex == trackIndex && _lastMeasuresPerLine == measuresPerLine &&
                _lastIsGrandStaff == isGrandStaff && _lastSplitPoint == splitPoint && _cachedScore != null)
                return _cachedScore;

            try
            {
                var midiFile = new MidiFile(midiPath, false);
                var score = new Score();

                var tempoEvents = new List<TempoEvent>();
                foreach (var track in midiFile.Events)
                {
                    tempoEvents.AddRange(track.OfType<TempoEvent>());
                }
                tempoEvents = tempoEvents.OrderBy(e => e.AbsoluteTime).ToList();

                var events = new List<NoteOnEvent>();
                if (trackIndex > 0 && trackIndex <= midiFile.Tracks)
                {
                    events.AddRange(midiFile.Events[trackIndex - 1].OfType<NoteOnEvent>().Where(n => n.Velocity > 0));
                }
                else
                {
                    for (int t = 0; t < midiFile.Tracks; t++)
                    {
                        events.AddRange(midiFile.Events[t].OfType<NoteOnEvent>().Where(n => n.Velocity > 0));
                    }
                }

                int ticksPerQuarter = midiFile.DeltaTicksPerQuarterNote;

                if (isGrandStaff)
                {
                    var trebleStaff = new Staff();
                    trebleStaff.Elements.Add(Clef.Treble);
                    trebleStaff.Elements.Add(new Key(0));
                    trebleStaff.Elements.Add(TimeSignature.CommonTime);
                    score.Staves.Add(trebleStaff);

                    var bassStaff = new Staff();
                    bassStaff.Elements.Add(Clef.Bass);
                    bassStaff.Elements.Add(new Key(0));
                    bassStaff.Elements.Add(TimeSignature.CommonTime);
                    score.Staves.Add(bassStaff);

                    var trebleEvents = events.Where(e => e.NoteNumber >= splitPoint).OrderBy(e => e.AbsoluteTime).ToList();
                    var bassEvents = events.Where(e => e.NoteNumber < splitPoint).OrderBy(e => e.AbsoluteTime).ToList();

                    ProcessEvents(trebleStaff, trebleEvents, tempoEvents, ticksPerQuarter, measuresPerLine);
                    ProcessEvents(bassStaff, bassEvents, tempoEvents, ticksPerQuarter, measuresPerLine);

                    trebleStaff.Elements.Add(new Barline(BarlineStyle.LightHeavy));
                    bassStaff.Elements.Add(new Barline(BarlineStyle.LightHeavy));
                }
                else
                {
                    var staff = new Staff();
                    staff.Elements.Add(Clef.Treble);
                    staff.Elements.Add(new Key(0));
                    staff.Elements.Add(TimeSignature.CommonTime);
                    score.Staves.Add(staff);

                    var sortedEvents = events.OrderBy(e => e.AbsoluteTime).ToList();
                    ProcessEvents(staff, sortedEvents, tempoEvents, ticksPerQuarter, measuresPerLine);

                    staff.Elements.Add(new Barline(BarlineStyle.LightHeavy));
                }

                _lastPath = midiPath;
                _lastTrackIndex = trackIndex;
                _lastMeasuresPerLine = measuresPerLine;
                _lastIsGrandStaff = isGrandStaff;
                _lastSplitPoint = splitPoint;
                _cachedScore = score;
                return score;
            }
            catch
            {
                return null;
            }
        }

        private void ProcessEvents(Staff staff, List<NoteOnEvent> noteEvents, List<TempoEvent> tempoEvents, int ticksPerQuarter, int measuresPerLine)
        {
            long currentTick = 0;
            long currentMeasureTick = 0;
            int measureCount = 0;
            long ticksPerMeasure = (long)ticksPerQuarter * 4L;

            int i = 0;
            while (i < noteEvents.Count)
            {
                long startTick = noteEvents[i].AbsoluteTime;

                if (startTick > currentTick)
                {
                    long restDurationTicks = startTick - currentTick;
                    AddRest(staff, restDurationTicks, ticksPerQuarter, ticksPerMeasure, ref currentTick, ref currentMeasureTick, measuresPerLine, ref measureCount, tempoEvents);
                }

                var chordNotes = new List<NoteOnEvent>();
                while (i < noteEvents.Count && noteEvents[i].AbsoluteTime == startTick)
                {
                    chordNotes.Add(noteEvents[i]);
                    i++;
                }

                long maxDuration = chordNotes.Max(n => n.NoteLength);
                var pitches = chordNotes.Select(n => GetPitchFromMidiNumber(n.NoteNumber)).OrderByDescending(p => p.MidiPitch).ToList();

                AddNotes(staff, pitches, maxDuration, ticksPerQuarter, ticksPerMeasure, ref currentTick, ref currentMeasureTick, measuresPerLine, ref measureCount, tempoEvents, ticksPerQuarter);
            }
        }

        private void AddRest(Staff staff, long totalTicks, int ticksPerQuarter, long ticksPerMeasure, ref long currentTick, ref long currentMeasureTick, int measuresPerLine, ref int measureCount, List<TempoEvent> tempoEvents)
        {
            long remaining = totalTicks;
            while (remaining > 0)
            {
                long ticksToBar = (currentMeasureTick + ticksPerMeasure) - currentTick;
                long ticks = Math.Min(remaining, ticksToBar);

                var duration = GetDurationFromTicks(ticks, ticksPerQuarter);

                double startTime = CalculateTime(currentTick, tempoEvents, ticksPerQuarter);
                double endTime = CalculateTime(currentTick + ticks, tempoEvents, ticksPerQuarter);

                staff.Elements.Add(new MidiRest(duration) { StartTime = startTime, EndTime = endTime });

                currentTick += ticks;
                remaining -= ticks;

                if (currentTick >= currentMeasureTick + ticksPerMeasure)
                {
                    staff.Elements.Add(new Barline());
                    currentMeasureTick += ticksPerMeasure;
                    measureCount++;
                    if (measureCount % measuresPerLine == 0)
                    {
                        staff.Elements.Add(new PrintSuggestion { IsSystemBreak = true });
                    }
                }
            }
        }

        private void AddNotes(Staff staff, List<Pitch> pitches, long totalTicks, int ticksPerQuarter, long ticksPerMeasure, ref long currentTick, ref long currentMeasureTick, int measuresPerLine, ref int measureCount, List<TempoEvent> tempoEvents, int tpq)
        {
            long remaining = totalTicks;
            List<MidiNote>? prevNotes = null;

            while (remaining > 0)
            {
                long ticksToBar = (currentMeasureTick + ticksPerMeasure) - currentTick;
                long ticks = Math.Min(remaining, ticksToBar);

                var duration = GetDurationFromTicks(ticks, ticksPerQuarter);
                double startTime = CalculateTime(currentTick, tempoEvents, tpq);
                double endTime = CalculateTime(currentTick + ticks, tempoEvents, tpq);

                var notes = pitches.Select(p => new MidiNote(p, duration)
                {
                    StemDirection = VerticalDirection.Up,
                    StartTime = startTime,
                    EndTime = endTime
                }).ToList();

                foreach (var note in notes)
                {
                    staff.Elements.Add(note);
                }

                if (prevNotes != null)
                {
                    foreach (var n in notes) n.TieType = NoteTieType.Stop;
                }

                currentTick += ticks;
                remaining -= ticks;
                prevNotes = notes;

                if (remaining > 0)
                {
                    foreach (var n in notes) n.TieType = NoteTieType.Start;

                    staff.Elements.Add(new Barline());
                    currentMeasureTick += ticksPerMeasure;
                    measureCount++;
                    if (measureCount % measuresPerLine == 0)
                    {
                        staff.Elements.Add(new PrintSuggestion { IsSystemBreak = true });
                    }
                }
                else if (currentTick >= currentMeasureTick + ticksPerMeasure)
                {
                    staff.Elements.Add(new Barline());
                    currentMeasureTick += ticksPerMeasure;
                    measureCount++;
                    if (measureCount % measuresPerLine == 0)
                    {
                        staff.Elements.Add(new PrintSuggestion { IsSystemBreak = true });
                    }
                }
            }
        }

        private double CalculateTime(long tick, List<TempoEvent> tempoEvents, int ticksPerQuarter)
        {
            double time = 0;
            long lastTick = 0;
            double currentMspq = 500000;

            foreach (var tempo in tempoEvents)
            {
                if (tempo.AbsoluteTime >= tick) break;
                time += (tempo.AbsoluteTime - lastTick) * (currentMspq / 1000.0 / 1000.0 / ticksPerQuarter);
                currentMspq = tempo.MicrosecondsPerQuarterNote;
                lastTick = tempo.AbsoluteTime;
            }
            time += (tick - lastTick) * (currentMspq / 1000.0 / 1000.0 / ticksPerQuarter);
            return time;
        }

        private Pitch GetPitchFromMidiNumber(int noteNumber)
        {
            int octave = (noteNumber / 12) - 1;
            int stepVal = noteNumber % 12;

            Step step = stepVal switch
            {
                0 => Step.C,
                1 => Step.C,
                2 => Step.D,
                3 => Step.D,
                4 => Step.E,
                5 => Step.F,
                6 => Step.F,
                7 => Step.G,
                8 => Step.G,
                9 => Step.A,
                10 => Step.A,
                11 => Step.B,
                _ => Step.C
            };

            int alter = 0;
            if (new[] { 1, 3, 6, 8, 10 }.Contains(stepVal)) alter = 1;

            return new Pitch(step, alter, octave);
        }

        private RhythmicDuration GetDurationFromTicks(long ticks, int ticksPerQuarter)
        {
            double quarters = (double)ticks / ticksPerQuarter;
            if (quarters >= 4.0) return RhythmicDuration.Whole;
            if (quarters >= 2.0) return RhythmicDuration.Half;
            if (quarters >= 1.0) return RhythmicDuration.Quarter;
            if (quarters >= 0.5) return RhythmicDuration.Eighth;
            if (quarters >= 0.25) return RhythmicDuration.Sixteenth;
            return RhythmicDuration.Quarter;
        }
    }
}
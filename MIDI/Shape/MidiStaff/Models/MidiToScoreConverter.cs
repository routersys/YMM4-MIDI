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
        public bool IsTriplet { get; set; }

        public MidiNote(Pitch pitch, RhythmicDuration duration) : base(pitch, duration)
        {
        }
    }

    public class MidiRest : Rest, IMidiElement
    {
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public bool IsTriplet { get; set; }

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

        private class QuantizedEvent
        {
            public long AbsoluteTick { get; set; }
            public long DurationTicks { get; set; }
            public int NoteNumber { get; set; }
            public bool IsRest { get; set; }
            public RhythmicDuration Duration { get; set; }
            public bool IsTriplet { get; set; }
        }

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
                    trebleStaff.Elements.Add(new TimeSignature(TimeSignatureType.Numbers, 4, 4));
                    score.Staves.Add(trebleStaff);

                    var bassStaff = new Staff();
                    bassStaff.Elements.Add(Clef.Bass);
                    bassStaff.Elements.Add(new Key(0));
                    bassStaff.Elements.Add(new TimeSignature(TimeSignatureType.Numbers, 4, 4));
                    score.Staves.Add(bassStaff);

                    var trebleEvents = events.Where(e => e.NoteNumber >= splitPoint).OrderBy(e => e.AbsoluteTime).ToList();
                    var bassEvents = events.Where(e => e.NoteNumber < splitPoint).OrderBy(e => e.AbsoluteTime).ToList();

                    GenerateStaffContent(trebleStaff, trebleEvents, tempoEvents, ticksPerQuarter, measuresPerLine);
                    GenerateStaffContent(bassStaff, bassEvents, tempoEvents, ticksPerQuarter, measuresPerLine);

                    trebleStaff.Elements.Add(new Barline(BarlineStyle.LightHeavy));
                    bassStaff.Elements.Add(new Barline(BarlineStyle.LightHeavy));
                }
                else
                {
                    var staff = new Staff();
                    staff.Elements.Add(Clef.Treble);
                    staff.Elements.Add(new Key(0));
                    staff.Elements.Add(new TimeSignature(TimeSignatureType.Numbers, 4, 4));
                    score.Staves.Add(staff);

                    var sortedEvents = events.OrderBy(e => e.AbsoluteTime).ToList();
                    GenerateStaffContent(staff, sortedEvents, tempoEvents, ticksPerQuarter, measuresPerLine);

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

        private void GenerateStaffContent(Staff staff, List<NoteOnEvent> rawEvents, List<TempoEvent> tempoEvents, int ticksPerQuarter, int measuresPerLine)
        {
            var symbolBuffer = new List<MusicalSymbol>();
            long currentTick = 0;

            if (rawEvents.Any())
            {
                currentTick = rawEvents.First().AbsoluteTime;
                if (currentTick > ticksPerQuarter / 4)
                {
                    var initialRestTicks = currentTick;
                    AddQuantizedRests(symbolBuffer, 0, initialRestTicks, ticksPerQuarter, tempoEvents);
                }
                else
                {
                    currentTick = 0;
                }
            }

            int i = 0;
            while (i < rawEvents.Count)
            {
                var evt = rawEvents[i];

                long gap = evt.AbsoluteTime - currentTick;
                if (gap > ticksPerQuarter / 8)
                {
                    AddQuantizedRests(symbolBuffer, currentTick, gap, ticksPerQuarter, tempoEvents);
                    currentTick = evt.AbsoluteTime;
                }

                var chordEvents = new List<NoteOnEvent>();
                long startTick = evt.AbsoluteTime;
                while (i < rawEvents.Count && Math.Abs(rawEvents[i].AbsoluteTime - startTick) < 10)
                {
                    chordEvents.Add(rawEvents[i]);
                    i++;
                }

                long maxDurationTicks = chordEvents.Max(n => n.NoteLength);
                var quantInfo = QuantizeDuration(maxDurationTicks, ticksPerQuarter);

                double startTime = CalculateTime(startTick, tempoEvents, ticksPerQuarter);
                double endTime = CalculateTime(startTick + quantInfo.DurationTicks, tempoEvents, ticksPerQuarter);

                var pitches = chordEvents.Select(n => GetPitchFromMidiNumber(n.NoteNumber))
                                         .OrderByDescending(p => p.MidiPitch)
                                         .ToList();

                var notes = new List<MidiNote>();
                foreach (var pitch in pitches)
                {
                    var note = new MidiNote(pitch, quantInfo.Duration)
                    {
                        StemDirection = VerticalDirection.Up,
                        StartTime = startTime,
                        EndTime = endTime,
                        IsTriplet = quantInfo.IsTriplet
                    };
                    notes.Add(note);
                }

                symbolBuffer.AddRange(notes);
                currentTick = startTick + quantInfo.DurationTicks;
            }

            ApplyTuplets(symbolBuffer);
            ApplyBeams(symbolBuffer, ticksPerQuarter);
            LayoutSymbolsToStaff(staff, symbolBuffer, ticksPerQuarter, measuresPerLine);
        }

        private void AddQuantizedRests(List<MusicalSymbol> buffer, long startTick, long durationTicks, int ticksPerQuarter, List<TempoEvent> tempoEvents)
        {
            long remaining = durationTicks;
            long current = startTick;

            while (remaining >= ticksPerQuarter / 4)
            {
                var q = QuantizeDuration(remaining, ticksPerQuarter);

                if (q.DurationTicks > remaining)
                {
                    q = QuantizeDuration(remaining * 2 / 3, ticksPerQuarter);
                    if (q.DurationTicks == 0) break;
                }

                double sTime = CalculateTime(current, tempoEvents, ticksPerQuarter);
                double eTime = CalculateTime(current + q.DurationTicks, tempoEvents, ticksPerQuarter);

                var rest = new MidiRest(q.Duration) { StartTime = sTime, EndTime = eTime, IsTriplet = q.IsTriplet };
                buffer.Add(rest);

                current += q.DurationTicks;
                remaining -= q.DurationTicks;
            }
        }

        private QuantizedEvent QuantizeDuration(long ticks, int tpq)
        {
            double quarters = (double)ticks / tpq;

            var candidates = new[]
            {
                new { Q = 4.0, D = RhythmicDuration.Whole, T = false },
                new { Q = 3.0, D = RhythmicDuration.Half, T = false },
                new { Q = 2.0, D = RhythmicDuration.Half, T = false },
                new { Q = 1.5, D = RhythmicDuration.Quarter, T = false },
                new { Q = 1.0, D = RhythmicDuration.Quarter, T = false },
                new { Q = 0.75, D = RhythmicDuration.Eighth, T = false },
                new { Q = 0.5, D = RhythmicDuration.Eighth, T = false },
                new { Q = 1.0/3.0, D = RhythmicDuration.Eighth, T = true },
                new { Q = 0.25, D = RhythmicDuration.Sixteenth, T = false },
                new { Q = 0.1666, D = RhythmicDuration.Sixteenth, T = true }
            };

            var best = candidates.OrderBy(c => Math.Abs(c.Q - quarters)).First();

            long qTicks;
            if (best.T)
            {
                if (best.D == RhythmicDuration.Eighth) qTicks = tpq / 3;
                else if (best.D == RhythmicDuration.Sixteenth) qTicks = tpq / 6;
                else qTicks = (long)(best.Q * tpq);
            }
            else
            {
                qTicks = (long)(best.Q * tpq);
            }

            if (qTicks == 0) qTicks = tpq / 4;

            return new QuantizedEvent
            {
                Duration = best.D,
                IsTriplet = best.T,
                DurationTicks = qTicks
            };
        }

        private void ApplyTuplets(List<MusicalSymbol> symbols)
        {
            for (int i = 0; i < symbols.Count; i++)
            {
                if (IsTripletSymbol(symbols[i]))
                {
                    var group = new List<NoteOrRest> { (NoteOrRest)symbols[i] };
                    int j = i + 1;
                    while (j < symbols.Count && IsTripletSymbol(symbols[j]) && ((NoteOrRest)symbols[j]).Duration == group[0].Duration)
                    {
                        group.Add((NoteOrRest)symbols[j]);
                        j++;
                        if (group.Count == 3) break;
                    }

                    if (group.Count == 3)
                    {
                        group[0].Tuplet = TupletType.Start;
                        group[2].Tuplet = TupletType.Stop;

                        i = j - 1;
                    }
                }
            }
        }

        private bool IsTripletSymbol(MusicalSymbol sym)
        {
            if (sym is MidiNote mn) return mn.IsTriplet;
            if (sym is MidiRest mr) return mr.IsTriplet;
            return false;
        }

        private void ApplyBeams(List<MusicalSymbol> symbols, int tpq)
        {
            var group = new List<MidiNote>();
            double currentPos = 0;

            for (int i = 0; i < symbols.Count; i++)
            {
                var sym = symbols[i];
                double duration = 0;

                if (sym is NoteOrRest nr)
                {
                    double val = nr.Duration.ToDouble();
                    if (IsTripletSymbol(sym)) val = val * 2.0 / 3.0;
                    duration = val;
                }

                if (sym is MidiNote note && note.Duration.ToDouble() <= 0.5)
                {
                    bool atBeatBoundary = Math.Abs(currentPos % 1.0) < 0.001;

                    if (group.Count > 0)
                    {
                        if (atBeatBoundary)
                        {
                            FinalizeBeam(group);
                        }
                        else
                        {
                            var last = group.Last();
                            if (last.IsTriplet != note.IsTriplet)
                            {
                                FinalizeBeam(group);
                            }
                        }
                    }
                    group.Add(note);
                }
                else
                {
                    FinalizeBeam(group);
                }

                currentPos += duration;
            }
            FinalizeBeam(group);
        }

        private void FinalizeBeam(List<MidiNote> group)
        {
            if (group.Count > 1)
            {
                foreach (var n in group) n.BeamList.Clear();

                group[0].BeamList.Add(NoteBeamType.Start);
                for (int k = 1; k < group.Count - 1; k++)
                {
                    group[k].BeamList.Add(NoteBeamType.Continue);
                }
                group[group.Count - 1].BeamList.Add(NoteBeamType.End);
            }
            group.Clear();
        }

        private void LayoutSymbolsToStaff(Staff staff, List<MusicalSymbol> symbols, int tpq, int measuresPerLine)
        {
            long ticksPerMeasure = tpq * 4;
            int measureCount = 0;

            long accumulatedTicksInMeasure = 0;

            foreach (var sym in symbols)
            {
                long durTicks = 0;
                if (sym is NoteOrRest nr)
                {
                    double quarters = nr.Duration.ToDouble();
                    if (IsTripletSymbol(sym)) quarters = quarters * 2.0 / 3.0;
                    durTicks = (long)(quarters * tpq);
                }

                if (accumulatedTicksInMeasure + durTicks > ticksPerMeasure)
                {
                    long fitTicks = ticksPerMeasure - accumulatedTicksInMeasure;
                    if (fitTicks > 0 && sym is NoteOrRest splitNr)
                    {
                        var splitDur = QuantizeDuration(fitTicks, tpq);
                        var remainingDur = QuantizeDuration(durTicks - fitTicks, tpq);

                        if (splitNr is MidiNote mn)
                        {
                            var n1 = new MidiNote(mn.Pitch, splitDur.Duration) { StartTime = mn.StartTime, EndTime = mn.StartTime + CalculateTimeDelta(fitTicks, tpq), TieType = NoteTieType.Start, IsTriplet = mn.IsTriplet };
                            var n2 = new MidiNote(mn.Pitch, remainingDur.Duration) { StartTime = n1.EndTime, EndTime = mn.EndTime, TieType = NoteTieType.Stop, IsTriplet = mn.IsTriplet };

                            n1.Tuplet = mn.Tuplet;
                            n1.BeamList.AddRange(mn.BeamList);

                            staff.Elements.Add(n1);

                            AddBarline(staff, ref measureCount, measuresPerLine);
                            accumulatedTicksInMeasure = 0;

                            staff.Elements.Add(n2);
                            accumulatedTicksInMeasure = remainingDur.DurationTicks;
                        }
                        else if (splitNr is MidiRest mr)
                        {
                            staff.Elements.Add(new MidiRest(splitDur.Duration) { StartTime = mr.StartTime, EndTime = mr.StartTime + CalculateTimeDelta(fitTicks, tpq), IsTriplet = mr.IsTriplet, Tuplet = mr.Tuplet });
                            AddBarline(staff, ref measureCount, measuresPerLine);
                            accumulatedTicksInMeasure = 0;

                            staff.Elements.Add(new MidiRest(remainingDur.Duration) { StartTime = mr.StartTime + CalculateTimeDelta(fitTicks, tpq), EndTime = mr.EndTime, IsTriplet = mr.IsTriplet });
                            accumulatedTicksInMeasure = remainingDur.DurationTicks;
                        }
                    }
                    else
                    {
                        AddBarline(staff, ref measureCount, measuresPerLine);
                        accumulatedTicksInMeasure = 0;
                        staff.Elements.Add(sym);
                        accumulatedTicksInMeasure += durTicks;
                    }
                }
                else
                {
                    staff.Elements.Add(sym);
                    accumulatedTicksInMeasure += durTicks;

                    if (accumulatedTicksInMeasure >= ticksPerMeasure)
                    {
                        AddBarline(staff, ref measureCount, measuresPerLine);
                        accumulatedTicksInMeasure = 0;
                    }
                }
            }
        }

        private double CalculateTimeDelta(long ticks, int tpq)
        {
            return (double)ticks / tpq * 0.5;
        }

        private void AddBarline(Staff staff, ref int measureCount, int measuresPerLine)
        {
            staff.Elements.Add(new Barline());
            measureCount++;
            if (measureCount % measuresPerLine == 0)
            {
                staff.Elements.Add(new PrintSuggestion { IsSystemBreak = true });
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
    }
}
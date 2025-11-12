using MIDI.Utils;
using MIDI.Voice.Engine.Worldline;
using MIDI.Voice.SUSL.Core;
using MIDI.Voice.SUSL.Parsing.AST;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MIDI.Voice.Engine.Worldline
{
    internal static class SuslEventConverter
    {
        public static (List<WorldlineResamplerItem>, TimingContext) Convert(SuslProgram program, UtauVoicebank voicebank)
        {
            var items = new List<WorldlineResamplerItem>();
            var context = new TimingContext(program);

            if (program.SequenceSection == null)
            {
                return (items, context);
            }

            foreach (var command in program.SequenceSection.Commands)
            {
                if (command is NoteCommandNode note)
                {
                    try
                    {
                        var resamplerItem = new WorldlineResamplerItem(note, voicebank, context);
                        items.Add(resamplerItem);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"Failed to create ResamplerItem for note: {note.LyricPhoneme.Lyric ?? note.LyricPhoneme.Phoneme}", ex);
                    }
                }
            }

            return (items, context);
        }

        internal class TimingContext
        {
            public int Timebase { get; }
            private readonly SuslProgram _program;
            private readonly double _initialTempo;
            private readonly TimeSignature _initialTimeSignature;

            private List<TempoChangeCommandNode> _tempoEvents = new List<TempoChangeCommandNode>();
            private List<TimeSignatureChangeCommandNode> _tsEvents = new List<TimeSignatureChangeCommandNode>();
            private readonly SuslContext _tempContext;

            public TimingContext(SuslProgram program)
            {
                _program = program;
                _tempContext = new SuslContext();

                if (program.ConfigurationSection != null)
                {
                    program.ConfigurationSection.Evaluate(_tempContext);
                }
                Timebase = _tempContext.Timebase;
                _initialTempo = _tempContext.InitialTempo;
                _initialTimeSignature = _tempContext.InitialTimeSignature;

                _tsEvents.Insert(0, new TimeSignatureChangeCommandNode(_initialTimeSignature.Numerator, _initialTimeSignature.Denominator) { AbsoluteTick = 0 });
                _tempoEvents.Insert(0, new TempoChangeCommandNode(_initialTempo) { AbsoluteTick = 0 });

                var allCommands = program.SequenceSection?.Commands ?? new List<ISequenceCommand>();

                foreach (var cmd in allCommands)
                {
                    cmd.AbsoluteTick = PositionToTick(cmd.AtPosition);
                }

                _tsEvents.AddRange(allCommands.OfType<TimeSignatureChangeCommandNode>());
                _tempoEvents.AddRange(allCommands.OfType<TempoChangeCommandNode>());

                _tsEvents = _tsEvents
                    .GroupBy(e => e.AbsoluteTick)
                    .Select(g => g.Last())
                    .OrderBy(e => e.AbsoluteTick)
                    .ToList();

                _tempoEvents = _tempoEvents
                    .GroupBy(e => e.AbsoluteTick)
                    .Select(g => g.Last())
                    .OrderBy(e => e.AbsoluteTick)
                    .ToList();
            }

            public int PositionToTick(AtDefinitionNode? at)
            {
                if (at == null) return 0;

                int measure = at.Measure - 1;
                int beat = at.Beat - 1;
                if (measure < 0) measure = 0;
                if (beat < 0) beat = 0;

                int currentTick = 0;
                int currentMeasure = 0;
                var currentTimeSignature = _initialTimeSignature;

                var tsEvents = _tsEvents.OrderBy(e => e.AbsoluteTick).ToList();
                int eventIndex = 0;

                while (currentMeasure < measure)
                {
                    while (eventIndex < tsEvents.Count && tsEvents[eventIndex].AbsoluteTick <= currentTick)
                    {
                        currentTimeSignature = new TimeSignature(tsEvents[eventIndex].Numerator, tsEvents[eventIndex].Denominator);
                        eventIndex++;
                    }

                    int ticksPerBeat = Timebase;
                    double beatUnit = 4.0 / currentTimeSignature.Denominator;
                    int ticksPerMeasure = (int)(ticksPerBeat * currentTimeSignature.Numerator * beatUnit);

                    currentTick += ticksPerMeasure;
                    currentMeasure++;
                }

                while (eventIndex < tsEvents.Count && tsEvents[eventIndex].AbsoluteTick <= currentTick)
                {
                    currentTimeSignature = new TimeSignature(tsEvents[eventIndex].Numerator, tsEvents[eventIndex].Denominator);
                    eventIndex++;
                }

                int finalTicksPerBeat = (int)(Timebase * (4.0 / currentTimeSignature.Denominator));
                currentTick += (beat * finalTicksPerBeat) + at.Tick;

                return currentTick;
            }


            public double GetMsForTick(int tick)
            {
                double ms = 0;
                int lastTick = 0;
                double currentTempo = _initialTempo;

                var events = _tempoEvents.Where(e => e.AbsoluteTick < tick).OrderBy(e => e.AbsoluteTick).ToList();

                foreach (var tempoEvent in events)
                {
                    int segmentTicks = tempoEvent.AbsoluteTick - lastTick;
                    if (segmentTicks > 0)
                    {
                        ms += WorldlineUtils.TempoTickToMs(segmentTicks, currentTempo, Timebase);
                    }
                    currentTempo = tempoEvent.Tempo;
                    lastTick = tempoEvent.AbsoluteTick;
                }

                int remainingTicks = tick - lastTick;
                if (remainingTicks > 0)
                {
                    ms += WorldlineUtils.TempoTickToMs(remainingTicks, currentTempo, Timebase);
                }

                return ms;
            }

            public double GetMsDuration(int startTick, int durationTicks)
            {
                if (durationTicks == 0) return 0;

                double startMs = GetMsForTick(startTick);
                double endMs = GetMsForTick(startTick + durationTicks);
                return endMs - startMs;
            }

            public int GetTickDuration(LengthDefinitionNode length)
            {
                if (length.IsTicks)
                {
                    return length.TickValue;
                }

                int baseLength = _tempContext.GetNoteLength(length.Constant ?? "QUARTER_NOTE");

                if (length.IsDotted)
                {
                    baseLength = (int)(baseLength * 1.5);
                }
                return baseLength;
            }

            public double GetCurrentTempo(int tick)
            {
                var tempoEvent = _tempoEvents.LastOrDefault(e => e.AbsoluteTick <= tick);
                return tempoEvent?.Tempo ?? _initialTempo;
            }

            public TimeSignature GetCurrentTimeSignature(int tick)
            {
                var tsEvent = _tsEvents.LastOrDefault(e => e.AbsoluteTick <= tick);
                return tsEvent != null ? new TimeSignature(tsEvent.Numerator, tsEvent.Denominator) : _initialTimeSignature;
            }

            public double GetFrequency(PitchDefinitionNode pitch)
            {
                int noteNum = 60;
                switch (pitch.Type)
                {
                    case PitchType.MidiNote:
                        noteNum = pitch.IntValue;
                        break;
                    case PitchType.Scientific:
                        noteNum = NoteNameToMidi(pitch.StringValue ?? "C4");
                        break;
                    case PitchType.Relative:
                        noteNum += pitch.IntValue;
                        break;
                }
                return WorldlineUtils.ToneToFreq(noteNum);
            }

            public (double[] f0, double[] gender, double[] tension, double[] breathiness, double[] voicing) GetCurves(int startTick, int durationTicks, int baseNoteNum)
            {
                const double frameMs = 10.0;

                if (startTick < 0) startTick = 0;
                if (durationTicks < 0) durationTicks = 0;

                double durationMs = GetMsDuration(startTick, durationTicks);
                if (double.IsNaN(durationMs) || double.IsInfinity(durationMs) || durationMs < 0)
                {
                    durationMs = 1.0;
                }

                int frames = (int)Math.Ceiling(durationMs / frameMs);
                if (frames <= 0) frames = 1;

                const int MAX_SAFE_FRAMES = 500000;
                if (frames > MAX_SAFE_FRAMES)
                {
                    Logger.Warn($"Frame count ({frames}) for note at tick {startTick} is excessively large (DurationMs: {durationMs}). Clamping to {MAX_SAFE_FRAMES}.");
                    frames = MAX_SAFE_FRAMES;
                }

                double[] f0 = new double[frames];
                double[] gender = new double[frames];
                double[] tension = new double[frames];
                double[] breathiness = new double[frames];
                double[] voicing = new double[frames];

                Array.Fill(f0, baseNoteNum);
                Array.Fill(gender, 0.5);
                Array.Fill(tension, 0.5);
                Array.Fill(breathiness, 0.5);
                Array.Fill(voicing, 1.0);

                return (f0, gender, tension, breathiness, voicing);
            }

            private static readonly Dictionary<string, int> NoteBaseValues = new Dictionary<string, int>
            {
                {"C", 0}, {"C#", 1}, {"D", 2}, {"D#", 3}, {"E", 4}, {"F", 5},
                {"F#", 6}, {"G", 7}, {"G#", 8}, {"A", 9}, {"A#", 10}, {"B", 11}
            };

            private int NoteNameToMidi(string noteName)
            {
                var match = System.Text.RegularExpressions.Regex.Match(noteName.ToUpper(), @"([A-G]#?)([0-9])");
                if (!match.Success) throw new ArgumentException("Invalid note name format: " + noteName);

                string key = match.Groups[1].Value;
                int octave = int.Parse(match.Groups[2].Value);

                return NoteBaseValues[key] + (octave + 1) * 12;
            }
        }
    }
}
using MIDI.Voice.SUSL.Parsing.AST;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MIDI.Voice.SUSL.Core
{
    public class TimeSignature
    {
        public int Numerator { get; }
        public int Denominator { get; }

        public TimeSignature(int numerator, int denominator)
        {
            Numerator = numerator;
            Denominator = denominator;
        }

        public override string ToString()
        {
            return $"{Numerator}/{Denominator}";
        }
    }

    public class SuslContext
    {
        public int Timebase { get; set; } = 480;
        public double InitialTempo { get; set; } = 120.0;
        public TimeSignature InitialTimeSignature { get; set; } = new TimeSignature(4, 4);

        private readonly List<TempoChangeCommandNode> _tempoEvents = new List<TempoChangeCommandNode>();
        private readonly List<TimeSignatureChangeCommandNode> _tsEvents = new List<TimeSignatureChangeCommandNode>();

        public void AddTempoChange(int absoluteTick, double tempo)
        {
            _tempoEvents.Add(new TempoChangeCommandNode(tempo) { AbsoluteTick = absoluteTick });
            _tempoEvents.Sort((a, b) => a.AbsoluteTick.CompareTo(b.AbsoluteTick));
        }

        public void AddTimeSignatureChange(int absoluteTick, int numerator, int denominator)
        {
            _tsEvents.Add(new TimeSignatureChangeCommandNode(numerator, denominator) { AbsoluteTick = absoluteTick });
            _tsEvents.Sort((a, b) => a.AbsoluteTick.CompareTo(b.AbsoluteTick));
        }

        public int PositionToTick(AtDefinitionNode? at)
        {
            if (at == null) return 0;

            int measure = at.Measure - 1;
            int beat = at.Beat - 1;

            if (measure < 0) measure = 0;
            if (beat < 0) beat = 0;

            int ticksPerBeat = Timebase;
            int beatsPerMeasure = InitialTimeSignature.Numerator;
            double beatUnit = 4.0 / InitialTimeSignature.Denominator;

            int ticksPerMeasure = (int)(ticksPerBeat * beatsPerMeasure * beatUnit);

            return (measure * ticksPerMeasure) + (beat * ticksPerBeat) + at.Tick;
        }

        public int GetNoteLength(string constant)
        {
            switch (constant.ToUpperInvariant())
            {
                case "WHOLE_NOTE":
                    return Timebase * 4;
                case "HALF_NOTE":
                    return Timebase * 2;
                case "QUARTER_NOTE":
                    return Timebase;
                case "EIGHTH_NOTE":
                    return Timebase / 2;
                case "SIXTEENTH_NOTE":
                    return Timebase / 4;
                case "THIRTY_SECOND_NOTE":
                    return Timebase / 8;
                case "SIXTY_FOURTH_NOTE":
                    return Timebase / 16;
                default:
                    return Timebase;
            }
        }
    }
}
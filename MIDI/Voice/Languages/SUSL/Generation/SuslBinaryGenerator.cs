using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MIDI.Voice.SUSL.Core;
using MIDI.Voice.SUSL.Parsing.AST;

namespace MIDI.Voice.SUSL.Generation
{
    public class SuslBinaryGenerator
    {
        private readonly SuslContext _context;

        public SuslBinaryGenerator(SuslContext context)
        {
            _context = context;
        }

        public void Generate(SuslProgram program, BinaryWriter writer)
        {
            WriteHeader(program, writer);
            WriteConfig(program.ConfigurationSection, writer);
            WriteDefaults(program.DefaultSection, writer);
            WriteSequence(program.SequenceSection, writer);
        }

        private void WriteHeader(SuslProgram program, BinaryWriter writer)
        {
            writer.Write(Encoding.UTF8.GetBytes("SUSL"));
            writer.Write(1);
        }

        private void WriteConfig(ConfigurationSectionNode? config, BinaryWriter writer)
        {
            writer.Write((byte)0x01);
            writer.Write(_context.Timebase);
            writer.Write(_context.InitialTempo);
            writer.Write(_context.InitialTimeSignature.Numerator);
            writer.Write(_context.InitialTimeSignature.Denominator);
        }

        private void WriteDefaults(DefaultSectionNode? defaults, BinaryWriter writer)
        {
            if (defaults == null)
            {
                writer.Write((byte)0x02);
                writer.Write(0);
                writer.Write(false);
                return;
            }

            writer.Write((byte)0x02);
            writer.Write(defaults.Expressions.Count);
            foreach (var expr in defaults.Expressions)
            {
                writer.Write(expr.Name);
                writer.Write(expr.Value);
            }

            bool hasDefaultVibrato = defaults.DefaultVibrato != null;
            writer.Write(hasDefaultVibrato);
            if (hasDefaultVibrato && defaults.DefaultVibrato != null)
            {
                WriteVibratoBlock(defaults.DefaultVibrato, writer);
            }
        }

        private void WriteSequence(SequenceSectionNode? sequence, BinaryWriter writer)
        {
            if (sequence == null)
            {
                writer.Write((byte)0x03);
                writer.Write(0);
                return;
            }

            writer.Write((byte)0x03);
            writer.Write(sequence.Commands.Count);

            foreach (var command in sequence.Commands)
            {
                switch (command)
                {
                    case NoteCommandNode note:
                        WriteNoteCommand(note, writer);
                        break;
                    case RestCommandNode rest:
                        WriteRestCommand(rest, writer);
                        break;
                    case TempoChangeCommandNode tempo:
                        WriteTempoChangeCommand(tempo, writer);
                        break;
                    case TimeSignatureChangeCommandNode tsig:
                        WriteTimeSigChangeCommand(tsig, writer);
                        break;
                }
            }
        }

        private void WriteNoteCommand(NoteCommandNode note, BinaryWriter writer)
        {
            writer.Write((byte)0x10);

            WriteAtPosition(note.AtPosition, writer);
            WriteLength(note.Length, writer);
            WritePitch(note.Pitch, writer);
            WriteLyricPhoneme(note.LyricPhoneme, writer);

            bool hasParams = note.Parameters != null;
            writer.Write(hasParams);
            if (hasParams && note.Parameters != null)
            {
                WriteParametersBlock(note.Parameters, writer);
            }
        }

        private void WriteRestCommand(RestCommandNode rest, BinaryWriter writer)
        {
            writer.Write((byte)0x11);
            WriteAtPosition(rest.AtPosition, writer);
            WriteLength(rest.Length, writer);
        }

        private void WriteTempoChangeCommand(TempoChangeCommandNode tempo, BinaryWriter writer)
        {
            writer.Write((byte)0x20);
            WriteAtPosition(tempo.AtPosition, writer);
            writer.Write(tempo.Tempo);
        }

        private void WriteTimeSigChangeCommand(TimeSignatureChangeCommandNode tsig, BinaryWriter writer)
        {
            writer.Write((byte)0x21);
            WriteAtPosition(tsig.AtPosition, writer);
            writer.Write(tsig.Numerator);
            writer.Write(tsig.Denominator);
        }


        private void WriteAtPosition(AtDefinitionNode? at, BinaryWriter writer)
        {
            bool hasAt = at != null;
            writer.Write(hasAt);
            if (hasAt && at != null)
            {
                writer.Write(at.Measure);
                writer.Write(at.Beat);
                writer.Write(at.Tick);
            }
        }

        private void WriteLength(LengthDefinitionNode length, BinaryWriter writer)
        {
            writer.Write(length.IsTicks);
            if (length.IsTicks)
            {
                writer.Write(length.TickValue);
            }
            else
            {
                writer.Write(length.Constant ?? string.Empty);
                writer.Write(length.IsDotted);
            }
        }

        private void WritePitch(PitchDefinitionNode pitch, BinaryWriter writer)
        {
            writer.Write((byte)pitch.Type);
            switch (pitch.Type)
            {
                case PitchType.MidiNote:
                    writer.Write(pitch.IntValue);
                    break;
                case PitchType.Scientific:
                    writer.Write(pitch.StringValue ?? string.Empty);
                    break;
                case PitchType.Relative:
                    writer.Write(pitch.IntValue);
                    break;
            }
        }

        private void WriteLyricPhoneme(LyricPhonemeNode lp, BinaryWriter writer)
        {
            writer.Write(lp.IsPhonemeOnly);
            writer.Write(lp.IsLyricOnly);
            writer.Write(lp.HasBoth);

            if (lp.IsPhonemeOnly || lp.HasBoth)
                writer.Write(lp.Phoneme ?? string.Empty);

            if (lp.IsLyricOnly || lp.HasBoth)
                writer.Write(lp.Lyric ?? string.Empty);
        }

        private void WriteParametersBlock(ParametersBlockNode block, BinaryWriter writer)
        {
            writer.Write(block.Expressions.Count);
            foreach (var expr in block.Expressions)
            {
                writer.Write(expr.Name);
                writer.Write(expr.Value);
            }

            writer.Write((byte)block.VibratoHandling);
            if (block.VibratoHandling == VibratoHandling.Override && block.VibratoOverride != null)
            {
                WriteVibratoBlock(block.VibratoOverride, writer);
            }

            bool hasPitchCurve = block.PitchCurve != null;
            writer.Write(hasPitchCurve);
            if (hasPitchCurve && block.PitchCurve != null)
            {
                WritePitchCurve(block.PitchCurve, writer);
            }
        }

        private void WriteVibratoBlock(VibratoBlockNode block, BinaryWriter writer)
        {
            writer.Write(block.Period.HasValue);
            if (block.Period.HasValue) writer.Write(block.Period.Value);

            writer.Write(block.Depth.HasValue);
            if (block.Depth.HasValue) writer.Write(block.Depth.Value);

            writer.Write(block.FadeIn.HasValue);
            if (block.FadeIn.HasValue) writer.Write(block.FadeIn.Value);

            writer.Write(block.FadeOut.HasValue);
            if (block.FadeOut.HasValue) writer.Write(block.FadeOut.Value);
        }

        private void WritePitchCurve(PitchCurveBlockNode curve, BinaryWriter writer)
        {
            writer.Write(curve.IsBezier);

            writer.Write(curve.Points.Count);
            foreach (var point in curve.Points)
            {
                writer.Write(point.Tick);
                writer.Write(point.Cent);
                writer.Write(point.Shape ?? string.Empty);
            }
        }
    }
}
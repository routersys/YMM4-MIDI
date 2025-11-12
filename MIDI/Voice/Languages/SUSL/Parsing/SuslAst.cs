using MIDI.Voice.SUSL.Core;
using MIDI.Voice.SUSL.Errors;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MIDI.Voice.SUSL.Parsing.AST
{
    public abstract class AstNode
    {
        public int Line { get; set; }
        public int Column { get; set; }
    }

    public class SuslProgram : AstNode
    {
        public ConfigurationSectionNode? ConfigurationSection { get; set; }
        public DefaultSectionNode? DefaultSection { get; set; }
        public SequenceSectionNode? SequenceSection { get; set; }
    }

    public class ConfigurationSectionNode : AstNode
    {
        public List<AssignmentNode> Assignments { get; } = new List<AssignmentNode>();

        public void Evaluate(SuslContext context)
        {
            foreach (var assignment in Assignments)
            {
                assignment.Evaluate(context);
            }
        }
    }

    public class AssignmentNode : AstNode
    {
        public string Variable { get; set; }
        public object Value { get; set; }

        public AssignmentNode(string variable, object value)
        {
            Variable = variable;
            Value = value;
        }

        public void Evaluate(SuslContext context)
        {
            try
            {
                switch (Variable.ToLower())
                {
                    case "timebase":
                        context.Timebase = Convert.ToInt32(Value);
                        break;
                    case "tempo":
                        context.InitialTempo = Convert.ToDouble(Value);
                        break;
                    case "timesignature":
                        if (Value is TimeSignature ts)
                            context.InitialTimeSignature = ts;
                        break;
                }
            }
            catch (Exception ex)
            {
                throw new SuslException(SuslErrorDefinitions.E0011_InvalidAssignmentValue, Line, Column, Value.ToString() ?? "", Variable, ex.Message);
            }
        }
    }

    public class DefaultSectionNode : AstNode
    {
        public List<ParameterExpressionNode> Expressions { get; } = new List<ParameterExpressionNode>();
        public VibratoBlockNode? DefaultVibrato { get; set; }

        public void Evaluate(SuslContext context)
        {
        }
    }

    public class SequenceSectionNode : AstNode
    {
        public List<ISequenceCommand> Commands { get; } = new List<ISequenceCommand>();
    }

    public interface ISequenceCommand
    {
        AtDefinitionNode? AtPosition { get; set; }
        int AbsoluteTick { get; set; }
    }

    public abstract class BaseCommandNode : AstNode, ISequenceCommand
    {
        public AtDefinitionNode? AtPosition { get; set; }
        public int AbsoluteTick { get; set; }
    }

    public class NoteCommandNode : BaseCommandNode
    {
        public LengthDefinitionNode Length { get; set; }
        public PitchDefinitionNode Pitch { get; set; }
        public LyricPhonemeNode LyricPhoneme { get; set; }
        public ParametersBlockNode? Parameters { get; set; }

        public NoteCommandNode(LengthDefinitionNode length, PitchDefinitionNode pitch, LyricPhonemeNode lyricPhoneme)
        {
            Length = length;
            Pitch = pitch;
            LyricPhoneme = lyricPhoneme;
        }
    }

    public class RestCommandNode : BaseCommandNode
    {
        public LengthDefinitionNode Length { get; set; }

        public RestCommandNode(LengthDefinitionNode length)
        {
            Length = length;
        }
    }

    public class TempoChangeCommandNode : BaseCommandNode
    {
        public double Tempo { get; set; }
        public TempoChangeCommandNode(double tempo)
        {
            Tempo = tempo;
        }
    }

    public class TimeSignatureChangeCommandNode : BaseCommandNode
    {
        public int Numerator { get; set; }
        public int Denominator { get; set; }
        public TimeSignatureChangeCommandNode(int num, int den)
        {
            Numerator = num;
            Denominator = den;
        }
    }


    public class AtDefinitionNode : AstNode
    {
        public int Measure { get; set; }
        public int Beat { get; set; }
        public int Tick { get; set; }

        public AtDefinitionNode(int measure, int beat, int tick)
        {
            Measure = measure;
            Beat = beat;
            Tick = tick;
        }
    }

    public class LengthDefinitionNode : AstNode
    {
        public bool IsTicks { get; private set; }
        public int TickValue { get; private set; }
        public string? Constant { get; private set; }
        public bool IsDotted { get; private set; }

        public LengthDefinitionNode(int ticks)
        {
            IsTicks = true;
            TickValue = ticks;
        }

        public LengthDefinitionNode(string constant, bool dotted)
        {
            IsTicks = false;
            Constant = constant;
            IsDotted = dotted;
        }
    }

    public enum PitchType
    {
        MidiNote,
        Scientific,
        Relative
    }

    public class PitchDefinitionNode : AstNode
    {
        public PitchType Type { get; private set; }
        public int IntValue { get; private set; }
        public string? StringValue { get; private set; }

        public PitchDefinitionNode(PitchType type, int value)
        {
            if (type != PitchType.MidiNote && type != PitchType.Relative)
                throw new ArgumentException("Invalid PitchType for integer value.");
            Type = type;
            IntValue = value;
        }

        public PitchDefinitionNode(PitchType type, string value)
        {
            if (type != PitchType.Scientific)
                throw new ArgumentException("Invalid PitchType for string value.");
            Type = type;
            StringValue = value;
        }
    }

    public class LyricPhonemeNode : AstNode
    {
        public string? Lyric { get; private set; }
        public string? Phoneme { get; private set; }

        public bool IsPhonemeOnly => Phoneme != null && Lyric == null;
        public bool IsLyricOnly => Lyric != null && Phoneme == null;
        public bool HasBoth => Lyric != null && Phoneme != null;

        public LyricPhonemeNode(string? lyric, string? phoneme)
        {
            if (lyric == null && phoneme == null)
                throw new ArgumentNullException("Both lyric and phoneme cannot be null.");
            Lyric = lyric;
            Phoneme = phoneme;
        }
    }

    public class ParametersBlockNode : AstNode
    {
        public List<ParameterExpressionNode> Expressions { get; } = new List<ParameterExpressionNode>();
        public VibratoHandling VibratoHandling { get; set; } = VibratoHandling.None;
        public VibratoBlockNode? VibratoOverride { get; set; }
        public PitchCurveBlockNode? PitchCurve { get; set; }
    }

    public class ParameterExpressionNode : AstNode
    {
        public string Name { get; set; }
        public double Value { get; set; }

        public ParameterExpressionNode(string name, double value)
        {
            Name = name;
            Value = value;
        }
    }

    public enum VibratoHandling
    {
        None,
        Default,
        Off,
        Override
    }

    public class VibratoBlockNode : AstNode
    {
        public double? Period { get; set; }
        public double? Depth { get; set; }
        public double? FadeIn { get; set; }
        public double? FadeOut { get; set; }
    }

    public class PitchCurveBlockNode : AstNode
    {
        public bool IsBezier { get; set; } = false;
        public List<PitchPointNode> Points { get; } = new List<PitchPointNode>();
    }

    public class PitchPointNode : AstNode
    {
        public int Tick { get; set; }
        public double Cent { get; set; }
        public string? Shape { get; set; }

        public PitchPointNode(int tick, double cent, string? shape = null)
        {
            Tick = tick;
            Cent = cent;
            Shape = shape;
        }
    }
}
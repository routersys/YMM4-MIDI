using MIDI.Voice.SUSL.Core;
using MIDI.Voice.SUSL.Errors;
using MIDI.Voice.SUSL.Parsing.AST;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MIDI.Voice.SUSL.Parsing
{
    public enum SuslTokenType
    {
        Header,
        SectionConfig,
        SectionDefault,
        SectionSequence,
        LBrace,
        RBrace,
        LParen,
        RParen,
        Identifier,
        Number,
        String,
        Equal,
        Semicolon,
        Comma,
        Colon,
        Dot,
        At,
        Plus,
        Minus,
        Note,
        Rest,
        Tempo,
        TimeSignature,
        Vibrato,
        PitchCurve,
        Bezier,
        Off,
        EOF,
        Unknown,
        Slash
    }

    public class SuslToken
    {
        public SuslTokenType Type { get; }
        public string Lexeme { get; }
        public string Literal { get; }
        public int Line { get; }
        public int Column { get; }

        public SuslToken(SuslTokenType type, string lexeme, string? literal, int line, int column)
        {
            Type = type;
            Lexeme = lexeme;
            Literal = literal ?? string.Empty;
            Line = line;
            Column = column;
        }

        public override string ToString()
        {
            return $"{Type}: {Lexeme} (Lit: {Literal}) @ {Line}:{Column}";
        }
    }


    public class SuslParser
    {
        private readonly List<SuslToken> _tokens;
        private int _position;

        public SuslParser(List<SuslToken> tokens)
        {
            _tokens = tokens;
            _position = 0;
        }

        private SuslToken Current => _position < _tokens.Count ? _tokens[_position] : _tokens.Last();
        private SuslToken Peek(int offset = 1) => _position + offset < _tokens.Count ? _tokens[_position + offset] : _tokens.Last();
        private bool IsAtEnd => _position >= _tokens.Count || Current.Type == SuslTokenType.EOF;
        private SuslToken Advance()
        {
            if (!IsAtEnd) _position++;
            return _tokens[_position - 1];
        }

        private SuslToken Consume(SuslTokenType type, (string Code, string Message) error)
        {
            if (Current.Type == type)
            {
                return Advance();
            }
            throw new SuslException(error, Current.Line, Current.Column, Current.Lexeme, type.ToString());
        }

        private bool Check(SuslTokenType type)
        {
            return Current.Type == type;
        }

        private bool Match(params SuslTokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }
            return false;
        }

        private SuslException Error((string Code, string Message) error, params object[] args)
        {
            return new SuslException(error, Current.Line, Current.Column, args);
        }

        public SuslProgram ParseProgram()
        {
            var program = new SuslProgram
            {
                Line = Current.Line,
                Column = Current.Column
            };

            if (Match(SuslTokenType.Header))
            {
            }
            else
            {
                throw Error(SuslErrorDefinitions.E0001_InvalidHeader);
            }

            while (!IsAtEnd)
            {
                if (Check(SuslTokenType.SectionConfig))
                {
                    if (program.ConfigurationSection != null)
                        throw Error(SuslErrorDefinitions.E0002_InvalidSectionName, "config");
                    program.ConfigurationSection = ParseConfigurationSection();
                }
                else if (Check(SuslTokenType.SectionDefault))
                {
                    if (program.DefaultSection != null)
                        throw Error(SuslErrorDefinitions.E0002_InvalidSectionName, "default");
                    program.DefaultSection = ParseDefaultSection();
                }
                else if (Check(SuslTokenType.SectionSequence))
                {
                    if (program.SequenceSection != null)
                        throw Error(SuslErrorDefinitions.E0002_InvalidSectionName, "sequence");
                    program.SequenceSection = ParseSequenceSection();
                }
                else
                {
                    throw Error(SuslErrorDefinitions.E0003_UnexpectedToken, Current.Lexeme, "Section");
                }
            }

            return program;
        }

        private ConfigurationSectionNode ParseConfigurationSection()
        {
            var section = new ConfigurationSectionNode
            {
                Line = Current.Line,
                Column = Current.Column
            };

            Consume(SuslTokenType.SectionConfig, SuslErrorDefinitions.E0003_UnexpectedToken);
            Consume(SuslTokenType.LBrace, SuslErrorDefinitions.E0004_ExpectedSectionBody);

            while (!Check(SuslTokenType.RBrace) && !IsAtEnd)
            {
                section.Assignments.Add(ParseAssignment());
            }

            Consume(SuslTokenType.RBrace, SuslErrorDefinitions.E0003_UnexpectedToken);
            return section;
        }

        private AssignmentNode ParseAssignment()
        {
            SuslToken identifier;
            if (Check(SuslTokenType.Identifier) || Check(SuslTokenType.Tempo) || Check(SuslTokenType.TimeSignature))
            {
                identifier = Advance();
            }
            else
            {
                identifier = Consume(SuslTokenType.Identifier, SuslErrorDefinitions.E0003_UnexpectedToken);
            }

            var line = identifier.Line;
            var col = identifier.Column;

            Consume(SuslTokenType.Equal, SuslErrorDefinitions.E0007_InvalidAssignment);

            object value;
            switch (identifier.Lexeme.ToUpperInvariant())
            {
                case "TIMEBASE":
                    value = ParseIntegerValue(SuslErrorDefinitions.E0008_InvalidValue);
                    break;
                case "TEMPO":
                    value = ParseDoubleValue(SuslErrorDefinitions.E0008_InvalidValue);
                    break;
                case "TIMESIGNATURE":
                    value = ParseTimeSignatureValue(SuslErrorDefinitions.E0009_InvalidTimeSignature);
                    break;
                default:
                    throw new SuslException(SuslErrorDefinitions.E0008_InvalidValue, identifier.Line, identifier.Column, identifier.Lexeme, "Known configuration variable");
            }

            Consume(SuslTokenType.Semicolon, SuslErrorDefinitions.E0003_UnexpectedToken);
            return new AssignmentNode(identifier.Lexeme, value) { Line = line, Column = col };
        }

        private int ParseIntegerValue((string Code, string Message) error)
        {
            var token = Consume(SuslTokenType.Number, error);
            if (int.TryParse(token.Lexeme, out int value))
            {
                return value;
            }
            throw new SuslException(SuslErrorDefinitions.E0010_InvalidNumber, token.Line, token.Column, token.Lexeme);
        }

        private double ParseDoubleValue((string Code, string Message) error)
        {
            var token = Consume(SuslTokenType.Number, error);
            if (double.TryParse(token.Lexeme, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            {
                return value;
            }
            throw new SuslException(SuslErrorDefinitions.E0010_InvalidNumber, token.Line, token.Column, token.Lexeme);
        }

        private TimeSignature ParseTimeSignatureValue((string Code, string Message) error)
        {
            var token = Consume(SuslTokenType.String, error);
            var parts = token.Literal.Split('/');
            if (parts.Length == 2 && int.TryParse(parts[0], out int num) && int.TryParse(parts[1], out int den))
            {
                return new TimeSignature(num, den);
            }
            throw new SuslException(error, token.Line, token.Column);
        }

        private DefaultSectionNode ParseDefaultSection()
        {
            var section = new DefaultSectionNode
            {
                Line = Current.Line,
                Column = Current.Column
            };

            Consume(SuslTokenType.SectionDefault, SuslErrorDefinitions.E0003_UnexpectedToken);
            Consume(SuslTokenType.LBrace, SuslErrorDefinitions.E0004_ExpectedSectionBody);

            while (!Check(SuslTokenType.RBrace) && !IsAtEnd)
            {
                if (Check(SuslTokenType.Identifier))
                {
                    section.Expressions.Add(ParseParameterExpression());
                }
                else if (Match(SuslTokenType.Vibrato))
                {
                    if (section.DefaultVibrato != null)
                        throw Error(SuslErrorDefinitions.E0021_InvalidVibrato);

                    section.DefaultVibrato = ParseVibratoBlock();
                    Consume(SuslTokenType.Semicolon, SuslErrorDefinitions.E0003_UnexpectedToken);
                }
                else
                {
                    throw Error(SuslErrorDefinitions.E0003_UnexpectedToken, Current.Lexeme, "Identifier or Vibrato");
                }
            }

            Consume(SuslTokenType.RBrace, SuslErrorDefinitions.E0003_UnexpectedToken);
            return section;
        }

        private ParameterExpressionNode ParseParameterExpression()
        {
            var identifier = Consume(SuslTokenType.Identifier, SuslErrorDefinitions.E0003_UnexpectedToken);
            Consume(SuslTokenType.Equal, SuslErrorDefinitions.E0020_InvalidParameterExpression);
            var value = ParseDoubleValue(SuslErrorDefinitions.E0008_InvalidValue);
            Consume(SuslTokenType.Semicolon, SuslErrorDefinitions.E0003_UnexpectedToken);
            return new ParameterExpressionNode(identifier.Lexeme, value) { Line = identifier.Line, Column = identifier.Column };
        }

        private SequenceSectionNode ParseSequenceSection()
        {
            var section = new SequenceSectionNode
            {
                Line = Current.Line,
                Column = Current.Column
            };

            Consume(SuslTokenType.SectionSequence, SuslErrorDefinitions.E0003_UnexpectedToken);
            Consume(SuslTokenType.LBrace, SuslErrorDefinitions.E0004_ExpectedSectionBody);

            while (!Check(SuslTokenType.RBrace) && !IsAtEnd)
            {
                section.Commands.Add(ParseSequenceCommand());
            }

            Consume(SuslTokenType.RBrace, SuslErrorDefinitions.E0003_UnexpectedToken);
            return section;
        }

        private ISequenceCommand ParseSequenceCommand()
        {
            AtDefinitionNode? at = null;
            if (Check(SuslTokenType.At))
            {
                at = ParseAtDefinition();
            }

            BaseCommandNode command;

            if (Match(SuslTokenType.Note))
            {
                command = ParseNoteCommand();
            }
            else if (Match(SuslTokenType.Rest))
            {
                command = ParseRestCommand();
            }
            else if (Match(SuslTokenType.Tempo))
            {
                command = ParseTempoChangeCommand();
            }
            else if (Match(SuslTokenType.TimeSignature))
            {
                command = ParseTimeSignatureChangeCommand();
            }
            else
            {
                throw Error(SuslErrorDefinitions.E0014_InvalidCommand, Current.Lexeme);
            }

            command.AtPosition = at;
            if (at == null && !(command is TempoChangeCommandNode || command is TimeSignatureChangeCommandNode))
            {
                throw Error(SuslErrorDefinitions.E0012_ExpectedAt);
            }

            if (command is NoteCommandNode noteCommand)
            {
                if (Check(SuslTokenType.Identifier) && Current.Lexeme.ToUpperInvariant() == "PARAMETERS")
                {
                    Advance();
                    noteCommand.Parameters = ParseParametersBlock(SuslErrorDefinitions.E0018_InvalidParameterBlock);
                }
            }

            Consume(SuslTokenType.Semicolon, SuslErrorDefinitions.E0003_UnexpectedToken);
            return command;
        }

        private AtDefinitionNode ParseAtDefinition()
        {
            var atToken = Consume(SuslTokenType.At, SuslErrorDefinitions.E0012_ExpectedAt);
            Consume(SuslTokenType.LParen, SuslErrorDefinitions.E0013_InvalidPositionFormat);
            int measure = ParseIntegerValue(SuslErrorDefinitions.E0013_InvalidPositionFormat);
            Consume(SuslTokenType.Comma, SuslErrorDefinitions.E0013_InvalidPositionFormat);
            int beat = ParseIntegerValue(SuslErrorDefinitions.E0013_InvalidPositionFormat);
            Consume(SuslTokenType.Comma, SuslErrorDefinitions.E0013_InvalidPositionFormat);
            int tick = ParseIntegerValue(SuslErrorDefinitions.E0013_InvalidPositionFormat);
            Consume(SuslTokenType.RParen, SuslErrorDefinitions.E0013_InvalidPositionFormat);

            return new AtDefinitionNode(measure, beat, tick) { Line = atToken.Line, Column = atToken.Column };
        }

        private NoteCommandNode ParseNoteCommand()
        {
            var line = Current.Line;
            var col = Current.Column;

            Consume(SuslTokenType.LParen, SuslErrorDefinitions.E0003_UnexpectedToken);
            var length = ParseLength(SuslErrorDefinitions.E0015_InvalidLength);
            Consume(SuslTokenType.Comma, SuslErrorDefinitions.E0003_UnexpectedToken);
            var pitch = ParsePitch(SuslErrorDefinitions.E0016_InvalidPitch);
            Consume(SuslTokenType.Comma, SuslErrorDefinitions.E0003_UnexpectedToken);
            var lyricPhoneme = ParseLyricPhoneme(SuslErrorDefinitions.E0017_InvalidLyricPhoneme);

            Consume(SuslTokenType.RParen, SuslErrorDefinitions.E0003_UnexpectedToken);

            var noteCmd = new NoteCommandNode(length, pitch, lyricPhoneme)
            {
                Parameters = null,
                Line = line,
                Column = col
            };
            return noteCmd;
        }

        private LyricPhonemeNode ParseLyricPhoneme((string Code, string Message) error)
        {
            var line = Current.Line;
            var col = Current.Column;

            if (Current.Type == SuslTokenType.String)
            {
                var lyricToken = Advance();
                if (Match(SuslTokenType.Colon))
                {
                    var phonemeToken = Consume(SuslTokenType.String, error);
                    return new LyricPhonemeNode(lyricToken.Literal, phonemeToken.Literal) { Line = line, Column = col };
                }
                return new LyricPhonemeNode(lyricToken.Literal, null) { Line = line, Column = col };
            }
            else if (Match(SuslTokenType.Colon))
            {
                var phonemeToken = Consume(SuslTokenType.String, error);
                return new LyricPhonemeNode(null, phonemeToken.Literal) { Line = line, Column = col };
            }

            throw new SuslException(error, line, col);
        }

        private LengthDefinitionNode ParseLength((string Code, string Message) error)
        {
            var line = Current.Line;
            var col = Current.Column;

            if (Current.Type == SuslTokenType.Number)
            {
                var token = Advance();
                if (int.TryParse(token.Lexeme, out int ticks))
                {
                    return new LengthDefinitionNode(ticks) { Line = line, Column = col };
                }
            }
            else if (Current.Type == SuslTokenType.Identifier)
            {
                var token = Advance();
                bool dotted = Match(SuslTokenType.Dot);
                return new LengthDefinitionNode(token.Lexeme, dotted) { Line = line, Column = col };
            }

            throw new SuslException(error, line, col);
        }

        private PitchDefinitionNode ParsePitch((string Code, string Message) error)
        {
            var line = Current.Line;
            var col = Current.Column;

            if (Current.Type == SuslTokenType.Identifier)
            {
                var token = Advance();
                return new PitchDefinitionNode(PitchType.Scientific, token.Lexeme) { Line = line, Column = col };
            }
            else if (Current.Type == SuslTokenType.Number)
            {
                var token = Advance();
                if (int.TryParse(token.Lexeme, out int midiNote))
                {
                    return new PitchDefinitionNode(PitchType.MidiNote, midiNote) { Line = line, Column = col };
                }
            }
            else if (Match(SuslTokenType.Plus) || Match(SuslTokenType.Minus))
            {
                var op = _tokens[_position - 1];
                var token = Consume(SuslTokenType.Number, error);
                if (int.TryParse(token.Lexeme, out int relative))
                {
                    if (op.Type == SuslTokenType.Minus)
                        relative = -relative;
                    return new PitchDefinitionNode(PitchType.Relative, relative) { Line = line, Column = col };
                }
            }

            throw new SuslException(error, line, col);
        }

        private ParametersBlockNode ParseParametersBlock((string Code, string Message) error)
        {
            var block = new ParametersBlockNode
            {
                Line = Current.Line,
                Column = Current.Column
            };

            Consume(SuslTokenType.LBrace, error);

            while (!Check(SuslTokenType.RBrace) && !IsAtEnd)
            {
                if (Check(SuslTokenType.Vibrato))
                {
                    Advance();
                    if (Match(SuslTokenType.Off))
                    {
                        block.VibratoHandling = VibratoHandling.Off;
                    }
                    else if (Check(SuslTokenType.LBrace))
                    {
                        block.VibratoHandling = VibratoHandling.Override;
                        block.VibratoOverride = ParseVibratoBlock();
                    }
                    else
                    {
                        block.VibratoHandling = VibratoHandling.Default;
                    }
                    Consume(SuslTokenType.Semicolon, SuslErrorDefinitions.E0003_UnexpectedToken);
                }
                else if (Check(SuslTokenType.PitchCurve))
                {
                    Advance();
                    block.PitchCurve = ParsePitchCurve();
                    Consume(SuslTokenType.Semicolon, SuslErrorDefinitions.E0003_UnexpectedToken);
                }
                else if (Check(SuslTokenType.Identifier))
                {
                    block.Expressions.Add(ParseParameterExpression());
                }
                else
                {
                    throw Error(SuslErrorDefinitions.E0003_UnexpectedToken, Current.Lexeme, "Parameter, Vibrato, or PitchCurve");
                }
            }

            Consume(SuslTokenType.RBrace, error);
            return block;
        }

        private VibratoBlockNode ParseVibratoBlock()
        {
            var block = new VibratoBlockNode
            {
                Line = Current.Line,
                Column = Current.Column
            };
            Consume(SuslTokenType.LBrace, SuslErrorDefinitions.E0021_InvalidVibrato);

            while (Match(SuslTokenType.Identifier))
            {
                var prop = _tokens[_position - 1];
                Consume(SuslTokenType.Equal, SuslErrorDefinitions.E0007_InvalidAssignment);
                var value = ParseDoubleValue(SuslErrorDefinitions.E0008_InvalidValue);
                Consume(SuslTokenType.Semicolon, SuslErrorDefinitions.E0003_UnexpectedToken);

                switch (prop.Lexeme.ToLower())
                {
                    case "period": block.Period = value; break;
                    case "depth": block.Depth = value; break;
                    case "fadein": block.FadeIn = value; break;
                    case "fadeout": block.FadeOut = value; break;
                    default: throw new SuslException(SuslErrorDefinitions.E0008_InvalidValue, prop.Line, prop.Column, prop.Lexeme, "Vibrato property");
                }
            }

            Consume(SuslTokenType.RBrace, SuslErrorDefinitions.E0021_InvalidVibrato);
            return block;
        }

        private PitchCurveBlockNode ParsePitchCurve()
        {
            var block = new PitchCurveBlockNode
            {
                Line = Current.Line,
                Column = Current.Column,
                IsBezier = false
            };

            if (Match(SuslTokenType.Bezier))
            {
                block.IsBezier = true;
            }

            Consume(SuslTokenType.LBrace, SuslErrorDefinitions.E0022_InvalidPitchCurve);

            while (Check(SuslTokenType.LParen))
            {
                Match(SuslTokenType.LParen);
                block.Points.Add(ParsePitchPoint());
                Consume(SuslTokenType.RParen, SuslErrorDefinitions.E0023_InvalidPitchPoint);

                if (Check(SuslTokenType.Comma))
                {
                    Advance();
                    continue;
                }
                else if (Check(SuslTokenType.RBrace))
                {
                    break;
                }
                else
                {
                    throw Error(SuslErrorDefinitions.E0003_UnexpectedToken, Current.Lexeme, "Comma or }");
                }
            }

            Consume(SuslTokenType.RBrace, SuslErrorDefinitions.E0022_InvalidPitchCurve);
            return block;
        }

        private PitchPointNode ParsePitchPoint()
        {
            var line = Current.Line;
            var col = Current.Column;

            int tick = ParseIntegerValue(SuslErrorDefinitions.E0023_InvalidPitchPoint);
            Consume(SuslTokenType.Comma, SuslErrorDefinitions.E0023_InvalidPitchPoint);
            double cent = ParseDoubleValue(SuslErrorDefinitions.E0023_InvalidPitchPoint);

            string? shape = null;
            if (Match(SuslTokenType.Comma))
            {
                var shapeToken = Consume(SuslTokenType.String, SuslErrorDefinitions.E0023_InvalidPitchPoint);
                shape = shapeToken.Literal;
            }

            return new PitchPointNode(tick, cent, shape) { Line = line, Column = col };
        }

        private RestCommandNode ParseRestCommand()
        {
            var line = Current.Line;
            var col = Current.Column;
            Consume(SuslTokenType.LParen, SuslErrorDefinitions.E0003_UnexpectedToken);
            var length = ParseLength(SuslErrorDefinitions.E0015_InvalidLength);
            Consume(SuslTokenType.RParen, SuslErrorDefinitions.E0003_UnexpectedToken);
            return new RestCommandNode(length) { Line = line, Column = col };
        }

        private TempoChangeCommandNode ParseTempoChangeCommand()
        {
            var line = Current.Line;
            var col = Current.Column;
            Consume(SuslTokenType.LParen, SuslErrorDefinitions.E0003_UnexpectedToken);
            var tempo = ParseDoubleValue(SuslErrorDefinitions.E0008_InvalidValue);
            Consume(SuslTokenType.RParen, SuslErrorDefinitions.E0003_UnexpectedToken);
            return new TempoChangeCommandNode(tempo) { Line = line, Column = col };
        }

        private TimeSignatureChangeCommandNode ParseTimeSignatureChangeCommand()
        {
            var line = Current.Line;
            var col = Current.Column;
            Consume(SuslTokenType.LParen, SuslErrorDefinitions.E0003_UnexpectedToken);
            var ts = ParseTimeSignatureValue(SuslErrorDefinitions.E0009_InvalidTimeSignature);
            Consume(SuslTokenType.RParen, SuslErrorDefinitions.E0003_UnexpectedToken);
            return new TimeSignatureChangeCommandNode(ts.Numerator, ts.Denominator) { Line = line, Column = col };
        }
    }
}
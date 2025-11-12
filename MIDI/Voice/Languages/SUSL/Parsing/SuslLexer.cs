using Microsoft.VisualBasic;
using MIDI.Voice.SUSL.Errors;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MIDI.Voice.SUSL.Parsing
{
    public class SuslLexer
    {
        private readonly string _source;
        private int _position;
        private int _line;
        private int _column;

        public SuslLexer(string source)
        {
            _source = source;
            _position = 0;
            _line = 1;
            _column = 1;
        }

        private char Peek() => _position >= _source.Length ? '\0' : _source[_position];
        private char Advance()
        {
            if (_position >= _source.Length) return '\0';
            char c = _source[_position];
            _position++;
            if (c == '\n')
            {
                _line++;
                _column = 1;
            }
            else
            {
                _column++;
            }
            return c;
        }

        public List<SuslToken> Tokenize()
        {
            var tokens = new List<SuslToken>();

            while (_position < _source.Length && char.IsWhiteSpace(Peek()))
            {
                Advance();
            }

            if (_position < _source.Length && _source.Substring(_position).StartsWith(Constants.SuslConstants.Header))
            {
                int startLine = _line;
                int startCol = _column;

                int headerEnd = _source.IndexOf('\n', _position);
                if (headerEnd == -1) headerEnd = _source.Length;

                string header = _source.Substring(_position, headerEnd - _position).Trim();
                tokens.Add(new SuslToken(SuslTokenType.Header, header, header, startLine, startCol));

                _position = headerEnd;
                if (_position < _source.Length && Peek() == '\n') Advance();
            }
            else
            {
                throw new SuslException(SuslErrorDefinitions.E0001_InvalidHeader, 1, 1);
            }

            while (_position < _source.Length)
            {
                char c = Peek();

                if (char.IsWhiteSpace(c))
                {
                    Advance();
                    continue;
                }

                if (c == '/' && _position + 1 < _source.Length && _source[_position + 1] == '*')
                {
                    ConsumeBlockComment();
                    continue;
                }

                int startLine = _line;
                int startCol = _column;

                if (char.IsLetter(c) || c == '_')
                {
                    tokens.Add(ScanIdentifier(startLine, startCol));
                }
                else if (char.IsDigit(c) || (c == '-' && _position + 1 < _source.Length && char.IsDigit(_source[_position + 1])))
                {
                    tokens.Add(ScanNumber(startLine, startCol));
                }
                else if (c == '"')
                {
                    tokens.Add(ScanString(startLine, startCol));
                }
                else if ("{()=;,:.@+-/}".IndexOf(c) != -1)
                {
                    tokens.Add(new SuslToken(CharToTokenType(c), c.ToString(), null, startLine, startCol));
                    Advance();
                }
                else if (c == '[' || c == ']')
                {
                    throw new SuslException(SuslErrorDefinitions.E0003_UnexpectedToken, startLine, startCol, c.ToString());
                }
                else
                {
                    throw new SuslException(SuslErrorDefinitions.E0003_UnexpectedToken, startLine, startCol, c.ToString());
                }
            }

            tokens.Add(new SuslToken(SuslTokenType.EOF, "EOF", null, _line, _column));
            return tokens;
        }

        private void ConsumeBlockComment()
        {
            Advance();
            Advance();

            int depth = 1;

            while (_position < _source.Length)
            {
                if (Peek() == '*' && _position + 1 < _source.Length && _source[_position + 1] == '/')
                {
                    Advance();
                    Advance();
                    depth--;
                    if (depth == 0) return;
                }
                else if (Peek() == '/' && _position + 1 < _source.Length && _source[_position + 1] == '*')
                {
                    Advance();
                    Advance();
                    depth++;
                }
                else
                {
                    Advance();
                }
            }
        }

        private SuslToken ScanIdentifier(int line, int col)
        {
            int start = _position;
            while (char.IsLetterOrDigit(Peek()) || Peek() == '_' || Peek() == '-' || Peek() == '#')
            {
                Advance();
            }
            string text = _source.Substring(start, _position - start).ToUpperInvariant();

            var type = SuslTokenType.Identifier;
            switch (text)
            {
                case "CONFIG": type = SuslTokenType.SectionConfig; break;
                case "DEFAULT": type = SuslTokenType.SectionDefault; break;
                case "SEQUENCE": type = SuslTokenType.SectionSequence; break;

                case "NOTE": type = SuslTokenType.Note; break;
                case "REST": type = SuslTokenType.Rest; break;
                case "TEMPO": type = SuslTokenType.Tempo; break;
                case "TIMESIGNATURE": type = SuslTokenType.TimeSignature; break;
                case "VIBRATO": type = SuslTokenType.Vibrato; break;
                case "PITCHCURVE": type = SuslTokenType.PitchCurve; break;
                case "BEZIER": type = SuslTokenType.Bezier; break;
                case "OFF": type = SuslTokenType.Off; break;
                case "DOT": type = SuslTokenType.Dot; break;
                case "AT": type = SuslTokenType.At; break;

                default:
                    if (Regex.IsMatch(text, @"^[A-G]#?[0-9]$"))
                    {
                        type = SuslTokenType.Identifier;
                    }
                    else if (Constants.SuslConstants.LengthConstants.Contains(text))
                    {
                        type = SuslTokenType.Identifier;
                    }
                    else
                    {
                        type = SuslTokenType.Identifier;
                    }
                    break;
            }

            return new SuslToken(type, text, null, line, col);
        }

        private SuslToken ScanNumber(int line, int col)
        {
            int start = _position;

            if (Peek() == '-')
            {
                Advance();
            }

            while (char.IsDigit(Peek()))
            {
                Advance();
            }

            if (Peek() == '.')
            {
                char next = (_position + 1 < _source.Length) ? _source[_position + 1] : '\0';
                if (char.IsDigit(next))
                {
                    Advance();
                    while (char.IsDigit(Peek()))
                    {
                        Advance();
                    }
                }
            }

            string text = _source.Substring(start, _position - start);
            return new SuslToken(SuslTokenType.Number, text, null, line, col);
        }

        private SuslToken ScanString(int line, int col)
        {
            Advance();
            int start = _position;
            while (Peek() != '"' && _position < _source.Length)
            {
                Advance();
            }

            if (_position >= _source.Length)
            {
                throw new SuslException(SuslErrorDefinitions.E0003_UnexpectedToken, line, col, "Unterminated string");
            }

            string literal = _source.Substring(start, _position - start);
            Advance();
            string lexeme = _source.Substring(start - 1, _position - (start - 1));
            return new SuslToken(SuslTokenType.String, lexeme, literal, line, col);
        }

        private SuslTokenType CharToTokenType(char c)
        {
            switch (c)
            {
                case '{': return SuslTokenType.LBrace;
                case '}': return SuslTokenType.RBrace;
                case '(': return SuslTokenType.LParen;
                case ')': return SuslTokenType.RParen;
                case '=': return SuslTokenType.Equal;
                case ';': return SuslTokenType.Semicolon;
                case ',': return SuslTokenType.Comma;
                case ':': return SuslTokenType.Colon;
                case '.': return SuslTokenType.Dot;
                case '@': return SuslTokenType.At;
                case '+': return SuslTokenType.Plus;
                case '-': return SuslTokenType.Minus;
                case '/': return SuslTokenType.Slash;
                default: return SuslTokenType.Unknown;
            }
        }
    }
}
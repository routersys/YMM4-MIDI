using MIDI.Utils;
using MIDI.Voice.Languages.Core;
using MIDI.Voice.Languages.Interface;
using MIDI.Voice.SUSL.Errors;
using MIDI.Voice.SUSL.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using MIDI.Voice.SUSL.Constants;

namespace MIDI.Voice.Languages.SUSL
{
    public class SuslParserAdapter : ILanguageParser
    {
        public string LanguageName => "SUSL";

        public int CheckConfidence(string inputText)
        {
            if (string.IsNullOrWhiteSpace(inputText))
            {
                return 0;
            }

            var firstLine = inputText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                     .FirstOrDefault()?.Trim();

            if (firstLine != null && firstLine == SuslConstants.Header)
            {
                return 100;
            }
            return 0;
        }

        public IParseResult Parse(string inputText)
        {
            var errors = new List<IParseError>();
            try
            {
                var lexer = new SuslLexer(inputText);
                var tokens = lexer.Tokenize();
                var parser = new SuslParser(tokens);
                var ast = parser.ParseProgram();
                return new ParseResult(ast, LanguageName, errors);
            }
            catch (SuslException ex)
            {
                Logger.Error("SUSLの解析に失敗しました。", ex);
                errors.Add(new ParseError(ex.ErrorCode, 'S', ex.Line, ex.Column, 0, ex.Message));
                return new ParseResult(null, LanguageName, errors);
            }
            catch (Exception ex)
            {
                Logger.Error("SUSLの解析中に予期せぬエラーが発生しました。", ex);
                errors.Add(new ParseError("SUSL_UnexpectedError", 'S', 1, 1, 0, ex.Message));
                return new ParseResult(null, LanguageName, errors);
            }
        }

        public IReadOnlyDictionary<long, string> GetErrorDefinitions()
        {
            return new Dictionary<long, string>();
        }
    }
}
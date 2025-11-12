using MIDI.Utils;
using MIDI.Voice.EMEL;
using MIDI.Voice.EMEL.Errors;
using MIDI.Voice.Languages.ACS;
using MIDI.Voice.Languages.Core;
using MIDI.Voice.Languages.Interface;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MIDI.Voice.Languages.EMEL
{
    public class EmelParserAdapter : ILanguageParser
    {
        public string LanguageName => "EMEL";

        public int CheckConfidence(string inputText)
        {
            if (string.IsNullOrWhiteSpace(inputText))
            {
                return 0;
            }

            var firstLine = inputText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                     .FirstOrDefault()?.Trim();

            if (firstLine != null && (firstLine == "#!EMEL" || firstLine == "#!EMEL2"))
            {
                return 100;
            }
            return 0;
        }

        public IParseResult Parse(string inputText)
        {
            var errors = new List<IParseError>();
            string compiledAcs;

            try
            {
                var compiler = new EmelCompiler();
                compiledAcs = compiler.Compile(inputText);
            }
            catch (EmelException ex)
            {
                Logger.Error("EMELの解析に失敗しました。", ex);
                errors.Add(new ParseError(ex.ErrorCode.ToString(), 'E', ex.Line, ex.Column, 0, ex.Message));
                var errorEvents = new List<object>
                {
                    NoteData.CreateErrorNote(1, 0.0, 0.5f)
                };
                return new ParseResult(errorEvents, LanguageName, errors);
            }
            catch (Exception ex)
            {
                Logger.Error("EMELのコンパイル中に予期せぬエラーが発生しました。", ex);
                errors.Add(new ParseError("EMEL_UnexpectedError", 'E', 1, 1, 0, ex.Message));
                var errorEvents = new List<object>
                {
                    NoteData.CreateErrorNote(1, 0.0, 0.5f)
                };
                return new ParseResult(errorEvents, LanguageName, errors);
            }

            var acsParser = new AcsParser();
            var acsResult = acsParser.Parse(compiledAcs);

            errors.AddRange(acsResult.Errors);

            return new ParseResult(acsResult.Output, LanguageName, errors);
        }

        public IReadOnlyDictionary<long, string> GetErrorDefinitions()
        {
            return new Dictionary<long, string>();
        }
    }
}
using MIDI.Voice.Languages.Interface;
using System.Collections.Generic;
using System.Linq;

namespace MIDI.Voice.Languages.Core
{
    public class ParsingOrchestrator
    {
        private readonly List<ILanguageParser> _parsers;

        public ParsingOrchestrator(IEnumerable<ILanguageParser> parsers)
        {
            _parsers = parsers?.ToList() ?? new List<ILanguageParser>();
        }

        public IParseResult Parse(string inputText)
        {
            if (string.IsNullOrWhiteSpace(inputText))
            {
                return new ParseResult(null, "Orchestrator", new List<IParseError>());
            }

            var candidates = _parsers
                .Select(p => new { Parser = p, Confidence = p.CheckConfidence(inputText) })
                .Where(c => c.Confidence > 0)
                .OrderByDescending(c => c.Confidence)
                .ToList();

            if (!candidates.Any())
            {
                return new ParseResult(
                    null,
                    "Orchestrator",
                    new List<IParseError> { new ParseError("E0000_NoParserFound", 'O', 1, 1, 0) }
                );
            }

            List<IParseError> errors = new List<IParseError>();

            foreach (var candidate in candidates)
            {
                var result = candidate.Parser.Parse(inputText);
                if (result.IsSuccess)
                {
                    return result;
                }
                else
                {
                    errors.AddRange(result.Errors);
                }
            }

            return new ParseResult(null, candidates.First().Parser.LanguageName, errors);
        }
    }
}
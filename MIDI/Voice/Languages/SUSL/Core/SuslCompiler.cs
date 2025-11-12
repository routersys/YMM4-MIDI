using System;
using System.IO;
using System.Linq;
using System.Text;
using MIDI.Voice.SUSL.Errors;
using MIDI.Voice.SUSL.Generation;
using MIDI.Voice.SUSL.Parsing;

namespace MIDI.Voice.SUSL.Core
{
    public class SuslCompiler
    {
        public byte[] Compile(string sourceText)
        {
            if (string.IsNullOrEmpty(sourceText))
            {
                throw new SuslException(SuslErrorDefinitions.E0001_InvalidHeader, 0, 0);
            }

            var firstLine = sourceText.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                                      .FirstOrDefault()?.Trim();

            if (firstLine != null && firstLine.StartsWith(Constants.SuslConstants.Header))
            {
            }
            else if (firstLine != null && (firstLine.StartsWith("#!EMEL") || firstLine.StartsWith("EMEL")))
            {
                throw new SuslException(SuslErrorDefinitions.E0019_WrongLanguageHeader, 1, 1, firstLine);
            }
            else
            {
                throw new SuslException(SuslErrorDefinitions.E0001_InvalidHeader, 1, 1);
            }

            var lexer = new SuslLexer(sourceText);
            var tokens = lexer.Tokenize();

            var parser = new SuslParser(tokens);
            var ast = parser.ParseProgram();

            var context = new SuslContext();

            ast.ConfigurationSection?.Evaluate(context);
            ast.DefaultSection?.Evaluate(context);

            var generator = new SuslBinaryGenerator(context);

            using (var memoryStream = new MemoryStream())
            using (var writer = new BinaryWriter(memoryStream, Encoding.UTF8, false))
            {
                generator.Generate(ast, writer);
                return memoryStream.ToArray();
            }
        }
    }
}
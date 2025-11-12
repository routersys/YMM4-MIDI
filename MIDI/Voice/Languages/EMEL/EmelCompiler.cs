using MIDI.Voice.EMEL.Errors;
using MIDI.Voice.EMEL.Execution;
using MIDI.Voice.EMEL.Generation;
using MIDI.Voice.EMEL.Parsing;
using MIDI.Voice.SUSL.Core;
using MIDI.Voice.SUSL.Errors;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MIDI.Voice.EMEL
{
    public class EmelCompiler
    {
        public string Compile(string code)
        {
            string normalizedCode = code.Replace("\r\n", "\n");
            string[] lines = normalizedCode.Split('\n');

            int firstLineIndex = -1;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    firstLineIndex = i;
                    break;
                }
            }

            if (firstLineIndex == -1)
            {
                throw new EmelException(EmelErrorCode.Compiler_MissingEmelDeclaration, 1, 1);
            }

            string firstLine = lines[firstLineIndex].Trim();

            if (!firstLine.StartsWith("#!"))
            {
                throw new EmelException(EmelErrorCode.Compiler_MissingEmelDeclaration, firstLineIndex + 1, 1);
            }

            if (firstLine == "#!EMEL2")
            {
            }
            else if (firstLine == "#!EMEL")
            {
            }
            else
            {
                throw new EmelException(EmelErrorCode.Compiler_InvalidEmelDeclaration, firstLineIndex + 1, 1, firstLine);
            }

            string codeBody = string.Join("\n", lines.Skip(firstLineIndex + 1));

            try
            {
                var tokens = EmelParser.Lex(codeBody);
                var parser = new EmelParser(tokens);
                var ast = parser.ParseProgram();

                var globalContext = new EmelContext(null);
                var builtinFunctions = BuiltinFunctions.LoadDefinitions();
                var generator = new EmelGenerator(globalContext, builtinFunctions);

                generator.VisitProgramNode((ProgramNode)ast);

                return generator.Output.ToString();
            }
            catch (EmelException ex)
            {
                throw new EmelException(ex.Message, ex.Line + (firstLineIndex + 1), ex.Column);
            }
            catch (System.Exception ex)
            {
                throw new EmelException(EmelErrorCode.Compiler_Unexpected, firstLineIndex + 1, 1, ex.Message);
            }
        }
    }
}
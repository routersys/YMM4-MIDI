using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using MIDI.Voice.EMEL.Generation;
using MIDI.Voice.EMEL.Parsing;
using MIDI.Voice.EMEL.Errors;
using System.Text.RegularExpressions;

namespace MIDI.Voice.EMEL.Execution
{
    public interface IEmelFunction
    {
        string Name { get; }
        int Arity { get; }
        object? Invoke(EmelGenerator generator, FunctionCallNode callNode, List<object?> args);
    }

    public class FunctionDefinition
    {
        public string Name { get; }
        public int Arity { get; }
        public IEmelFunction Implementation { get; }

        public FunctionDefinition(string name, int arity, IEmelFunction implementation)
        {
            Name = name;
            Arity = arity;
            Implementation = implementation;
        }
    }

    public static class BuiltinFunctions
    {
        public static Dictionary<string, FunctionDefinition> LoadDefinitions()
        {
            var functions = new List<IEmelFunction>
            {
                new NoteFunction(),
                new NoteExFunction(),
                new RestFunction(),
                new CcFunction(),
                new ProgramFunction(),
                new PitchBendFunction(),
                new ChannelPressureFunction(),
                new ChordFunction()
            };

            return functions.ToDictionary(f => f.Name, f => new FunctionDefinition(f.Name, f.Arity, f));
        }
    }

    internal static class GeneratorHelpers
    {
        public static string FormatValue(object? val)
        {
            if (val is double d) return d.ToString(CultureInfo.InvariantCulture);
            if (val is int i) return i.ToString(CultureInfo.InvariantCulture);
            return val?.ToString() ?? "";
        }

        public static string FormatMidiValue(object? val)
        {
            if (val == null) return "0";
            if (val is double d)
            {
                if (d > 1.0)
                {
                    return ((int)Math.Round(d)).ToString(CultureInfo.InvariantCulture);
                }
                return ((int)Math.Round(d * 127)).ToString(CultureInfo.InvariantCulture);
            }
            if (val is int i) return i.ToString(CultureInfo.InvariantCulture);
            return val.ToString() ?? "0";
        }

        public static double ToDouble(object? arg, double defaultVal)
        {
            if (arg == null) return defaultVal;
            if (double.TryParse(arg.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
            {
                return d;
            }
            return defaultVal;
        }

        public static double ExpectNumber(object? arg, AstNode argNode, string paramName)
        {
            if (arg is double d) return d;
            if (arg is int i) return i;
            throw new EmelException(EmelErrorCode.Runtime_InvalidType, argNode.StartToken.Line, argNode.StartToken.Column, arg?.GetType().Name ?? "null");
        }
    }

    internal class NoteFunction : IEmelFunction
    {
        public string Name => "Note";
        public int Arity => 2;
        public object? Invoke(EmelGenerator generator, FunctionCallNode callNode, List<object?> args)
        {
            var context = generator.Context;

            string vStr = GeneratorHelpers.FormatMidiValue(context.Get("__V"));
            string lStr = GeneratorHelpers.FormatMidiValue(context.Get("__L"));
            string pbStr = GeneratorHelpers.FormatValue(context.Get("__PB"));
            string mStr = GeneratorHelpers.FormatMidiValue(context.Get("__M"));
            string eStr = GeneratorHelpers.FormatMidiValue(context.Get("__E"));
            string pStr = GeneratorHelpers.FormatMidiValue(context.Get("__P"));
            string cpStr = GeneratorHelpers.FormatMidiValue(context.Get("__CP"));

            generator.Output.AppendLine($"'{GeneratorHelpers.FormatValue(args[0])}D{GeneratorHelpers.FormatValue(args[1])}V{vStr}L{lStr}PB{pbStr}M{mStr}E{eStr}P{pStr}CP{cpStr}'");
            return null;
        }
    }

    internal class NoteExFunction : IEmelFunction
    {
        public string Name => "NoteEx";
        public int Arity => 9;
        public object? Invoke(EmelGenerator generator, FunctionCallNode callNode, List<object?> args)
        {
            double dDuration = GeneratorHelpers.ExpectNumber(args[1], callNode.Arguments[1], "Duration");

            if (dDuration == 0.0)
            {
                var context = generator.Context;

                context.Define("__V", GeneratorHelpers.ToDouble(args[2], 100.0));
                context.Define("__L", GeneratorHelpers.ToDouble(args[3], 0.0));
                context.Define("__PB", GeneratorHelpers.ToDouble(args[4], 0.0));
                context.Define("__M", GeneratorHelpers.ToDouble(args[5], 0.0));

                double e_arg = GeneratorHelpers.ToDouble(args[6], 127.0);
                context.Define("__E", e_arg == 0.0 ? 127.0 : e_arg);

                double p_arg = GeneratorHelpers.ToDouble(args[7], 64.0);
                context.Define("__P", p_arg == 0.0 ? 64.0 : p_arg);

                context.Define("__CP", GeneratorHelpers.ToDouble(args[8], 0.0));

                return null;
            }

            string v = GeneratorHelpers.FormatMidiValue(args[2]);
            string l = GeneratorHelpers.FormatMidiValue(args[3]);
            string pb = GeneratorHelpers.FormatValue(args[4]);
            string m = GeneratorHelpers.FormatMidiValue(args[5]);
            string e = GeneratorHelpers.FormatMidiValue(args[6]);
            string p = GeneratorHelpers.FormatMidiValue(args[7]);
            string cp = GeneratorHelpers.FormatMidiValue(args[8]);

            generator.Output.AppendLine($"'{GeneratorHelpers.FormatValue(args[0])}D{GeneratorHelpers.FormatValue(args[1])}V{v}L{l}PB{pb}M{m}E{e}P{p}CP{cp}'");
            return null;
        }
    }

    internal class RestFunction : IEmelFunction
    {
        public string Name => "Rest";
        public int Arity => 1;
        public object? Invoke(EmelGenerator generator, FunctionCallNode callNode, List<object?> args)
        {
            generator.Output.AppendLine($"'RD{GeneratorHelpers.FormatValue(args[0])}'");
            return null;
        }
    }

    internal class CcFunction : IEmelFunction
    {
        public string Name => "CC";
        public int Arity => 2;
        public object? Invoke(EmelGenerator generator, FunctionCallNode callNode, List<object?> args)
        {
            double ccNum = GeneratorHelpers.ExpectNumber(args[0], callNode.Arguments[0], "CC Number");
            double ccVal = GeneratorHelpers.ExpectNumber(args[1], callNode.Arguments[1], "CC Value");

            if (ccNum < 0 || ccNum > 127)
            {
                throw new EmelException(EmelErrorCode.Runtime_ValueOutOfRange, callNode.Arguments[0].StartToken.Line, callNode.Arguments[0].StartToken.Column, "CC番号", (int)ccNum, 0, 127);
            }
            if (ccVal < 0 || ccVal > 127)
            {
                throw new EmelException(EmelErrorCode.Runtime_ValueOutOfRange, callNode.Arguments[1].StartToken.Line, callNode.Arguments[1].StartToken.Column, "CC値", (int)ccVal, 0, 127);
            }

            generator.Output.AppendLine($"'<CC={GeneratorHelpers.FormatValue(args[0])} V={GeneratorHelpers.FormatMidiValue(args[1])}>'");
            return null;
        }
    }

    internal class ProgramFunction : IEmelFunction
    {
        public string Name => "Program";
        public int Arity => 1;
        public object? Invoke(EmelGenerator generator, FunctionCallNode callNode, List<object?> args)
        {
            double progNum = GeneratorHelpers.ExpectNumber(args[0], callNode.Arguments[0], "Program Number");
            if (progNum < 0 || progNum > 127)
            {
                throw new EmelException(EmelErrorCode.Runtime_ValueOutOfRange, callNode.Arguments[0].StartToken.Line, callNode.Arguments[0].StartToken.Column, "プログラム番号", (int)progNum, 0, 127);
            }
            generator.Output.AppendLine($"'<Program={GeneratorHelpers.FormatValue(args[0])}>'");
            return null;
        }
    }

    internal class PitchBendFunction : IEmelFunction
    {
        public string Name => "PitchBend";
        public int Arity => 1;
        public object? Invoke(EmelGenerator generator, FunctionCallNode callNode, List<object?> args)
        {
            double pbVal = GeneratorHelpers.ExpectNumber(args[0], callNode.Arguments[0], "PitchBend Value");
            if (pbVal < -8192 || pbVal > 8191)
            {
                throw new EmelException(EmelErrorCode.Runtime_ValueOutOfRange, callNode.Arguments[0].StartToken.Line, callNode.Arguments[0].StartToken.Column, "ピッチベンド値", (int)pbVal, -8192, 8191);
            }
            generator.Output.AppendLine($"'<PitchBend={GeneratorHelpers.FormatValue(args[0])}>'");
            return null;
        }
    }

    internal class ChannelPressureFunction : IEmelFunction
    {
        public string Name => "ChannelPressure";
        public int Arity => 1;
        public object? Invoke(EmelGenerator generator, FunctionCallNode callNode, List<object?> args)
        {
            double cpVal = GeneratorHelpers.ExpectNumber(args[0], callNode.Arguments[0], "ChannelPressure Value");
            if (cpVal < 0 || cpVal > 127)
            {
                throw new EmelException(EmelErrorCode.Runtime_ValueOutOfRange, callNode.Arguments[0].StartToken.Line, callNode.Arguments[0].StartToken.Column, "チャンネルプレッシャー値", (int)cpVal, 0, 127);
            }
            generator.Output.AppendLine($"'<ChannelPressure={GeneratorHelpers.FormatMidiValue(args[0])}>'");
            return null;
        }
    }

    internal class ChordFunction : IEmelFunction
    {
        public string Name => "Chord";
        public int Arity => 2;
        public object? Invoke(EmelGenerator generator, FunctionCallNode callNode, List<object?> args)
        {
            var pitches = new List<string>();
            var duration = args[1];

            if (args[0] is List<object?> pitchList)
            {
                pitches.AddRange(pitchList.Select(p => p?.ToString() ?? "C4"));
            }
            else if (args[0] is string chordName)
            {
                try
                {
                    pitches.AddRange(PitchMath.ResolveChord(chordName));
                }
                catch (Exception ex)
                {
                    throw new EmelException(ex.Message, callNode.Arguments[0].StartToken.Line, callNode.Arguments[0].StartToken.Column);
                }
            }
            else
            {
                throw new EmelException(EmelErrorCode.Runtime_InvalidType, callNode.Arguments[0].StartToken.Line, callNode.Arguments[0].StartToken.Column, args[0]?.GetType().Name ?? "null");
            }

            var context = generator.Context;
            string vStr = GeneratorHelpers.FormatMidiValue(context.Get("__V"));
            string lStr = GeneratorHelpers.FormatMidiValue(context.Get("__L"));
            string pbStr = GeneratorHelpers.FormatValue(context.Get("__PB"));
            string mStr = GeneratorHelpers.FormatMidiValue(context.Get("__M"));
            string eStr = GeneratorHelpers.FormatMidiValue(context.Get("__E"));
            string pStr = GeneratorHelpers.FormatMidiValue(context.Get("__P"));
            string cpStr = GeneratorHelpers.FormatMidiValue(context.Get("__CP"));

            string durationStr = GeneratorHelpers.FormatValue(duration);

            for (int i = 0; i < pitches.Count; i++)
            {
                string pitch = pitches[i];
                string currentDurationStr = "0";
                string currentLegatoStr = lStr;

                if (i == pitches.Count - 1)
                {
                    currentDurationStr = durationStr;
                }
                else
                {
                    currentLegatoStr = durationStr;
                }

                generator.Output.AppendLine($"'{GeneratorHelpers.FormatValue(pitch)}D{currentDurationStr}V{vStr}L{currentLegatoStr}PB{pbStr}M{mStr}E{eStr}P{pStr}CP{cpStr}'");
            }

            return null;
        }
    }
}
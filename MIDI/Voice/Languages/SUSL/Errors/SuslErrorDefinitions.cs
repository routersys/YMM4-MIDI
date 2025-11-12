using System.Collections.Generic;

namespace MIDI.Voice.SUSL.Errors
{
    public static class SuslErrorDefinitions
    {
        public static readonly (string Code, string Message) E0001_InvalidHeader =
            ("SUSL-E0001", "Invalid or missing #!SUSL header.");
        public static readonly (string Code, string Message) E0002_InvalidSectionName =
            ("SUSL-E0002", "Invalid section name '{0}'.");
        public static readonly (string Code, string Message) E0003_UnexpectedToken =
            ("SUSL-E0003", "Unexpected token. Expected '{1}' but found '{0}'.");
        public static readonly (string Code, string Message) E0004_ExpectedSectionBody =
            ("SUSL-E0004", "Expected section body after '{0}'.");
        public static readonly (string Code, string Message) E0005_UnexpectedEOF =
            ("SUSL-E0005", "Unexpected end of file.");
        public static readonly (string Code, string Message) E0006_InvalidToken =
            ("SUSL-E0006", "Invalid token '{0}'.");
        public static readonly (string Code, string Message) E0007_InvalidAssignment =
            ("SUSL-E0007", "Invalid assignment. Expected '=' after variable.");
        public static readonly (string Code, string Message) E0008_InvalidValue =
            ("SUSL-E0008", "Invalid value for '{0}'. Expected {1}.");
        public static readonly (string Code, string Message) E0009_InvalidTimeSignature =
            ("SUSL-E0009", "Invalid time signature format. Expected 'num/den' (e.g., '4/4').");
        public static readonly (string Code, string Message) E0010_InvalidNumber =
            ("SUSL-E0010", "Invalid number format: '{0}'.");
        public static readonly (string Code, string Message) E0011_InvalidAssignmentValue =
            ("SUSL-E0011", "Invalid assignment value '{0}' for '{1}'. Error: {2}");
        public static readonly (string Code, string Message) E0012_ExpectedAt =
            ("SUSL-E0012", "Expected '@' for position definition.");
        public static readonly (string Code, string Message) E0013_InvalidPositionFormat =
            ("SUSL-E0013", "Invalid position format. Expected '@(measure, beat, tick)'.");
        public static readonly (string Code, string Message) E0014_InvalidCommand =
            ("SUSL-E0014", "Invalid command '{0}' in sequence.");
        public static readonly (string Code, string Message) E0015_InvalidLength =
            ("SUSL-E0015", "Invalid length definition.");
        public static readonly (string Code, string Message) E0016_InvalidPitch =
            ("SUSL-E0016", "Invalid pitch definition.");
        public static readonly (string Code, string Message) E0017_InvalidLyricPhoneme =
            ("SUSL-E0017", "Invalid lyric/phoneme definition.");
        public static readonly (string Code, string Message) E0018_InvalidParameterBlock =
            ("SUSL-E0018", "Invalid parameter block.");
        public static readonly (string Code, string Message) E0019_WrongLanguageHeader =
            ("SUSL-E0019", "Wrong language header '{0}'. Did you mean '#!SUSL'?");
        public static readonly (string Code, string Message) E0020_InvalidParameterExpression =
            ("SUSL-E0020", "Invalid parameter expression. Expected 'name=value'.");
        public static readonly (string Code, string Message) E0021_InvalidVibrato =
            ("SUSL-E0021", "Invalid vibrato definition.");
        public static readonly (string Code, string Message) E0022_InvalidPitchCurve =
            ("SUSL-E0022", "Invalid pitch curve definition.");
        public static readonly (string Code, string Message) E0023_InvalidPitchPoint =
            ("SUSL-E0023", "Invalid pitch point. Expected '(tick, cent, [shape])'.");

        public static string GetMessage((string Code, string Message) error, params object[] args)
        {
            try
            {
                return string.Format(error.Message, args);
            }
            catch
            {
                return error.Message;
            }
        }
    }
}
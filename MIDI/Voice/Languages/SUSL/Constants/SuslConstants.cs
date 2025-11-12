using System.Collections.Generic;

namespace MIDI.Voice.SUSL.Constants
{
    public static class SuslConstants
    {
        public const string Header = "#!SUSL";

        public static readonly HashSet<string> SectionKeywords = new HashSet<string>
        {
            "CONFIGURATION", "CONF",
            "DEFAULT", "DEF",
            "SEQUENCE", "SEQ"
        };

        public static readonly HashSet<string> EndSectionKeywords = new HashSet<string>
        {
            "END"
        };

        public static readonly HashSet<string> ConfigKeywords = new HashSet<string>
        {
            "TIMEBASE", "TEMPO", "BPM", "TIME_SIGNATURE", "TSIG"
        };

        public static readonly HashSet<string> DefaultKeywords = new HashSet<string>
        {
            "EXPRESSION", "DEFAULT_VIBRATO"
        };

        public static readonly HashSet<string> SequenceKeywords = new HashSet<string>
        {
            "NOTE", "N", "REST", "R", "TEMPO_CHANGE", "TCHG", "TIME_SIGNATURE_CHANGE", "TSIGCHG"
        };

        public static readonly HashSet<string> LyricKeywords = new HashSet<string>
        {
            "LYRIC", "PHONEME"
        };

        public static readonly HashSet<string> PropertyKeywords = new HashSet<string>
        {
            "AT", "LENGTH", "LEN", "PITCH", "PIT"
        };

        public static readonly HashSet<string> LengthConstants = new HashSet<string>
        {
            "WHOLE_NOTE", "HALF_NOTE", "QUARTER_NOTE", "EIGHTH_NOTE", "SIXTEENTH_NOTE"
        };

        public const string Dot = "DOT";
        public const string Ticks = "TICKS";
        public const string RelativePitch = "REL";
        public const string Ppq = "PPQ";

        public static readonly HashSet<string> ParameterBlockKeywords = new HashSet<string>
        {
            "PARAMETERS", "VIBRATO", "PITCH_CURVE"
        };

        public static readonly HashSet<string> ExpressionPropertyKeywords = new HashSet<string>
        {
            "VOL", "PAN", "GEN", "BRE", "BRI", "CLE", "OPE"
        };

        public static readonly HashSet<string> VibratoPropertyKeywords = new HashSet<string>
        {
            "PERIOD", "DEPTH", "FADE_IN", "FADE_OUT"
        };

        public const string VibratoOn = "ON";

        public static readonly HashSet<string> PitchCurvePropertyKeywords = new HashSet<string>
        {
            "TYPE", "POINTS"
        };

        public static readonly HashSet<string> PitchCurveTypes = new HashSet<string>
        {
            "LINEAR", "BEZIER"
        };

        public static readonly HashSet<string> PitchCurvePointShapes = new HashSet<string>
        {
            "IN", "OUT", "IO"
        };
    }
}
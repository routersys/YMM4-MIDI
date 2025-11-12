using System.Collections.Generic;

namespace MIDI.Shape.MidiPianoRoll.Models
{
    public class ColorDefinition
    {
        public string HexColor { get; set; } = "#FFFFFF";
        public double Opacity { get; set; } = 1.0;
    }

    public class PianoRollResourcePack
    {
        public ColorDefinition WhiteKeyColor { get; set; } = new ColorDefinition { HexColor = "#FFFFFF", Opacity = 1.0 };
        public ColorDefinition BlackKeyColor { get; set; } = new ColorDefinition { HexColor = "#000000", Opacity = 1.0 };
        public ColorDefinition WhiteKeyShadowColor { get; set; } = new ColorDefinition { HexColor = "#646464", Opacity = 0.5 };
        public ColorDefinition BlackKeyShadowColor { get; set; } = new ColorDefinition { HexColor = "#000000", Opacity = 0.5 };
        public ColorDefinition WhiteKeyHighlightColor { get; set; } = new ColorDefinition { HexColor = "#FFFFFF", Opacity = 0.25 };
        public ColorDefinition BlackKeyHighlightColor { get; set; } = new ColorDefinition { HexColor = "#C8C8C8", Opacity = 0.25 };
        public ColorDefinition NoteColor { get; set; } = new ColorDefinition { HexColor = "#1E90FF", Opacity = 1.0 };
        public ColorDefinition SelectedNoteColor { get; set; } = new ColorDefinition { HexColor = "#FFA500", Opacity = 1.0 };
        public ColorDefinition PlayingKeyColor { get; set; } = new ColorDefinition { HexColor = "#90EE90", Opacity = 1.0 };
        public ColorDefinition CursorColor { get; set; } = new ColorDefinition { HexColor = "#FF0000", Opacity = 1.0 };
        public ColorDefinition LineColor { get; set; } = new ColorDefinition { HexColor = "#D3D3D3", Opacity = 1.0 };
        public ColorDefinition KeySeparatorColor { get; set; } = new ColorDefinition { HexColor = "#696969", Opacity = 1.0 };
        public ColorDefinition NoteHitGlowColor { get; set; } = new ColorDefinition { HexColor = "#FFFFE0", Opacity = 0.78 };
        public ColorDefinition NoteSplashGlowColor { get; set; } = new ColorDefinition { HexColor = "#FFFFFF", Opacity = 0.7 };
        public ColorDefinition PressedKeyDarkEdgeColor { get; set; } = new ColorDefinition { HexColor = "#000000", Opacity = 0.3 };

        public ColorDefinition WhiteKeyGradientStop1 { get; set; } = new ColorDefinition { HexColor = "#FAFAFA", Opacity = 1.0 };
        public ColorDefinition WhiteKeyGradientStop2 { get; set; } = new ColorDefinition { HexColor = "#DCDCDC", Opacity = 1.0 };
        public ColorDefinition BlackKeyGradientStop1 { get; set; } = new ColorDefinition { HexColor = "#3C3C3C", Opacity = 1.0 };
        public ColorDefinition BlackKeyGradientStop2 { get; set; } = new ColorDefinition { HexColor = "#1E1E1E", Opacity = 1.0 };

        public Dictionary<int, ColorDefinition> ChannelColors { get; set; } = new Dictionary<int, ColorDefinition>
        {
            { 1, new ColorDefinition { HexColor = "#1E90FF", Opacity = 1.0 } },
            { 2, new ColorDefinition { HexColor = "#00BFFF", Opacity = 1.0 } },
            { 3, new ColorDefinition { HexColor = "#87CEFA", Opacity = 1.0 } },
            { 4, new ColorDefinition { HexColor = "#4682B4", Opacity = 1.0 } },
            { 5, new ColorDefinition { HexColor = "#6495ED", Opacity = 1.0 } },
            { 6, new ColorDefinition { HexColor = "#0000FF", Opacity = 1.0 } },
            { 7, new ColorDefinition { HexColor = "#4169E1", Opacity = 1.0 } },
            { 8, new ColorDefinition { HexColor = "#191970", Opacity = 1.0 } },
            { 9, new ColorDefinition { HexColor = "#0000CD", Opacity = 1.0 } },
            { 10, new ColorDefinition { HexColor = "#00008B", Opacity = 1.0 } },
            { 11, new ColorDefinition { HexColor = "#5F9EA0", Opacity = 1.0 } },
            { 12, new ColorDefinition { HexColor = "#B0E0E6", Opacity = 1.0 } },
            { 13, new ColorDefinition { HexColor = "#ADD8E6", Opacity = 1.0 } },
            { 14, new ColorDefinition { HexColor = "#483D8B", Opacity = 1.0 } },
            { 15, new ColorDefinition { HexColor = "#6A5ACD", Opacity = 1.0 } },
            { 16, new ColorDefinition { HexColor = "#7B68EE", Opacity = 1.0 } }
        };

        public bool UseChannelColors { get; set; } = true;
    }
}
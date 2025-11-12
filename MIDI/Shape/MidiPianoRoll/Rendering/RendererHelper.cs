using Vortice.Mathematics;
using MIDI.Shape.MidiPianoRoll.Models;
using System.Globalization;

namespace MIDI.Shape.MidiPianoRoll.Rendering
{
    internal static class RendererHelper
    {
        public static Color4 ToVorticeColor(ColorDefinition colorDef)
        {
            return ToVorticeColor(colorDef.HexColor, (float)colorDef.Opacity);
        }

        public static Color4 ToVorticeColor(string hexColor, float opacity)
        {
            var hex = hexColor.TrimStart('#');
            if (hex.Length != 6)
            {
                return new Color4(1.0f, 0.0f, 1.0f, opacity);
            }

            try
            {
                float r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255.0f;
                float g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255.0f;
                float b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255.0f;
                return new Color4(r, g, b, opacity);
            }
            catch
            {
                return new Color4(1.0f, 0.0f, 1.0f, opacity);
            }
        }
    }
}
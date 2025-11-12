using System;
using System.Globalization;

namespace MIDI.Voice.Engine.Worldline
{
    public class UtauOto
    {
        public string File { get; set; } = "";
        public string Alias { get; set; } = "";
        public double Offset { get; set; } = 0;
        public double Consonant { get; set; } = 0;
        public double Cutoff { get; set; } = 0;
        public double Preutter { get; set; } = 0;
        public double Overlap { get; set; } = 0;
        public string? OriginalAlias { get; private set; }

        public UtauOto(string line, string wavPath)
        {
            File = wavPath;
            var parts = line.Split('=');
            if (parts.Length < 2) throw new ArgumentException("Invalid oto line: " + line);

            OriginalAlias = parts[0];
            var values = parts[1].Split(',');

            Alias = values.Length > 0 ? values[0] : "";
            if (string.IsNullOrEmpty(Alias)) Alias = OriginalAlias;

            Offset = values.Length > 1 ? double.Parse(values[1], CultureInfo.InvariantCulture) : 0;
            Consonant = values.Length > 2 ? double.Parse(values[2], CultureInfo.InvariantCulture) : 0;
            Cutoff = values.Length > 3 ? double.Parse(values[3], CultureInfo.InvariantCulture) : 0;
            Preutter = values.Length > 4 ? double.Parse(values[4], CultureInfo.InvariantCulture) : 0;
            Overlap = values.Length > 5 ? double.Parse(values[5], CultureInfo.InvariantCulture) : 0;
        }
    }
}
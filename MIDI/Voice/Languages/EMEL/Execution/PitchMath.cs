using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace MIDI.Voice.EMEL.Execution
{
    public static class PitchMath
    {
        private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        private static readonly Regex PitchRegex = new Regex(@"^([A-Ga-g])([#b]?)(-?\d+)$");

        public static int PitchToMidi(string pitchName)
        {
            if (string.IsNullOrEmpty(pitchName)) throw new ArgumentException("Pitch name cannot be empty.");

            var match = PitchRegex.Match(pitchName);
            if (!match.Success)
            {
                if (int.TryParse(pitchName, NumberStyles.Any, CultureInfo.InvariantCulture, out int midiNote))
                {
                    return midiNote;
                }
                throw new ArgumentException($"Invalid pitch format: {pitchName}");
            }

            string note = match.Groups[1].Value.ToUpper();
            string accidental = match.Groups[2].Value;
            int octave = int.Parse(match.Groups[3].Value);

            int noteIndex = Array.IndexOf(NoteNames, note);
            if (noteIndex == -1)
            {
                throw new ArgumentException($"Invalid note name: {note}");
            }

            if (accidental == "#") noteIndex++;
            else if (accidental == "b") noteIndex--;

            return (octave + 1) * 12 + noteIndex;
        }

        public static string MidiToPitch(int midiNote)
        {
            int octave = (midiNote / 12) - 1;
            int noteIndex = midiNote % 12;
            if (noteIndex < 0) noteIndex += 12;

            return NoteNames[noteIndex] + octave;
        }

        public static string Transpose(string pitchName, double semitones)
        {
            try
            {
                int midi = PitchToMidi(pitchName);
                int transposedMidi = midi + (int)Math.Round(semitones);
                return MidiToPitch(transposedMidi);
            }
            catch (Exception)
            {
                throw new Exception($"Cannot transpose non-pitch string: {pitchName}");
            }
        }

        public static List<string> ResolveChord(string chordName, int defaultOctave = 4)
        {
            string baseNoteName;
            string chordType;
            int baseOctave = defaultOctave;

            var baseNoteMatch = Regex.Match(chordName, @"^([A-Ga-g][#b]?)(-?\d+)(.*)$");

            if (baseNoteMatch.Success)
            {
                baseNoteName = baseNoteMatch.Groups[1].Value;
                baseOctave = int.Parse(baseNoteMatch.Groups[2].Value);
                chordType = baseNoteMatch.Groups[3].Value;
            }
            else
            {
                int splitIndex = 1;
                if (chordName.Length > 1 && (chordName[1] == '#' || chordName[1] == 'b'))
                {
                    splitIndex = 2;
                }
                baseNoteName = chordName.Substring(0, splitIndex);
                chordType = chordName.Substring(splitIndex);
            }


            if (chordType.StartsWith("-"))
            {
                chordType = chordType.Substring(1);
            }

            int baseMidi;
            try
            {
                baseMidi = PitchToMidi(baseNoteName + baseOctave);
            }
            catch (Exception)
            {
                throw new Exception($"Invalid chord base note: {baseNoteName}");
            }

            var intervals = new List<int> { 0 };

            switch (chordType.ToLower())
            {
                case "maj":
                case "":
                    intervals.AddRange(new[] { 4, 7 });
                    break;
                case "m":
                case "min":
                    intervals.AddRange(new[] { 3, 7 });
                    break;
                case "7":
                    intervals.AddRange(new[] { 4, 7, 10 });
                    break;
                case "maj7":
                    intervals.AddRange(new[] { 4, 7, 11 });
                    break;
                case "m7":
                case "min7":
                    intervals.AddRange(new[] { 3, 7, 10 });
                    break;
                case "dim":
                    intervals.AddRange(new[] { 3, 6 });
                    break;
                case "aug":
                    intervals.AddRange(new[] { 4, 8 });
                    break;
                case "sus4":
                    intervals.AddRange(new[] { 5, 7 });
                    break;
                case "sus2":
                    intervals.AddRange(new[] { 2, 7 });
                    break;
                default:
                    throw new Exception($"Unknown chord type: {chordType}");
            }

            return intervals.Select(i => MidiToPitch(baseMidi + i)).ToList();
        }
    }
}
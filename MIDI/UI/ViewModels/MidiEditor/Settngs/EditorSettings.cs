using MIDI.Configuration.Models;
using System.ComponentModel;
using System.Windows.Media;

namespace MIDI.UI.ViewModels.MidiEditor.Settings
{
    public static class EditorSettings
    {
        public class View
        {
            public static bool EnableGpuNoteRendering { get; set; } = true;

            public static bool LightUpKeyboardDuringPlayback { get; set; } = true;

            public static bool ShowThumbnail { get; set; } = true;

            public static AdditionalKeyLabelType AdditionalKeyLabel { get; set; } = AdditionalKeyLabelType.DoReMi;
        }

        public class Note
        {
            public static Color NoteColor { get; set; } = Color.FromRgb(74, 144, 226);

            public static NoteOverlapBehavior NoteOverlapBehavior { get; set; } = NoteOverlapBehavior.Keep;
        }

        public class Grid
        {
            public static string GridQuantizeValue { get; set; } = "1/16";

            public static int TimeRulerInterval { get; set; } = 5;

            public static bool EnableSnapToGrid { get; set; } = false;
        }

        public class Input
        {
            public static MidiInputMode MidiInputMode { get; set; } = MidiInputMode.Keyboard;
        }

        public class Metronome
        {
            public static bool MetronomeEnabled { get; set; } = false;

            public static double MetronomeVolume { get; set; } = 0.5;
        }

        public class Backup
        {
            public static bool EnableAutoBackup { get; set; } = true;

            public static int BackupIntervalMinutes { get; set; } = 5;

            public static int MaxBackupFiles { get; set; } = 20;
        }
    }
}
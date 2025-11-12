using MIDI.Configuration.Models;
using System.ComponentModel;
using System.Windows.Media;

namespace MIDI.UI.ViewModels.MidiEditor.Settings
{
    [MajorSettingGroup("エディター設定")]
    public static class EditorSettings
    {
        [SettingGroup("表示")]
        public class View
        {
            [Setting("GPUノートレンダリングを有効にする", Description = "GPUを使用してピアノロールのノート描画を高速化します。")]
            public static bool EnableGpuNoteRendering { get; set; } = true;

            [Setting("再生中に鍵盤を光らせる", Description = "再生中に対応する鍵盤をハイライトします。")]
            public static bool LightUpKeyboardDuringPlayback { get; set; } = true;

            [Setting("サムネイル表示", Description = "ピアノロール下部に波形サムネイルを表示します。")]
            public static bool ShowThumbnail { get; set; } = true;

            [Setting("追加の鍵盤ラベル", Description = "ピアノキーにドレミなどの追加ラベルを表示します。")]
            public static AdditionalKeyLabelType AdditionalKeyLabel { get; set; } = AdditionalKeyLabelType.DoReMi;
        }

        [SettingGroup("ノート")]
        public class Note
        {
            [Setting("ノートの色", Description = "ピアノロールに表示されるノートのデフォルトの色。")]
            public static Color NoteColor { get; set; } = Color.FromRgb(74, 144, 226);

            [Setting("ノートの重複時の動作", Description = "ノートが重なった場合にどのように処理するかを設定します。")]
            public static NoteOverlapBehavior NoteOverlapBehavior { get; set; } = NoteOverlapBehavior.Keep;
        }

        [SettingGroup("グリッド")]
        public class Grid
        {
            [Setting("クオンタイズ値", Description = "グリッドの分割単位を設定します。")]
            public static string GridQuantizeValue { get; set; } = "1/16";

            [Setting("タイムルーラーの間隔(秒)", Description = "タイムルーラーの目盛りの間隔を秒単位で設定します。")]
            public static int TimeRulerInterval { get; set; } = 5;

            [Setting("グリッドにスナップ", Description = "ノートの移動や追加時にグリッドにスナップします。Shiftキーで一時的に無効化できます。")]
            public static bool EnableSnapToGrid { get; set; } = false;
        }

        [SettingGroup("入力")]
        public class Input
        {
            [Setting("MIDI入力モード", Description = "MIDIキーボードやPCキーボードからの入力方法を選択します。")]
            public static MidiInputMode MidiInputMode { get; set; } = MidiInputMode.Keyboard;
        }

        [SettingGroup("メトロノーム")]
        public class Metronome
        {
            [Setting("メトロノームを有効にする")]
            public static bool MetronomeEnabled { get; set; } = false;

            [Setting("メトロノームの音量")]
            public static double MetronomeVolume { get; set; } = 0.5;
        }

        [SettingGroup("バックアップ")]
        public class Backup
        {
            [Setting("自動バックアップを有効にする", Description = "プロジェクトを定期的に自動でバックアップします。")]
            public static bool EnableAutoBackup { get; set; } = true;

            [Setting("バックアップ間隔 (分)", Description = "自動バックアップを実行する間隔を分単位で設定します。")]
            public static int BackupIntervalMinutes { get; set; } = 5;

            [Setting("最大バックアップファイル数", Description = "保存するバックアップファイルの最大数です。これを超えると古いものから削除されます。")]
            public static int MaxBackupFiles { get; set; } = 20;
        }
    }
}
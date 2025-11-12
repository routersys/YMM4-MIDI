using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using MIDI.UI.ViewModels;
using MIDI.UI.ViewModels.MidiEditor.Modals;
using MIDI.UI.ViewModels.MidiEditor.Settings;
using MIDI.Utils;
using YukkuriMovieMaker.Plugin;

namespace MIDI.Configuration.Models
{
    public enum AdditionalKeyLabelType
    {
        [Description("無効")]
        None,
        [Description("ドレミ")]
        DoReMi,
        [Description("イロハ")]
        Iroha
    }

    public enum TuningSystemType
    {
        [Description("12平均律")]
        TwelveToneEqualTemperament,
        [Description("24平均律")]
        TwentyFourToneEqualTemperament,
        [Description("純正律")]
        JustIntonation,
        [Description("微分音")]
        Microtonal
    }

    public enum NoteOverlapBehavior
    {
        [Description("元の音を維持")]
        Keep,
        [Description("上書き")]
        Overwrite,
        [Description("削除")]
        Delete
    }

    [MajorSettingGroup("エディター設定")]
    public class MidiEditorSettings : SettingsBase<MidiEditorSettings>
    {
        public override string Name => "MIDIエディター設定";
        public override SettingsCategory Category => SettingsCategory.None;
        public override bool HasSettingView => false;

        [JsonIgnore]
        public override object? SettingView => null;

        private static readonly string ConfigFileName = "MidiEditorConfig.json";
        private static string PluginDir => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
        private static string OldConfigPath => Path.Combine(PluginDir, ConfigFileName);

        private static string ConfigDir
        {
            get
            {
                return Path.Combine(PluginDir, "Config");
            }
        }
        private static string ConfigPath => Path.Combine(ConfigDir, ConfigFileName);

        [JsonIgnore]
        private static readonly Timer _saveTimer = new Timer(_ => _ = SaveAsync());

        private View _view = new();
        public View View { get => _view; set => Set(ref _view, value); }

        private Note _note = new();
        public Note Note { get => _note; set => Set(ref _note, value); }

        private Flag _flag = new();
        public Flag Flag { get => _flag; set => Set(ref _flag, value); }

        private Grid _grid = new();
        public Grid Grid { get => _grid; set => Set(ref _grid, value); }

        private Input _input = new();
        public Input Input { get => _input; set => Set(ref _input, value); }

        private Metronome _metronome = new();
        public Metronome Metronome { get => _metronome; set => Set(ref _metronome, value); }

        private Backup _backup = new();
        public Backup Backup { get => _backup; set => Set(ref _backup, value); }

        private string? _layoutXml;
        public string? LayoutXml { get => _layoutXml; set => Set(ref _layoutXml, value); }

        private TuningSystemType _tuningSystem = TuningSystemType.TwelveToneEqualTemperament;
        public TuningSystemType TuningSystem { get => _tuningSystem; set => Set(ref _tuningSystem, value); }

        private ObservableCollection<KeyboardMapping> _keyboardMappings = new();
        public ObservableCollection<KeyboardMapping> KeyboardMappings { get => _keyboardMappings; set => Set(ref _keyboardMappings, value); }


        public override void Initialize()
        {
            Load();
            AttachEventHandlers();
        }

        private void AttachEventHandlers()
        {
            View.PropertyChanged += OnNestedPropertyChanged;
            Note.PropertyChanged += OnNestedPropertyChanged;
            Flag.PropertyChanged += OnNestedPropertyChanged;
            Grid.PropertyChanged += OnNestedPropertyChanged;
            Input.PropertyChanged += OnNestedPropertyChanged;
            Metronome.PropertyChanged += OnNestedPropertyChanged;
            Backup.PropertyChanged += OnNestedPropertyChanged;
        }

        private void OnNestedPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            Save();
        }

        private void MigrateSettingsFile()
        {
            if (File.Exists(OldConfigPath))
            {
                try
                {
                    if (!Directory.Exists(ConfigDir))
                    {
                        Directory.CreateDirectory(ConfigDir);
                    }

                    if (!File.Exists(ConfigPath))
                    {
                        File.Move(OldConfigPath, ConfigPath);
                        Logger.Info($"Moved editor configuration file from '{OldConfigPath}' to '{ConfigPath}'.");
                    }
                    else
                    {
                        File.Delete(OldConfigPath);
                        Logger.Info($"Deleted old editor configuration file at '{OldConfigPath}' as new one already exists.");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to migrate editor configuration file from '{OldConfigPath}' to '{ConfigPath}'.", ex);
                }
            }
        }

        public void Load()
        {
            MigrateSettingsFile();

            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = new MidiEditorSettings();
                defaultConfig.SaveSynchronously();
                CopyFrom(defaultConfig);
            }
            else
            {
                try
                {
                    var jsonString = File.ReadAllText(ConfigPath);
                    var options = new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                        WriteIndented = true,
                        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        AllowTrailingCommas = true,
                        PropertyNameCaseInsensitive = true
                    };
                    var loadedConfig = JsonSerializer.Deserialize<MidiEditorSettings>(jsonString, options);
                    if (loadedConfig != null)
                    {
                        CopyFrom(loadedConfig);
                    }
                    else
                    {
                        throw new JsonException("エディター設定のデシリアライズに失敗しました。");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error("エディター設定ファイルの読み込み中にエラーが発生しました。", ex, 1, ex.Message);
                    BackupAndLoadDefault();
                }
            }
        }

        private void BackupAndLoadDefault()
        {
            try
            {
                string backupDir = Path.Combine(PluginDir, "backup");
                if (!Directory.Exists(backupDir)) Directory.CreateDirectory(backupDir);
                if (File.Exists(ConfigPath))
                {
                    var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HH.mm.ss");
                    var backupFileName = $"{Path.GetFileNameWithoutExtension(ConfigFileName)}-{timestamp}.bak";
                    var backupPath = Path.Combine(backupDir, backupFileName);
                    File.Move(ConfigPath, backupPath);
                }
            }
            catch { }

            var defaultConfig = new MidiEditorSettings();
            CopyFrom(defaultConfig);
            SaveSynchronously();
        }

        public new void Save()
        {
            _saveTimer.Change(500, Timeout.Infinite);
        }

        private static async Task SaveAsync()
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var jsonString = JsonSerializer.Serialize(Default, options);
                await File.WriteAllTextAsync(ConfigPath, jsonString);
            }
            catch (Exception ex)
            {
                Logger.Error("エディター設定ファイル保存エラー", ex, 1, ex.Message);
            }
        }

        public void SaveSynchronously()
        {
            _saveTimer.Change(Timeout.Infinite, Timeout.Infinite);
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
                };
                var jsonString = JsonSerializer.Serialize(this, options);
                File.WriteAllText(ConfigPath, jsonString);
            }
            catch (Exception ex)
            {
                Logger.Error("エディター設定ファイル保存エラー", ex, 1, ex.Message);
            }
        }

        public static void SaveEditorSettingsSeparately(string jsonString)
        {
            try
            {
                if (!Directory.Exists(ConfigDir))
                {
                    Directory.CreateDirectory(ConfigDir);
                }
                File.WriteAllText(ConfigPath, jsonString);
            }
            catch (Exception ex)
            {
                Logger.Error("分離されたエディター設定ファイルの保存中にエラーが発生しました。", ex, 1, ex.Message);
            }
        }

        public void Reload()
        {
            Load();
            OnPropertyChanged(string.Empty);
        }


        public MidiEditorSettings Clone()
        {
            var clone = new MidiEditorSettings();
            clone.CopyFrom(this);
            return clone;
        }

        public void CopyFrom(MidiEditorSettings source)
        {
            LayoutXml = source.LayoutXml;
            TuningSystem = source.TuningSystem;
            KeyboardMappings = new ObservableCollection<KeyboardMapping>(source.KeyboardMappings.Select(m => (KeyboardMapping)m.Clone()));

            View.CopyFrom(source.View);
            Note.CopyFrom(source.Note);
            Flag.CopyFrom(source.Flag);
            Grid.CopyFrom(source.Grid);
            Input.CopyFrom(source.Input);
            Metronome.CopyFrom(source.Metronome);
            Backup.CopyFrom(source.Backup);
        }

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName!);
            Save();
            return true;
        }
    }

    [SettingGroup("表示")]
    public class View : INotifyPropertyChanged
    {
        private bool _enableGpuNoteRendering = true;
        [Setting("GPUノートレンダリングを有効にする", Description = "GPUを使用してピアノロールのノート描画を高速化します。")]
        public bool EnableGpuNoteRendering { get => _enableGpuNoteRendering; set => SetField(ref _enableGpuNoteRendering, value); }

        private bool _lightUpKeyboardDuringPlayback = true;
        [Setting("再生中に鍵盤を光らせる", Description = "再生中に対応する鍵盤をハイライトします。")]
        public bool LightUpKeyboardDuringPlayback { get => _lightUpKeyboardDuringPlayback; set => SetField(ref _lightUpKeyboardDuringPlayback, value); }

        private bool _showThumbnail = true;
        [Setting("サムネイル表示", Description = "ピアノロール下部に波形サムネイルを表示します。")]
        public bool ShowThumbnail { get => _showThumbnail; set => SetField(ref _showThumbnail, value); }

        private AdditionalKeyLabelType _additionalKeyLabel = AdditionalKeyLabelType.DoReMi;
        [Setting("追加の鍵盤ラベル", Description = "ピアノキーにドレミなどの追加ラベルを表示します。")]
        public AdditionalKeyLabelType AdditionalKeyLabel { get => _additionalKeyLabel; set => SetField(ref _additionalKeyLabel, value); }

        public void CopyFrom(View source)
        {
            EnableGpuNoteRendering = source.EnableGpuNoteRendering;
            LightUpKeyboardDuringPlayback = source.LightUpKeyboardDuringPlayback;
            ShowThumbnail = source.ShowThumbnail;
            AdditionalKeyLabel = source.AdditionalKeyLabel;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
            return true;
        }
    }

    [SettingGroup("ノート")]
    public class Note : INotifyPropertyChanged
    {
        private Color _noteColor = Color.FromRgb(74, 144, 226);
        [Setting("ノートの色", Description = "ピアノロールに表示されるノートのデフォルトの色。")]
        public Color NoteColor { get => _noteColor; set => SetField(ref _noteColor, value); }

        private Color _selectedNoteColor = Colors.Orange;
        [Setting("選択されたノートの色", Description = "選択されたノートの色。")]
        public Color SelectedNoteColor { get => _selectedNoteColor; set => SetField(ref _selectedNoteColor, value); }

        private ObservableCollection<Color> _channelColors = new ObservableCollection<Color>
        {
            Color.FromRgb(30, 144, 255), Color.FromRgb(0, 191, 255), Color.FromRgb(135, 206, 250), Color.FromRgb(70, 130, 180),
            Color.FromRgb(100, 149, 237), Color.FromRgb(0, 0, 255), Color.FromRgb(65, 105, 225), Color.FromRgb(25, 25, 112),
            Color.FromRgb(0, 0, 205), Color.FromRgb(0, 0, 139), Color.FromRgb(95, 158, 160), Color.FromRgb(176, 224, 230),
            Color.FromRgb(173, 216, 230), Color.FromRgb(72, 61, 139), Color.FromRgb(106, 90, 205), Color.FromRgb(123, 104, 238)
        };
        [Setting("チャンネルごとの色", Description = "チャンネルごとに自動で色分けする際の色。")]
        public ObservableCollection<Color> ChannelColors { get => _channelColors; set => SetField(ref _channelColors, value); }

        private NoteOverlapBehavior _noteOverlapBehavior = NoteOverlapBehavior.Keep;
        [Setting("ノートの重複時の動作", Description = "ノートが重なった場合にどのように処理するかを設定します。")]
        public NoteOverlapBehavior NoteOverlapBehavior { get => _noteOverlapBehavior; set => SetField(ref _noteOverlapBehavior, value); }


        public void CopyFrom(Note source)
        {
            NoteColor = source.NoteColor;
            SelectedNoteColor = source.SelectedNoteColor;
            ChannelColors = new ObservableCollection<Color>(source.ChannelColors);
            NoteOverlapBehavior = source.NoteOverlapBehavior;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
            return true;
        }
    }

    [SettingGroup("フラグ")]
    public class Flag : INotifyPropertyChanged
    {
        private Color _flagColor = Colors.Blue;
        [Setting("フラグの色", Description = "フラグのデフォルトの色。")]
        public Color FlagColor { get => _flagColor; set => SetField(ref _flagColor, value); }

        private Color _selectedFlagColor = Colors.CornflowerBlue;
        [Setting("選択されたフラグの色", Description = "選択されたフラグの色。")]
        public Color SelectedFlagColor { get => _selectedFlagColor; set => SetField(ref _selectedFlagColor, value); }

        public void CopyFrom(Flag source)
        {
            FlagColor = source.FlagColor;
            SelectedFlagColor = source.SelectedFlagColor;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
            return true;
        }
    }

    [SettingGroup("グリッド")]
    public class Grid : INotifyPropertyChanged
    {
        private string _gridQuantizeValue = "1/16";
        [Setting("クオンタイズ値", Description = "グリッドの分割単位を設定します。")]
        public string GridQuantizeValue { get => _gridQuantizeValue; set => SetField(ref _gridQuantizeValue, value); }

        private int _timeRulerInterval = 5;
        [Setting("タイムルーラーの間隔(秒)", Description = "タイムルーラーの目盛りの間隔を秒単位で設定します。")]
        public int TimeRulerInterval { get => _timeRulerInterval; set => SetField(ref _timeRulerInterval, value); }

        private bool _enableSnapToGrid = false;
        [Setting("グリッドにスナップ", Description = "ノートの移動や追加時にグリッドにスナップします。Shiftキーで一時的に無効化できます。")]
        public bool EnableSnapToGrid { get => _enableSnapToGrid; set => SetField(ref _enableSnapToGrid, value); }

        public void CopyFrom(Grid source)
        {
            GridQuantizeValue = source.GridQuantizeValue;
            TimeRulerInterval = source.TimeRulerInterval;
            EnableSnapToGrid = source.EnableSnapToGrid;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
            return true;
        }
    }

    [SettingGroup("入力")]
    public class Input : INotifyPropertyChanged
    {
        private MidiInputMode _midiInputMode = MidiInputMode.Keyboard;
        [Setting("MIDI入力モード", Description = "MIDIキーボードやPCキーボードからの入力方法を選択します。")]
        public MidiInputMode MidiInputMode { get => _midiInputMode; set => SetField(ref _midiInputMode, value); }

        public void CopyFrom(Input source)
        {
            MidiInputMode = source.MidiInputMode;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
            return true;
        }
    }

    [SettingGroup("メトロノーム")]
    public class Metronome : INotifyPropertyChanged
    {
        private bool _metronomeEnabled = false;
        [Setting("メトロノームを有効にする")]
        public bool MetronomeEnabled { get => _metronomeEnabled; set => SetField(ref _metronomeEnabled, value); }

        private double _metronomeVolume = 0.5;
        [Setting("メトロノームの音量")]
        public double MetronomeVolume { get => _metronomeVolume; set => SetField(ref _metronomeVolume, value); }

        public void CopyFrom(Metronome source)
        {
            MetronomeEnabled = source.MetronomeEnabled;
            MetronomeVolume = source.MetronomeVolume;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
            return true;
        }
    }

    [SettingGroup("バックアップ")]
    public class Backup : INotifyPropertyChanged
    {
        private bool _enableAutoBackup = true;
        [Setting("自動バックアップを有効にする", Description = "プロジェクトを定期的に自動でバックアップします。")]
        public bool EnableAutoBackup { get => _enableAutoBackup; set => SetField(ref _enableAutoBackup, value); }

        private int _backupIntervalMinutes = 5;
        [Setting("バックアップ間隔 (分)", Description = "自動バックアップを実行する間隔を分単位で設定します。")]
        public int BackupIntervalMinutes { get => _backupIntervalMinutes; set => SetField(ref _backupIntervalMinutes, value); }

        private int _maxBackupFiles = 20;
        [Setting("最大バックアップファイル数", Description = "保存するバックアップファイルの最大数です。これを超えると古いものから削除されます。")]
        public int MaxBackupFiles { get => _maxBackupFiles; set => SetField(ref _maxBackupFiles, value); }

        public void CopyFrom(Backup source)
        {
            EnableAutoBackup = source.EnableAutoBackup;
            BackupIntervalMinutes = source.BackupIntervalMinutes;
            MaxBackupFiles = source.MaxBackupFiles;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName ?? string.Empty));
            return true;
        }
    }
}
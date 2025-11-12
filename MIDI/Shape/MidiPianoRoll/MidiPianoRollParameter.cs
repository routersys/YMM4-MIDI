using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MIDI.Shape.MidiPianoRoll.Attributes;
using MIDI.Shape.MidiPianoRoll.Effects;
using MIDI.Shape.MidiPianoRoll.Effects.Default;
using MIDI.Shape.MidiPianoRoll.Models;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace MIDI.Shape.MidiPianoRoll
{
    public enum VerticalGridLineType
    {
        [Display(Name = "なし")]
        None,
        [Display(Name = "全音符")]
        Whole,
        [Display(Name = "2分音符")]
        Half,
        [Display(Name = "4分音符")]
        Quarter,
        [Display(Name = "8分音符")]
        Eighth,
        [Display(Name = "16分音符")]
        Sixteenth,
        [Display(Name = "32分音符")]
        ThirtySecond
    }

    public enum PianoRollOrientation
    {
        [Display(Name = "横")]
        Horizontal,
        [Display(Name = "縦")]
        Vertical
    }

    public enum KeyColorSyncMode
    {
        [Display(Name = "なし")]
        None,
        [Display(Name = "同期")]
        SyncChannel
    }

    public class MidiPianoRollParameter : ShapeParameterBase
    {
        private readonly IResourcePackService _resourcePackService;
        private PianoRollResourcePack _resourcePack = new();
        private readonly MidiDataManager _midiDataManager = new MidiDataManager();

        public PianoRollResourcePack ResourcePack => _resourcePack;
        public MidiDataManager MidiDataManager => _midiDataManager;
        public double MidiDurationSeconds => _midiDataManager.MidiDuration.TotalSeconds;

        public event EventHandler? MidiFileReloadRequested;

        [Display(GroupName = "ファイル", Name = "MIDIファイル", Description = "表示するMIDIファイルを選択します。")]
        [MidiFileEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public string MidiFilePath
        {
            get => midiFilePath;
            set
            {
                if (Set(ref midiFilePath, value))
                {
                    _midiDataManager.LoadMidiData(value);
                    OnPropertyChanged(nameof(MidiDurationSeconds));
                }
            }
        }
        private string midiFilePath = "";

        [Display(GroupName = "ファイル", Name = "リソースパック", Description = "鍵盤やノートの色を定義したリソースパック(JSON)を選択します。")]
        [ResourcePackEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public string ResourcePackPath { get => resourcePackPath; set { if (Set(ref resourcePackPath, value)) _ = LoadResourcePackAsync(); } }
        private string resourcePackPath = "";

        [Display(GroupName = "表示", Name = "向き", Description = "ピアノロール全体の向き")]
        [EnumComboBox]
        public PianoRollOrientation Orientation { get => orientation; set => Set(ref orientation, value); }
        private PianoRollOrientation orientation = PianoRollOrientation.Horizontal;

        [Display(GroupName = "表示", Name = "表示範囲(秒)", Description = "ピアノロールに表示する時間範囲")]
        [AnimationSlider("F1", "秒", 0.1, 10)]
        public Animation DisplayDuration { get; } = new Animation(5, 0.1, 60);

        [Display(GroupName = "表示", Name = "最低音", Description = "表示する最低音のノート番号")]
        [AnimationSlider("F0", "", 0, 127)]
        public Animation MinNote { get; } = new Animation(21, 0, 127);

        [Display(GroupName = "表示", Name = "最高音", Description = "表示する最高音のノート番号")]
        [AnimationSlider("F0", "", 0, 127)]
        public Animation MaxNote { get; } = new Animation(108, 0, 127);

        [Display(GroupName = "再生", Name = "タイムシフト", Description = "MIDIの再生開始時間をオフセットします。hh:mm:ss:fff形式。")]
        [TimeShiftEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public double TimeShift { get => timeShift; set => Set(ref timeShift, value); }
        private double timeShift = 0.0;

        [Display(GroupName = "再生", Name = "再生速度", Description = "MIDIの再生速度。")]
        [AnimationSlider("F2", "倍", 0.1, 4.0)]
        public Animation PlaybackSpeed { get; } = new Animation(1.0, 0.1, 4.0);

        [Display(GroupName = "奥行き", Name = "パース(Y軸回転)", Description = "ピアノロールをY軸中心に回転させ、奥行きを表現します。")]
        [AnimationSlider("F1", "度", -80, 80)]
        public Animation PerspectiveYRotation { get; } = new Animation(0, -80, 80);

        [Display(GroupName = "奥行き", Name = "チルト(X軸回転)", Description = "ピアノロールをX軸中心に回転させ、上下の奥行きを表現します。")]
        [AnimationSlider("F1", "度", -80, 80)]
        public Animation PerspectiveXRotation { get; } = new Animation(0, -80, 80);

        [Display(GroupName = "奥行き", Name = "Z軸回転", Description = "ピアノロールをZ軸中心に回転させます。")]
        [AnimationSlider("F1", "度", -180, 180)]
        public Animation PerspectiveZRotation { get; } = new Animation(0, -180, 180);

        [Display(GroupName = "奥行き", Name = "奥行きの深さ(視点Z)", Description = "パースペクティブの視点の深さ(Z座標)を調整します。大きいほど歪みが小さくなります。")]
        [AnimationSlider("F0", "px", 500, 10000)]
        public Animation PerspectiveDepth { get; } = new Animation(1000, 500, 10000);

        [Display(GroupName = "奥行き", Name = "描画バッファ倍率", Description = "回転時にノーツが途切れないよう、描画範囲を時間軸方向に拡大します。")]
        [AnimationSlider("F1", "倍", 1, 10)]
        public Animation RenderBufferScale { get; } = new Animation(2.0, 1.0, 10.0);

        [Display(GroupName = "鍵盤", Name = "鍵盤サイズ", Description = "鍵盤領域のサイズ(%)。横向きの場合は幅、縦向きの場合は高さになります。")]
        [AnimationSlider("F1", "%", 1, 50)]
        public Animation KeySize { get; } = new Animation(10, 1, 50);

        [Display(GroupName = "鍵盤", Name = "鍵盤左右反転", Description = "黒鍵盤の左右の位置を反転します（通常は右側）。")]
        [ToggleSlider]
        public bool InvertKeyboard { get => invertKeyboard; set => Set(ref invertKeyboard, value); }
        private bool invertKeyboard = false;

        [Display(GroupName = "鍵盤", Name = "鍵盤上下反転", Description = "鍵盤の高低の表示順序を反転します（通常は上が高音）。")]
        [ToggleSlider]
        public bool InvertVertical { get => invertVertical; set => Set(ref invertVertical, value); }
        private bool invertVertical = false;

        [Display(GroupName = "鍵盤", Name = "ハイライト表示", Description = "再生中の鍵盤を明るく表示します。")]
        [ToggleSlider]
        public bool EnableKeyHighlight { get => enableKeyHighlight; set => Set(ref enableKeyHighlight, value); }
        private bool enableKeyHighlight = true;

        [Display(GroupName = "鍵盤", Name = "同期", Description = "鍵盤の色を、再生中のノートのチャンネル色と同期させます。")]
        [EnumComboBox]
        public KeyColorSyncMode KeyColorSync { get => keyColorSync; set => Set(ref keyColorSync, value); }
        private KeyColorSyncMode keyColorSync = KeyColorSyncMode.None;

        [Display(GroupName = "グリッド", Name = "縦線グリッド", Description = "ピアノロールに表示する縦線の間隔を指定します。")]
        [EnumComboBox]
        public VerticalGridLineType VerticalGridLine { get => verticalGridLine; set => Set(ref verticalGridLine, value); }
        private VerticalGridLineType verticalGridLine = VerticalGridLineType.Quarter;

        [Display(GroupName = "グリッド", Name = "横線を表示", Description = "ピアノロールの背景に横線を表示するかどうか。")]
        [ToggleSlider]
        public bool ShowHorizontalLines { get => showHorizontalLines; set => Set(ref showHorizontalLines, value); }
        private bool showHorizontalLines = true;

        [Display(GroupName = "エフェクト", Name = "エフェクト")]
        [EffectSelector(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public ObservableCollection<EffectParameterBase> Effects { get; } = new();

        public MidiPianoRollParameter() : this(null, new ResourcePackService()) { }

        public MidiPianoRollParameter(SharedDataStore? sharedData, IResourcePackService resourcePackService) : base(sharedData)
        {
            _resourcePackService = resourcePackService;
            _ = LoadResourcePackAsync();
        }

        public async Task LoadResourcePackAsync()
        {
            if (string.IsNullOrEmpty(ResourcePackPath))
            {
                _resourcePack = new PianoRollResourcePack();
            }
            else
            {
                _resourcePack = await _resourcePackService.LoadAsync(ResourcePackPath);
            }
            OnPropertyChanged(nameof(ResourcePack));
        }

        public void ReloadMidiFile()
        {
            _midiDataManager.LoadMidiData(MidiFilePath, true);
            OnPropertyChanged(nameof(MidiDurationSeconds));
            MidiFileReloadRequested?.Invoke(this, EventArgs.Empty);
        }

        public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        {
            return new MidiPianoRollSource(devices, this);
        }

        public override IEnumerable<string> CreateMaskExoFilter(int keyFrameIndex, ExoOutputDescription desc, ShapeMaskExoOutputDescription shapeMaskParameters)
        {
            return [];
        }

        public override IEnumerable<string> CreateShapeItemExoFilter(int keyFrameIndex, ExoOutputDescription desc)
        {
            return [];
        }

        protected override IEnumerable<IAnimatable> GetAnimatables()
        {
            return new IAnimatable[] { DisplayDuration, MinNote, MaxNote, PlaybackSpeed, PerspectiveYRotation, PerspectiveXRotation, PerspectiveZRotation, PerspectiveDepth, RenderBufferScale, KeySize };
        }


        protected override void LoadSharedData(SharedDataStore store)
        {
            var shared = store.Load<SharedData>();
            shared?.Apply(this);
            _ = LoadResourcePackAsync();
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new SharedData(this));
        }

        private class SharedData
        {
            public string MidiFilePath { get; }
            public string ResourcePackPath { get; }
            public PianoRollOrientation Orientation { get; } = PianoRollOrientation.Horizontal;
            public Animation DisplayDuration { get; } = new Animation(5, 0.1, 60);
            public Animation MinNote { get; } = new Animation(21, 0, 127);
            public Animation MaxNote { get; } = new Animation(108, 0, 127);
            public double TimeShift { get; } = 0.0;
            public Animation PlaybackSpeed { get; } = new Animation(1.0, 0.1, 4.0);
            public Animation KeySize { get; } = new Animation(10, 1, 50);
            public Animation PerspectiveYRotation { get; } = new Animation(0, -80, 80);
            public Animation PerspectiveXRotation { get; } = new Animation(0, -80, 80);
            public Animation PerspectiveZRotation { get; } = new Animation(0, -180, 180);
            public Animation PerspectiveDepth { get; } = new Animation(1000, 500, 10000);
            public Animation RenderBufferScale { get; } = new Animation(2.0, 1.0, 10.0);
            public bool InvertKeyboard { get; } = false;
            public bool InvertVertical { get; } = false;
            public bool EnableKeyHighlight { get; } = true;
            public KeyColorSyncMode KeyColorSync { get; } = KeyColorSyncMode.None;
            public VerticalGridLineType VerticalGridLine { get; } = VerticalGridLineType.Quarter;
            public bool ShowHorizontalLines { get; } = true;

            [JsonInclude]
            public List<EffectParameterBase.SharedDataBase> Effects { get; }

            public SharedData()
            {
                MidiFilePath = "";
                ResourcePackPath = "";
                Effects = new List<EffectParameterBase.SharedDataBase>();
            }

            public SharedData(MidiPianoRollParameter p)
            {
                MidiFilePath = p.MidiFilePath;
                ResourcePackPath = p.ResourcePackPath;
                Orientation = p.Orientation;
                DisplayDuration.CopyFrom(p.DisplayDuration);
                MinNote.CopyFrom(p.MinNote);
                MaxNote.CopyFrom(p.MaxNote);
                TimeShift = p.TimeShift;
                PlaybackSpeed.CopyFrom(p.PlaybackSpeed);
                KeySize.CopyFrom(p.KeySize);
                PerspectiveYRotation.CopyFrom(p.PerspectiveYRotation);
                PerspectiveXRotation.CopyFrom(p.PerspectiveXRotation);
                PerspectiveZRotation.CopyFrom(p.PerspectiveZRotation);
                PerspectiveDepth.CopyFrom(p.PerspectiveDepth);
                RenderBufferScale.CopyFrom(p.RenderBufferScale);
                InvertKeyboard = p.InvertKeyboard;
                InvertVertical = p.InvertVertical;
                EnableKeyHighlight = p.EnableKeyHighlight;
                KeyColorSync = p.KeyColorSync;
                VerticalGridLine = p.VerticalGridLine;
                ShowHorizontalLines = p.ShowHorizontalLines;
                Effects = p.Effects.Select(e => e.CreateSharedData()).ToList();
            }

            public void Apply(MidiPianoRollParameter p)
            {
                p.MidiFilePath = MidiFilePath;
                p.ResourcePackPath = ResourcePackPath;
                p.Orientation = Orientation;
                p.DisplayDuration.CopyFrom(DisplayDuration);
                p.MinNote.CopyFrom(MinNote);
                p.MaxNote.CopyFrom(MaxNote);
                p.TimeShift = TimeShift;
                p.PlaybackSpeed.CopyFrom(PlaybackSpeed);
                p.KeySize.CopyFrom(KeySize);
                p.PerspectiveYRotation.CopyFrom(PerspectiveYRotation);
                p.PerspectiveXRotation.CopyFrom(PerspectiveXRotation);
                p.PerspectiveZRotation.CopyFrom(PerspectiveZRotation);
                p.PerspectiveDepth.CopyFrom(PerspectiveDepth);
                p.RenderBufferScale.CopyFrom(RenderBufferScale);
                p.InvertKeyboard = InvertKeyboard;
                p.InvertVertical = InvertVertical;
                p.EnableKeyHighlight = EnableKeyHighlight;
                p.KeyColorSync = KeyColorSync;
                p.VerticalGridLine = VerticalGridLine;
                p.ShowHorizontalLines = ShowHorizontalLines;

                p.Effects.Clear();
                var plugins = EffectRegistry.GetPlugins();
                if (Effects != null)
                {
                    foreach (var effectData in Effects)
                    {
                        var plugin = plugins.FirstOrDefault(pl => pl.ParameterType == effectData.GetParameterType());
                        if (plugin != null)
                        {
                            var param = plugin.CreateParameter();
                            effectData.Apply(param);
                            p.Effects.Add(param);
                        }
                    }
                }
            }
        }
    }
}
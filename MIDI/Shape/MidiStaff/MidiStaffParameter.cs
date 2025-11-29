using MIDI.Shape.MidiPianoRoll.Attributes;
using MIDI.Shape.MidiStaff.Models;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Controls;
using YukkuriMovieMaker.Exo;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;
using YukkuriMovieMaker.Project;

namespace MIDI.Shape.MidiStaff
{
    public class MidiStaffParameter : ShapeParameterBase
    {
        private readonly MidiToScoreConverter _converter = new();

        [Display(GroupName = "ファイル", Name = "MIDIファイル", Description = "表示するMIDIファイルを選択します。")]
        [MidiFileEditor(PropertyEditorSize = PropertyEditorSize.FullWidth)]
        public string MidiFilePath
        {
            get => midiFilePath;
            set
            {
                if (Set(ref midiFilePath, value))
                {
                    OnPropertyChanged(nameof(ScoreData));
                }
            }
        }
        private string midiFilePath = "";

        [Display(GroupName = "表示", Name = "トラック", Description = "表示するMIDIトラック番号(1始まり)。0の場合はすべてのトラックをマージします。")]
        [TextBoxSlider("F0", "", 0, 16)]
        [Range(0, 16)]
        [DefaultValue(1d)]
        public double TrackIndex
        {
            get => trackIndex;
            set
            {
                if (Set(ref trackIndex, value))
                {
                    OnPropertyChanged(nameof(ScoreData));
                }
            }
        }
        private double trackIndex = 1;

        [Display(GroupName = "表示", Name = "大譜表モード", Description = "音程によってト音記号とヘ音記号に振り分けます。")]
        [ToggleSlider]
        public bool IsGrandStaff
        {
            get => isGrandStaff;
            set
            {
                if (Set(ref isGrandStaff, value))
                {
                    OnPropertyChanged(nameof(ScoreData));
                }
            }
        }
        private bool isGrandStaff = false;

        [Display(GroupName = "表示", Name = "分割ポイント", Description = "大譜表モード時に上下の譜表に分ける境目の音番号です（60=真ん中のド）。")]
        [TextBoxSlider("F0", "MIDI No", 0, 127)]
        [Range(0, 127)]
        [DefaultValue(60d)]
        public double SplitNoteNumber
        {
            get => splitNoteNumber;
            set
            {
                if (Set(ref splitNoteNumber, value))
                {
                    OnPropertyChanged(nameof(ScoreData));
                }
            }
        }
        private double splitNoteNumber = 60;

        [Display(GroupName = "色", Name = "基本色", Description = "楽譜の色")]
        [ColorPicker]
        public Color Color { get => color; set => Set(ref color, value); }
        Color color = Colors.White;

        [Display(GroupName = "色", Name = "ハイライト色", Description = "再生中の音符の色")]
        [ColorPicker]
        public Color HighlightColor { get => highlightColor; set => Set(ref highlightColor, value); }
        Color highlightColor = Colors.Cyan;

        [Display(GroupName = "レイアウト", Name = "スケール", Description = "楽譜の拡大率")]
        [AnimationSlider("F1", "倍", 0.5, 5.0)]
        public Animation Scale { get; } = new Animation(1.0, 0.5, 5.0);

        [Display(GroupName = "レイアウト", Name = "1行の小節数", Description = "1行に表示する小節の数")]
        [TextBoxSlider("F0", "小節", 1, 20)]
        [DefaultValue(4)]
        [Range(1, 20)]
        public int MeasuresPerLine
        {
            get => measuresPerLine;
            set
            {
                if (Set(ref measuresPerLine, value))
                {
                    OnPropertyChanged(nameof(ScoreData));
                }
            }
        }
        private int measuresPerLine = 4;

        [Display(GroupName = "レイアウト", Name = "行間隔", Description = "行と行の間の間隔（ピクセル）")]
        [AnimationSlider("F0", "px", 0, 500)]
        public Animation LineSpacing { get; } = new Animation(100, 0, 500);

        [Display(GroupName = "レイアウト", Name = "ページ幅", Description = "描画するページ幅（ピクセル）")]
        [AnimationSlider("F0", "px", 500, 5000)]
        public Animation PageWidth { get; } = new Animation(1920, 500, 5000);

        [Display(GroupName = "軽量化", Name = "最大表示小節数", Description = "一度に描画する最大小節数。数値を下げると軽量化されます。")]
        [TextBoxSlider("F0", "小節", 1, 100)]
        [DefaultValue(20)]
        [Range(1, 200)]
        public int MaxVisibleMeasures
        {
            get => maxVisibleMeasures;
            set => Set(ref maxVisibleMeasures, value);
        }
        private int maxVisibleMeasures = 20;

        [Newtonsoft.Json.JsonIgnore]
        public Manufaktura.Controls.Model.Score? ScoreData => _converter.Convert(MidiFilePath, (int)TrackIndex, MeasuresPerLine, IsGrandStaff, (int)SplitNoteNumber);

        public MidiStaffParameter() : this(null) { }

        public MidiStaffParameter(SharedDataStore? sharedData) : base(sharedData)
        {
        }

        public override IShapeSource CreateShapeSource(IGraphicsDevicesAndContext devices)
        {
            return new MidiStaffSource(devices, this);
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
            return [Scale, LineSpacing, PageWidth];
        }

        protected override void LoadSharedData(SharedDataStore store)
        {
            var shared = store.Load<SharedData>();
            shared?.Apply(this);
        }

        protected override void SaveSharedData(SharedDataStore store)
        {
            store.Save(new SharedData(this));
        }

        private class SharedData
        {
            public string MidiFilePath { get; }
            public double TrackIndex { get; } = 1;
            public bool IsGrandStaff { get; } = false;
            public double SplitNoteNumber { get; } = 60;
            public Color Color { get; } = Colors.White;
            public Color HighlightColor { get; } = Colors.Cyan;
            public Animation Scale { get; } = new Animation(1.0, 0.5, 5.0);
            public int MeasuresPerLine { get; } = 4;
            public Animation LineSpacing { get; } = new Animation(100, 0, 500);
            public Animation PageWidth { get; } = new Animation(1920, 500, 5000);
            public int MaxVisibleMeasures { get; } = 20;

            public SharedData()
            {
                MidiFilePath = "";
            }

            public SharedData(MidiStaffParameter p)
            {
                MidiFilePath = p.MidiFilePath;
                TrackIndex = p.TrackIndex;
                IsGrandStaff = p.IsGrandStaff;
                SplitNoteNumber = p.SplitNoteNumber;
                Color = p.Color;
                HighlightColor = p.HighlightColor;
                Scale.CopyFrom(p.Scale);
                MeasuresPerLine = p.MeasuresPerLine;
                LineSpacing.CopyFrom(p.LineSpacing);
                PageWidth.CopyFrom(p.PageWidth);
                MaxVisibleMeasures = p.MaxVisibleMeasures;
            }

            public void Apply(MidiStaffParameter p)
            {
                p.MidiFilePath = MidiFilePath;
                p.TrackIndex = TrackIndex;
                p.IsGrandStaff = IsGrandStaff;
                p.SplitNoteNumber = SplitNoteNumber;
                p.Color = Color;
                p.HighlightColor = HighlightColor;
                p.Scale.CopyFrom(Scale);
                p.MeasuresPerLine = MeasuresPerLine;
                p.LineSpacing.CopyFrom(LineSpacing);
                p.PageWidth.CopyFrom(PageWidth);
                p.MaxVisibleMeasures = MaxVisibleMeasures;
            }
        }
    }
}
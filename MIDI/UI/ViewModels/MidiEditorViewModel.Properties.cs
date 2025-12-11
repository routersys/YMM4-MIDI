using MIDI.Configuration.Models;
using MIDI.UI.ViewModels.MidiEditor;
using MIDI.UI.ViewModels.MidiEditor.Logic;
using MIDI.UI.ViewModels.MidiEditor.Rendering;
using NAudio.Midi;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;
using MIDI.UI.ViewModels.MidiEditor.Enums;

namespace MIDI.UI.ViewModels
{
    public partial class MidiEditorViewModel
    {
        public MidiEditorSettings EditorSettings => MidiEditorSettings.Default;
        public MidiFile? MidiFile { get; set; }
        public MidiFile? OriginalMidiFile { get; set; }

        private bool _isMidiFileLoaded = false;
        public bool IsMidiFileLoaded { get => _isMidiFileLoaded; set => SetField(ref _isMidiFileLoaded, value); }
        public bool IsNewFile { get; set; } = false;

        private string _filePath = "ファイルが選択されていません";
        public string FilePath
        {
            get => _filePath;
            set
            {
                if (SetField(ref _filePath, value)) OnPropertyChanged(nameof(FileName));
            }
        }

        private string _projectPath = string.Empty;
        public string ProjectPath
        {
            get => _projectPath;
            set
            {
                if (SetField(ref _projectPath, value)) OnPropertyChanged(nameof(FileName));
            }
        }

        public string FileName => string.IsNullOrEmpty(_filePath) || _filePath == "ファイルが選択されていません"
            ? "MIDIエディター"
            : (string.IsNullOrEmpty(_projectPath) ? System.IO.Path.GetFileName(_filePath) : $"~ {System.IO.Path.GetFileName(_projectPath)} - {System.IO.Path.GetFileName(_filePath)}");

        public ObservableCollection<NoteViewModel> AllNotes { get; } = new ObservableCollection<NoteViewModel>();
        public ObservableCollection<NoteViewModel> SelectedNotes { get; } = new ObservableCollection<NoteViewModel>();
        public ObservableCollection<NoteViewModel> EditingNotes { get; } = new ObservableCollection<NoteViewModel>();
        public ObservableCollection<FlagViewModel> Flags { get; } = new ObservableCollection<FlagViewModel>();
        public ObservableCollection<FlagViewModel> SelectedFlags { get; } = new ObservableCollection<FlagViewModel>();
        public ObservableCollection<TempoEventViewModel> TempoEvents { get; } = new ObservableCollection<TempoEventViewModel>();
        public ObservableCollection<ControlChangeEventViewModel> ControlChangeEvents { get; } = new ObservableCollection<ControlChangeEventViewModel>();
        public ObservableCollection<ProgramChangeEventViewModel> ProgramChangeEvents { get; } = new ObservableCollection<ProgramChangeEventViewModel>();
        public ObservableCollection<PianoKeyViewModel> PianoKeys { get; } = new ObservableCollection<PianoKeyViewModel>();
        public Dictionary<int, PianoKeyViewModel> PianoKeysMap { get; } = new Dictionary<int, PianoKeyViewModel>();
        public ObservableCollection<TimeRulerViewModel> TimeRuler { get; } = new ObservableCollection<TimeRulerViewModel>();
        public ObservableCollection<string> SoundFonts { get; } = new ObservableCollection<string>();
        public ObservableCollection<MidiInstrumentViewModel> AvailableInstruments { get; } = new ObservableCollection<MidiInstrumentViewModel>();

        private double _horizontalZoom = 100.0;
        public double HorizontalZoom
        {
            get => _horizontalZoom;
            set
            {
                if (SetField(ref _horizontalZoom, value))
                {
                    ViewManager.UpdatePianoRollSize();
                    OnPropertyChanged(nameof(PlaybackCursorPosition));
                    foreach (var flag in Flags) flag.OnPropertyChanged(nameof(FlagViewModel.X));
                    if (!IsSliderDragging) ViewManager.UpdateTimeRuler();
                    OnPropertyChanged(nameof(LoopStartX));
                    OnPropertyChanged(nameof(LoopDurationWidth));
                    OnPropertyChanged(nameof(IsZoomed));
                    PianoRollRenderer.RequestRedraw(true);
                }
            }
        }

        private double _verticalZoom = 1.0;
        public double VerticalZoom
        {
            get => _verticalZoom;
            set
            {
                if (SetField(ref _verticalZoom, value))
                {
                    ViewManager.UpdatePianoRollSize();
                    OnPropertyChanged(nameof(IsZoomed));
                    foreach (var key in PianoKeys) key.OnPropertyChanged(nameof(PianoKeyViewModel.Height));
                    PianoRollRenderer.RequestRedraw(true);
                }
            }
        }
        public bool IsZoomed => HorizontalZoom != 100.0 || VerticalZoom != 1.0;

        public System.TimeSpan CurrentTime
        {
            get => PlaybackService.CurrentTime;
            set => PlaybackService.CurrentTime = value;
        }
        public double PlaybackCursorPosition => PlaybackService.GetInterpolatedTime().TotalSeconds * HorizontalZoom;
        public bool IsPlaying => PlaybackService.IsPlaying;
        public bool IsLooping => PlaybackService.IsLooping;
        public string LoopRangeText => IsLooping ? $"Loop: {PlaybackService.LoopStart:mm\\:ss\\.fff} - {PlaybackService.LoopEnd:mm\\:ss\\.fff}" : "";
        public double LoopStartX => PlaybackService.LoopStart.TotalSeconds * HorizontalZoom;
        public double LoopDurationWidth => (PlaybackService.LoopEnd - PlaybackService.LoopStart).TotalSeconds * HorizontalZoom;

        public double MasterVolume
        {
            get => PlaybackService.MasterVolume;
            set
            {
                if (PlaybackService.MasterVolume != value)
                {
                    PlaybackService.MasterVolume = value;
                    OnPropertyChanged();
                }
            }
        }

        public string PlayButtonIcon => PlaybackService.IsPlaying ? "M18,18H6V6H18V18Z" : "M8,5.14V19.14L19,12.14L8,5.14Z";

        private string _statusText = "準備完了";
        public string StatusText { get => _statusText; set => SetField(ref _statusText, value); }

        private string _currentTempoText = "";
        public string CurrentTempoText { get => _currentTempoText; set => SetField(ref _currentTempoText, value); }

        private string _selectionStatusText = "";
        public string SelectionStatusText { get => _selectionStatusText; set => SetField(ref _selectionStatusText, value); }

        private bool _isNoteEditorVisible = true;
        public bool IsNoteEditorVisible { get => _isNoteEditorVisible; set => SetField(ref _isNoteEditorVisible, value); }

        private bool _isPianoRollVisible = true;
        public bool IsPianoRollVisible { get => _isPianoRollVisible; set => SetField(ref _isPianoRollVisible, value); }

        private bool _isQuantizeSettingsVisible = true;
        public bool IsQuantizeSettingsVisible { get => _isQuantizeSettingsVisible; set => SetField(ref _isQuantizeSettingsVisible, value); }

        private bool _isPlaybackSettingsVisible = true;
        public bool IsPlaybackSettingsVisible { get => _isPlaybackSettingsVisible; set => SetField(ref _isPlaybackSettingsVisible, value); }

        private bool _isMetronomeVisible = true;
        public bool IsMetronomeVisible { get => _isMetronomeVisible; set => SetField(ref _isMetronomeVisible, value); }

        private bool _isTempoEditorVisible = true;
        public bool IsTempoEditorVisible { get => _isTempoEditorVisible; set => SetField(ref _isTempoEditorVisible, value); }

        private bool _isControlChangeEditorVisible = true;
        public bool IsControlChangeEditorVisible { get => _isControlChangeEditorVisible; set => SetField(ref _isControlChangeEditorVisible, value); }

        private bool _isProgramChangeEditorVisible = true;
        public bool IsProgramChangeEditorVisible { get => _isProgramChangeEditorVisible; set => SetField(ref _isProgramChangeEditorVisible, value); }

        private bool _isSoundPeakVisible = true;
        public bool IsSoundPeakVisible { get => _isSoundPeakVisible; set => SetField(ref _isSoundPeakVisible, value); }

        private GridLength _pianoKeysWidth = new GridLength(120);
        private double _lastPianoKeysWidthValue = 120;
        public GridLength PianoKeysWidth
        {
            get => _pianoKeysWidth;
            set { if (SetField(ref _pianoKeysWidth, value) && value.Value > 0) _lastPianoKeysWidthValue = value.Value; }
        }

        private bool _isPianoKeysVisible = true;
        public bool IsPianoKeysVisible
        {
            get => _isPianoKeysVisible;
            set
            {
                if (SetField(ref _isPianoKeysVisible, value))
                    PianoKeysWidth = value ? new GridLength(_lastPianoKeysWidthValue) : new GridLength(0);
            }
        }

        private NoteViewModel? _selectedNote;
        public NoteViewModel? SelectedNote { get => _selectedNote; set => SetField(ref _selectedNote, value); }

        private FlagViewModel? _selectedFlag;
        public FlagViewModel? SelectedFlag { get => _selectedFlag; set => SetField(ref _selectedFlag, value); }

        private bool _isMultipleNotesSelected;
        public bool IsMultipleNotesSelected { get => _isMultipleNotesSelected; set => SetField(ref _isMultipleNotesSelected, value); }

        public bool CanAddNote => NoteUnderCursor == null;
        public bool CanDeleteNote => NoteUnderCursor != null || SelectedNotes.Count > 0;

        private bool _canSplitNotes;
        public bool CanSplitNotes { get => _canSplitNotes; set => SetField(ref _canSplitNotes, value); }

        private bool _canMergeNotes;
        public bool CanMergeNotes { get => _canMergeNotes; set => SetField(ref _canMergeNotes, value); }

        private Rect _selectionRectangle;
        public Rect SelectionRectangle { get => _selectionRectangle; set => SetField(ref _selectionRectangle, value); }

        private Point _rightClickPosition;
        public Point RightClickPosition { get => _rightClickPosition; set => SetField(ref _rightClickPosition, value); }

        private NoteViewModel? _noteUnderCursor;
        public NoteViewModel? NoteUnderCursor { get => _noteUnderCursor; set => SetField(ref _noteUnderCursor, value); }

        private WriteableBitmap? _pianoRollBitmap;
        public WriteableBitmap? PianoRollBitmap { get => _pianoRollBitmap; set => SetField(ref _pianoRollBitmap, value); }

        private WriteableBitmap? _thumbnailBitmap;
        public WriteableBitmap? ThumbnailBitmap { get => _thumbnailBitmap; set => SetField(ref _thumbnailBitmap, value); }

        public double PianoRollWidth { get; set; } = 3000;
        public double PianoRollHeight { get; set; } = 2560;
        public double HorizontalOffset { get; set; }
        public double VerticalScrollOffset { get; set; }
        public double VerticalOffset { get; set; }
        public double ViewportWidth { get; set; }
        public double ViewportHeight { get; set; }

        private bool _isEditingHorizontalZoom;
        public bool IsEditingHorizontalZoom { get => _isEditingHorizontalZoom; set => SetField(ref _isEditingHorizontalZoom, value); }

        private bool _isEditingVerticalZoom;
        public bool IsEditingVerticalZoom { get => _isEditingVerticalZoom; set => SetField(ref _isEditingVerticalZoom, value); }

        private string _tempHorizontalZoomInput = "100.0";
        public string TempHorizontalZoomInput { get => _tempHorizontalZoomInput; set => SetField(ref _tempHorizontalZoomInput, value); }

        private string _tempVerticalZoomInput = "1.0";
        public string TempVerticalZoomInput { get => _tempVerticalZoomInput; set => SetField(ref _tempVerticalZoomInput, value); }

        private bool _isSliderDragging;
        public bool IsSliderDragging { get => _isSliderDragging; set => SetField(ref _isSliderDragging, value); }

        public string GridQuantizeValue
        {
            get => MidiEditorSettings.Default.Grid.GridQuantizeValue;
            set
            {
                if (MidiEditorSettings.Default.Grid.GridQuantizeValue != value)
                {
                    MidiEditorSettings.Default.Grid.GridQuantizeValue = value;
                    MidiEditorSettings.Default.Save();
                    OnPropertyChanged();
                    ViewManager.UpdateTimeRuler();
                    PianoRollRenderer.RequestRedraw(true);
                }
            }
        }

        public int TimeRulerInterval
        {
            get => MidiEditorSettings.Default.Grid.TimeRulerInterval;
            set
            {
                if (MidiEditorSettings.Default.Grid.TimeRulerInterval != value)
                {
                    MidiEditorSettings.Default.Grid.TimeRulerInterval = value;
                    MidiConfiguration.Default.Save();
                    OnPropertyChanged();
                    ViewManager.UpdateTimeRuler();
                }
            }
        }

        public TuningSystemType TuningSystem
        {
            get => MidiEditorSettings.Default.TuningSystem;
            set
            {
                if (MidiEditorSettings.Default.TuningSystem != value)
                {
                    if (value != TuningSystemType.Microtonal) foreach (var note in AllNotes) note.CentOffset = 0;
                    MidiEditorSettings.Default.TuningSystem = value;
                    MidiEditorSettings.Default.Save();
                    OnPropertyChanged();
                    RefreshPianoKeys();
                    PianoRollRenderer.RequestRedraw(true);
                }
            }
        }

        public MidiInputMode MidiInputMode
        {
            get => MidiEditorSettings.Default.Input.MidiInputMode;
            set
            {
                if (MidiEditorSettings.Default.Input.MidiInputMode != value)
                {
                    MidiEditorSettings.Default.Input.MidiInputMode = value;
                    MidiEditorSettings.Default.Save();
                    MidiInputService.InputMode = value;
                    OnPropertyChanged();
                }
            }
        }

        public AdditionalKeyLabelType AdditionalKeyLabel
        {
            get => MidiEditorSettings.Default.View.AdditionalKeyLabel;
            set
            {
                if (MidiEditorSettings.Default.View.AdditionalKeyLabel != value)
                {
                    MidiEditorSettings.Default.View.AdditionalKeyLabel = value;
                    MidiEditorSettings.Default.Save();
                    OnPropertyChanged();
                    RefreshPianoKeys();
                }
            }
        }

        public bool ShowThumbnail
        {
            get => MidiEditorSettings.Default.View.ShowThumbnail;
            set
            {
                if (MidiEditorSettings.Default.View.ShowThumbnail != value)
                {
                    MidiEditorSettings.Default.View.ShowThumbnail = value;
                    MidiEditorSettings.Default.Save();
                    OnPropertyChanged();
                    if (value) ViewManager.RenderThumbnail();
                    else ThumbnailBitmap = null;
                }
            }
        }

        private string _ccSearchText = string.Empty;
        public string CcSearchText
        {
            get => _ccSearchText;
            set
            {
                if (SetField(ref _ccSearchText, value)) TrackEventManager.RefreshFiltering();
            }
        }

        private string _tempoSearchText = string.Empty;
        public string TempoSearchText
        {
            get => _tempoSearchText;
            set
            {
                if (SetField(ref _tempoSearchText, value)) TrackEventManager.RefreshFiltering();
            }
        }

        private bool _isColorizedByChannel = false;
        public bool IsColorizedByChannel
        {
            get => _isColorizedByChannel;
            set => SetField(ref _isColorizedByChannel, value);
        }

        public PianoRollMouseHandler MouseHandler => _pianoRollMouseHandler;

        public string SelectedSoundFont
        {
            get => MidiConfiguration.Default.SoundFont.PreferredSoundFont;
            set
            {
                if (MidiConfiguration.Default.SoundFont.PreferredSoundFont != value)
                {
                    MidiConfiguration.Default.SoundFont.PreferredSoundFont = value;
                    MidiConfiguration.Default.Save();
                    OnPropertyChanged();
                    PlaybackService.InitializePlayback(value);
                    UpdateAvailableInstruments(value);
                }
            }
        }

        public ICollectionView FilteredTempoEvents => TrackEventManager.FilteredTempoEvents;
        public ICollectionView FilteredControlEvents => TrackEventManager.FilteredControlEvents;
        public ICollectionView FilteredProgramChangeEvents => TrackEventManager.FilteredProgramChangeEvents;
    }
}
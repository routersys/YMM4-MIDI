using Microsoft.Win32;
using MIDI.Configuration.Models;
using MIDI.Core;
using MIDI.Core.Audio;
using MIDI.Renderers;
using MIDI.UI.Commands;
using MIDI.UI.Core;
using MIDI.UI.ViewModels.MidiEditor;
using MIDI.UI.ViewModels.MidiEditor.Modals;
using MIDI.UI.ViewModels.MidiEditor.Settings;
using MIDI.UI.Views.MidiEditor.Modals;
using NAudio.Midi;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NAudioMidi = NAudio.Midi;
using MessagePack;

namespace MIDI.UI.ViewModels
{
    public enum MidiInputMode
    {
        Keyboard,
        Realtime,
        ComputerKeyboard
    }

    public class MidiEditorViewModel : ViewModelBase, IDisposable
    {
        private NAudioMidi.MidiFile? _midiFile;
        private NAudioMidi.MidiFile? _originalMidiFile;
        private string _filePath = "ファイルが選択されていません";
        private string _projectPath = string.Empty;
        private double _horizontalZoom = 100.0;
        private double _verticalZoom = 1.0;
        private bool _isDisposed;
        private CancellationTokenSource _loadCts = new CancellationTokenSource();

        private bool _isMidiFileLoaded = false;
        public bool IsMidiFileLoaded
        {
            get => _isMidiFileLoaded;
            set => SetField(ref _isMidiFileLoaded, value);
        }

        private readonly DispatcherTimer _zoomTimer;
        private readonly DispatcherTimer _backupTimer;
        private readonly PlaybackService _playbackService;
        private readonly MidiInputService _midiInputService;
        private readonly PianoRollMouseHandler _mouseHandler;
        private readonly UndoRedoService _undoRedoService = new UndoRedoService();
        private readonly KeyboardMappingViewModel _keyboardMappingViewModel;
        private readonly Dictionary<int, NoteViewModel> _recordingNotes = new Dictionary<int, NoteViewModel>();
        private readonly DispatcherTimer _uiUpdateTimer;
        private HashSet<int> _currentlyLitKeys = new HashSet<int>();
        public Dictionary<int, PianoKeyViewModel> PianoKeysMap { get; } = new Dictionary<int, PianoKeyViewModel>();

        private readonly DispatcherTimer _scrollTimer;
        private double _horizontalOffset, _viewportWidth, _verticalOffset, _viewportHeight;
        private readonly DispatcherTimer _seekDelayTimer;

        public MetronomeViewModel Metronome { get; }
        public SoundPeakViewModel SoundPeakViewModel { get; }
        private readonly AudioMeter _audioMeter;

        public MultiNoteEditorViewModel MultiNoteEditor { get; }

        public event Action? NotesLoaded;

        public Task LoadingTask { get; private set; } = Task.CompletedTask;

        public string FileName => string.IsNullOrEmpty(_filePath) || _filePath == "ファイルが選択されていません" ? "MIDIエディター" : (string.IsNullOrEmpty(_projectPath) ? Path.GetFileName(_filePath) : $"~ {Path.GetFileName(_projectPath)} - {Path.GetFileName(_filePath)}");
        internal NAudioMidi.MidiFile? MidiFile => _midiFile;
        public MidiEditorSettings EditorSettings => MidiEditorSettings.Default;

        public ObservableCollection<NoteViewModel> AllNotes { get; } = new ObservableCollection<NoteViewModel>();
        public ObservableCollection<NoteViewModel> VisibleNotes { get; } = new ObservableCollection<NoteViewModel>();
        public ObservableCollection<NoteViewModel> SelectedNotes { get; } = new ObservableCollection<NoteViewModel>();
        private List<NoteViewModel> _clipboard = new List<NoteViewModel>();

        public ObservableCollection<PianoKeyViewModel> PianoKeys { get; } = new ObservableCollection<PianoKeyViewModel>();
        public ObservableCollection<TimeRulerViewModel> TimeRuler { get; } = new ObservableCollection<TimeRulerViewModel>();
        public ObservableCollection<GridLineViewModel> GridLines { get; } = new ObservableCollection<GridLineViewModel>();
        public ObservableCollection<GridLineViewModel> HorizontalLines { get; } = new ObservableCollection<GridLineViewModel>();
        public ObservableCollection<TempoEventViewModel> TempoEvents { get; } = new ObservableCollection<TempoEventViewModel>();
        public ObservableCollection<ControlChangeEventViewModel> ControlChangeEvents { get; } = new ObservableCollection<ControlChangeEventViewModel>();
        public Array ControllerTypes => Enum.GetValues(typeof(NAudioMidi.MidiController));
        public ObservableCollection<string> SoundFonts { get; } = new ObservableCollection<string>();
        public ObservableCollection<FlagViewModel> Flags { get; } = new ObservableCollection<FlagViewModel>();
        public ObservableCollection<FlagViewModel> SelectedFlags { get; } = new ObservableCollection<FlagViewModel>();

        public ICollectionView FilteredControlEvents { get; }
        private string _ccSearchText = string.Empty;
        public string CcSearchText
        {
            get => _ccSearchText;
            set
            {
                if (SetField(ref _ccSearchText, value))
                {
                    FilteredControlEvents.Refresh();
                }
            }
        }

        public ICollectionView FilteredTempoEvents { get; }
        private string _tempoSearchText = string.Empty;
        public string TempoSearchText
        {
            get => _tempoSearchText;
            set
            {
                if (SetField(ref _tempoSearchText, value))
                {
                    FilteredTempoEvents.Refresh();
                }
            }
        }


        public ObservableCollection<string> MidiInputDevices => _midiInputService.MidiInputDevices;

        public ICommand NewCommand { get; }
        public ICommand LoadMidiCommand { get; }
        public ICommand CloseMidiCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand SaveAsCommand { get; }
        public ICommand LoadProjectCommand { get; }
        public ICommand SaveProjectCommand { get; }
        public ICommand SaveProjectAsCommand { get; }
        public ICommand ExportAudioCommand { get; }
        public ICommand PlayPauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand RewindCommand { get; }
        public ICommand ZoomInCommand { get; }
        public ICommand ZoomOutCommand { get; }
        public ICommand SliderDragStartedCommand { get; }
        public ICommand SliderDragCompletedCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand AddNoteCommand { get; }
        public ICommand DeleteNoteCommand { get; }
        public ICommand DeleteSelectedNotesCommand { get; }
        public ICommand OpenQuantizeSettingsCommand { get; }
        public ICommand QuantizeCommand { get; }
        public ICommand OpenDisplaySettingsCommand { get; }
        public ICommand OpenEditorSettingsCommand { get; }
        public ICommand OpenKeyboardMappingCommand { get; }
        public ICommand CopyCommand { get; }
        public ICommand PasteCommand { get; }
        public ICommand SetMidiInputModeCommand { get; }
        public ICommand SetAdditionalKeyLabelCommand { get; }
        public ICommand SetTuningSystemCommand { get; }
        public ICommand AddTempoEventCommand { get; }
        public ICommand RemoveTempoEventCommand { get; }
        public ICommand AddControlChangeEventCommand { get; }
        public ICommand RemoveControlChangeEventCommand { get; }
        public ICommand ResetCcSearchCommand { get; }
        public ICommand ResetTempoSearchCommand { get; }
        public ICommand SplitSelectedNotesCommand { get; }
        public ICommand MergeSelectedNotesCommand { get; }

        public ICommand SelectAllCommand { get; }
        public ICommand InvertSelectionCommand { get; }
        public ICommand SelectSamePitchCommand { get; }
        public ICommand SelectSameChannelCommand { get; }
        public ICommand TransposeCommand { get; }
        public ICommand ChangeVelocityCommand { get; }
        public ICommand LegatoCommand { get; }
        public ICommand StaccatoCommand { get; }
        public ICommand HumanizeCommand { get; }
        public ICommand LoopSelectionCommand { get; }
        public ICommand SetPlayheadToNoteStartCommand { get; }
        public ICommand AddFlagCommand { get; }
        public ICommand CreateFlagsFromSelectionCommand { get; }
        public ICommand ZoomToSelectionCommand { get; }
        public ICommand ColorizeByChannelCommand { get; }
        public ICommand DoubleDurationCommand { get; }
        public ICommand HalveDurationCommand { get; }
        public ICommand InvertPitchCommand { get; }
        public ICommand RetrogradeCommand { get; }
        public ICommand ResetNoteColorCommand { get; }
        public ICommand DeleteSelectedFlagsCommand { get; }
        public ICommand DeleteAllFlagsCommand { get; }
        public ICommand AddFlagAtSelectionStartCommand { get; }
        public ICommand AddFlagAtSelectionEndCommand { get; }
        public ICommand GoToNextFlagCommand { get; }
        public ICommand GoToPreviousFlagCommand { get; }
        public ICommand ResetZoomCommand { get; }
        public ICommand RenameFlagCommand { get; }
        public ICommand SnapFlagToNearestTempoCommand { get; }
        public ICommand ShowShortcutHelpCommand { get; }


        public bool IsLooping => _playbackService.IsLooping;
        public string LoopRangeText => IsLooping ? $"Loop: {_playbackService.LoopStart:mm\\:ss\\.fff} - {_playbackService.LoopEnd:mm\\:ss\\.fff}" : "";
        public double LoopStartX => _playbackService.LoopStart.TotalSeconds * HorizontalZoom;
        public double LoopDurationWidth => (_playbackService.LoopEnd - _playbackService.LoopStart).TotalSeconds * HorizontalZoom;


        public int LengthInBars { get; private set; } = 16;
        public int TimeSignatureNumerator { get; private set; } = 4;
        public int TimeSignatureDenominator { get; private set; } = 4;
        private long TicksPerBar => _midiFile != null ? (long)(TimeSignatureNumerator * _midiFile.DeltaTicksPerQuarterNote * (4.0 / TimeSignatureDenominator)) : 480 * 4;

        public QuantizeSettingsViewModel QuantizeSettings { get; } = new QuantizeSettingsViewModel();

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
                    UpdateTimeRuler();
                }
            }
        }

        public string PlayButtonIcon => _playbackService.IsPlaying ? "M18,18H6V6H18V18Z" : "M8,5.14V19.14L19,12.14L8,5.14Z";

        private bool _isSliderDragging;
        public bool IsSliderDragging { get => _isSliderDragging; set => SetField(ref _isSliderDragging, value); }

        private NoteViewModel? _selectedNote;
        public NoteViewModel? SelectedNote
        {
            get => _selectedNote;
            set => SetField(ref _selectedNote, value);
        }

        private FlagViewModel? _selectedFlag;
        public FlagViewModel? SelectedFlag
        {
            get => _selectedFlag;
            set => SetField(ref _selectedFlag, value);
        }

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
                    _playbackService.InitializePlayback(value);
                    UpdatePlaybackMidiData();
                }
            }
        }

        private Rect _selectionRectangle;
        public Rect SelectionRectangle
        {
            get => _selectionRectangle;
            set => SetField(ref _selectionRectangle, value);
        }

        private bool _isNoteEditorVisible = true;
        public bool IsNoteEditorVisible { get => _isNoteEditorVisible; set => SetField(ref _isNoteEditorVisible, value); }

        private bool _isMultipleNotesSelected = false;
        public bool IsMultipleNotesSelected
        {
            get => _isMultipleNotesSelected;
            set => SetField(ref _isMultipleNotesSelected, value);
        }

        private bool _isPianoKeysVisible = true;
        public bool IsPianoKeysVisible { get => _isPianoKeysVisible; set => SetField(ref _isPianoKeysVisible, value); }

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

        private bool _isSoundPeakVisible = true;
        public bool IsSoundPeakVisible { get => _isSoundPeakVisible; set => SetField(ref _isSoundPeakVisible, value); }

        private Dock _toolbarDock = Dock.Top;
        public Dock ToolbarDock { get => _toolbarDock; set => SetField(ref _toolbarDock, value); }

        public MidiInputMode MidiInputMode
        {
            get => MidiEditorSettings.Default.Input.MidiInputMode;
            set
            {
                if (MidiEditorSettings.Default.Input.MidiInputMode != value)
                {
                    MidiEditorSettings.Default.Input.MidiInputMode = value;
                    MidiEditorSettings.Default.Save();
                    _midiInputService.InputMode = value;
                    OnPropertyChanged();
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
                    if (value)
                    {
                        RenderThumbnail();
                    }
                    else
                    {
                        ThumbnailBitmap = null;
                    }
                }
            }
        }

        private WriteableBitmap? _thumbnailBitmap;
        public WriteableBitmap? ThumbnailBitmap
        {
            get => _thumbnailBitmap;
            set => SetField(ref _thumbnailBitmap, value);
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

        public TuningSystemType TuningSystem
        {
            get => MidiEditorSettings.Default.TuningSystem;
            set
            {
                if (MidiEditorSettings.Default.TuningSystem != value)
                {
                    if (value != TuningSystemType.Microtonal)
                    {
                        foreach (var note in AllNotes)
                        {
                            note.CentOffset = 0;
                        }
                    }
                    MidiEditorSettings.Default.TuningSystem = value;
                    MidiEditorSettings.Default.Save();
                    OnPropertyChanged();
                    RefreshPianoKeys();
                    UpdateHorizontalLines();
                }
            }
        }

        public ICommand SetToolbarDockCommand { get; }

        public double HorizontalZoom
        {
            get => _horizontalZoom;
            set
            {
                if (SetField(ref _horizontalZoom, value))
                {
                    OnPropertyChanged(nameof(PianoRollWidth));
                    OnPropertyChanged(nameof(PlaybackCursorPosition));
                    foreach (var note in AllNotes)
                    {
                        note.UpdateHorizontal();
                    }
                    foreach (var flag in Flags)
                    {
                        flag.OnPropertyChanged(nameof(FlagViewModel.X));
                    }
                    UpdateVisibleNotes();
                    if (!IsSliderDragging)
                    {
                        UpdateTimeRuler();
                    }
                    OnPropertyChanged(nameof(LoopStartX));
                    OnPropertyChanged(nameof(LoopDurationWidth));
                    OnPropertyChanged(nameof(IsZoomed));
                }
            }
        }
        public double VerticalZoom
        {
            get => _verticalZoom;
            set
            {
                if (SetField(ref _verticalZoom, value))
                {
                    OnPropertyChanged(nameof(PianoRollHeight));
                    foreach (var note in AllNotes)
                    {
                        note.UpdateVertical();
                    }
                    UpdateVisibleNotes();
                    UpdateHorizontalLines();
                    OnPropertyChanged(nameof(IsZoomed));
                }
            }
        }

        public bool IsZoomed => HorizontalZoom != 100.0 || VerticalZoom != 1.0;

        public TimeSpan MaxTime
        {
            get
            {
                if (_midiFile == null) return TimeSpan.FromSeconds(30);
                var totalTicks = _midiFile.Events.SelectMany(t => t).Any() ? _midiFile.Events.SelectMany(t => t).Max(e => e.AbsoluteTime) : 0;
                var minTotalTicks = TicksPerBar * LengthInBars;
                totalTicks = Math.Max(totalTicks, minTotalTicks);
                var tempoMap = MidiProcessor.ExtractTempoMap(_midiFile, MidiConfiguration.Default);
                return MidiProcessor.TicksToTimeSpan(totalTicks, _midiFile.DeltaTicksPerQuarterNote, tempoMap);
            }
        }

        public TimeSpan CurrentTime
        {
            get => _playbackService.CurrentTime;
            set
            {
                var newTime = value;
                if (newTime < TimeSpan.Zero)
                {
                    newTime = TimeSpan.Zero;
                }
                if (newTime > MaxTime)
                {
                    newTime = MaxTime;
                }

                if (_playbackService.CurrentTime != newTime)
                {
                    _playbackService.CurrentTime = newTime;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(PlaybackCursorPosition));
                }
            }
        }

        public void SetCurrentTimeFromArrowKey(TimeSpan time)
        {
            _playbackService.SuppressSeek = true;
            CurrentTime = time;
            _playbackService.SuppressSeek = false;
            _seekDelayTimer.Stop();
            _seekDelayTimer.Start();
        }

        public double MasterVolume
        {
            get => _playbackService.MasterVolume;
            set
            {
                if (_playbackService.MasterVolume != value)
                {
                    _playbackService.MasterVolume = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool GpuAcceleration
        {
            get => MidiConfiguration.Default.Performance.GPU.EnableGpuSynthesis;
            set
            {
                if (MidiConfiguration.Default.Performance.GPU.EnableGpuSynthesis != value)
                {
                    MidiConfiguration.Default.Performance.GPU.EnableGpuSynthesis = value;
                    MidiConfiguration.Default.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool IsMidiInputEnabled
        {
            get => _midiInputService.IsMidiInputEnabled;
            set => _midiInputService.IsMidiInputEnabled = value;
        }

        public string SelectedMidiInputDevice
        {
            get => _midiInputService.SelectedMidiInputDevice;
            set => _midiInputService.SelectedMidiInputDevice = value;
        }

        public double PianoRollWidth
        {
            get
            {
                if (_midiFile == null) return 3000;
                return Math.Max(3000, MaxTime.TotalSeconds * HorizontalZoom);
            }
        }

        public int MaxNoteNumber => TuningSystem switch
        {
            TuningSystemType.TwentyFourToneEqualTemperament => 255,
            _ => 127,
        };

        public double KeyYScale => TuningSystem switch
        {
            TuningSystemType.TwentyFourToneEqualTemperament => 2.0,
            _ => 1.0,
        };


        public double PianoRollHeight => (MaxNoteNumber + 1) * 20.0 * VerticalZoom / KeyYScale;


        public double PlaybackCursorPosition => _playbackService.GetInterpolatedTime().TotalSeconds * HorizontalZoom;

        internal bool IsPlaying
        {
            get => _playbackService.IsPlaying;
            set => _playbackService.IsPlaying = value;
        }

        public PianoRollMouseHandler MouseHandler => _mouseHandler;

        private Point _rightClickPosition;
        public Point RightClickPosition { get => _rightClickPosition; set => SetField(ref _rightClickPosition, value); }

        private NoteViewModel? _noteUnderCursor;
        public NoteViewModel? NoteUnderCursor { get => _noteUnderCursor; set => SetField(ref _noteUnderCursor, value); }

        public bool CanAddNote => NoteUnderCursor == null;
        public bool CanDeleteNote => NoteUnderCursor != null || SelectedNotes.Any();

        private bool _canSplitNotes;
        public bool CanSplitNotes { get => _canSplitNotes; set => SetField(ref _canSplitNotes, value); }

        private bool _canMergeNotes;
        public bool CanMergeNotes { get => _canMergeNotes; set => SetField(ref _canMergeNotes, value); }


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
                    UpdateTimeRuler();
                }
            }
        }

        public bool LightUpKeyboardDuringPlayback
        {
            get => MidiEditorSettings.Default.View.LightUpKeyboardDuringPlayback;
            set
            {
                if (MidiEditorSettings.Default.View.LightUpKeyboardDuringPlayback != value)
                {
                    MidiEditorSettings.Default.View.LightUpKeyboardDuringPlayback = value;
                    MidiEditorSettings.Default.Save();
                    OnPropertyChanged();
                }
            }
        }
        public double VerticalScrollOffset { get; private set; }
        private string _statusText = "準備完了";
        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        private string _currentTempoText = "";
        public string CurrentTempoText
        {
            get => _currentTempoText;
            set => SetField(ref _currentTempoText, value);
        }

        private string _selectionStatusText = "";
        public string SelectionStatusText
        {
            get => _selectionStatusText;
            set => SetField(ref _selectionStatusText, value);
        }


        public MidiEditorViewModel(string? filePath)
        {
            _filePath = filePath ?? "ファイルが選択されていません";

            MidiEditorSettings.Default.Note.PropertyChanged += Editor_Note_PropertyChanged;
            MidiEditorSettings.Default.Flag.PropertyChanged += Editor_Flag_PropertyChanged;

            FilteredControlEvents = CollectionViewSource.GetDefaultView(ControlChangeEvents);
            FilteredControlEvents.Filter = FilterCcEvents;
            ResetCcSearchCommand = new RelayCommand(_ => CcSearchText = string.Empty);

            FilteredTempoEvents = CollectionViewSource.GetDefaultView(TempoEvents);
            FilteredTempoEvents.Filter = FilterTempoEvents;
            ResetTempoSearchCommand = new RelayCommand(_ => TempoSearchText = string.Empty);

            MultiNoteEditor = new MultiNoteEditorViewModel(this);
            SelectedNotes.CollectionChanged += (s, e) =>
            {
                IsMultipleNotesSelected = SelectedNotes.Count > 1;
                SelectedNote = SelectedNotes.Count == 1 ? SelectedNotes.First() : null;
                UpdateSelectionStatus();
            };

            Flags.CollectionChanged += (s, e) =>
            {
                (GoToNextFlagCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (GoToPreviousFlagCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteAllFlagsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            };
            SelectedFlags.CollectionChanged += (s, e) =>
            {
                SelectedFlag = SelectedFlags.Count == 1 ? SelectedFlags.First() : null;
                UpdateSelectionStatus();
                (DeleteSelectedFlagsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RenameFlagCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SnapFlagToNearestTempoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            };

            RefreshPianoKeys();

            _playbackService = new PlaybackService(this);
            _playbackService.AudioChunkRendered += OnAudioChunkRendered;
            Metronome = new MetronomeViewModel(_playbackService);
            _playbackService.ParentViewModel.Metronome.PropertyChanged += _playbackService.Metronome_PropertyChanged;
            Metronome.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MetronomeViewModel.Tempo))
                {
                    UpdateTempoStatus();
                }
            };
            SoundPeakViewModel = new SoundPeakViewModel();
            _audioMeter = new AudioMeter(MidiConfiguration.Default.Audio.SampleRate);
            SoundPeakViewModel.ResetLoudnessAction = () => _audioMeter.ResetLoudness();
            _keyboardMappingViewModel = new KeyboardMappingViewModel();
            _midiInputService = new MidiInputService(this, _keyboardMappingViewModel);
            _mouseHandler = new PianoRollMouseHandler(this, AllNotes);
            _mouseHandler.NoteSelected += (note, isCtrlPressed) =>
            {
                if (note != null && !note.IsSelected && !isCtrlPressed)
                {
                    foreach (var n in AllNotes) n.IsSelected = false;
                    SelectedNotes.Clear();
                }


                if (note != null)
                {
                    note.IsSelected = !note.IsSelected;
                    if (note.IsSelected)
                    {
                        if (!SelectedNotes.Contains(note)) SelectedNotes.Add(note);
                    }
                    else
                    {
                        SelectedNotes.Remove(note);
                    }
                }
                SelectedNote = SelectedNotes.FirstOrDefault();
                (DeleteNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteSelectedNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (QuantizeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CopyCommand as RelayCommand)?.RaiseCanExecuteChanged();
                UpdateMergeSplitState();
            };

            MasterVolume = 1.0;
            MidiInputMode = MidiEditorSettings.Default.Input.MidiInputMode;

            _playbackService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(PlaybackService.IsPlaying))
                {
                    OnPropertyChanged(nameof(PlayButtonIcon));
                }
            };

            _midiInputService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MidiInputService.IsMidiInputEnabled))
                {
                    OnPropertyChanged(nameof(IsMidiInputEnabled));
                }
                else if (e.PropertyName == nameof(MidiInputService.SelectedMidiInputDevice))
                {
                    OnPropertyChanged(nameof(SelectedMidiInputDevice));
                }
            };

            _uiUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;

            _zoomTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _zoomTimer.Tick += OnZoomTimerTick;

            _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _scrollTimer.Tick += (s, e) => {
                _scrollTimer.Stop();
                UpdateVisibleNotes();
            };

            _seekDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _seekDelayTimer.Tick += (s, e) => {
                _seekDelayTimer.Stop();
                _playbackService.Seek(_playbackService.CurrentTime);
            };

            _backupTimer = new DispatcherTimer();
            _backupTimer.Tick += OnBackupTimerTick;
            UpdateBackupTimer();

            NewCommand = new RelayCommand(_ => CreateNewFile());
            LoadMidiCommand = new RelayCommand(_ => LoadMidiFile());
            CloseMidiCommand = new RelayCommand(_ => CloseMidiFile(), _ => _midiFile != null);
            SaveCommand = new RelayCommand(_ =>
            {
                if (!string.IsNullOrEmpty(_projectPath))
                {
                    SaveProjectCommand?.Execute(null);
                }
                else
                {
                    SaveFile();
                }
            }, _ => _midiFile != null);
            SaveAsCommand = new RelayCommand(_ => SaveFileAs(), _ => _midiFile != null);
            LoadProjectCommand = new RelayCommand(async _ => await LoadProjectAsync());
            SaveProjectCommand = new RelayCommand(async _ => await SaveProjectAsync(), _ => !string.IsNullOrEmpty(_projectPath));
            SaveProjectAsCommand = new RelayCommand(async _ => await SaveProjectAsAsync(), _ => _midiFile != null);
            ExportAudioCommand = new RelayCommand(async _ => await ExportAudioAsync(), _ => _midiFile != null);
            PlayPauseCommand = new RelayCommand(_ => {
                _playbackService.PlayPause();
                if (_playbackService.IsPlaying)
                {
                    _uiUpdateTimer.Start();
                    StatusText = "再生中";
                }
                else
                {
                    _uiUpdateTimer.Stop();
                    OnPlayingNotesChanged(Enumerable.Empty<int>());
                    StatusText = "一時停止";
                }
            }, _ => _midiFile != null);
            StopCommand = new RelayCommand(_ => {
                _playbackService.Stop();
                _uiUpdateTimer.Stop();
                OnPropertyChanged(nameof(CurrentTime));
                OnPropertyChanged(nameof(PlaybackCursorPosition));
                OnPlayingNotesChanged(Enumerable.Empty<int>());
                StatusText = "停止";
            }, _ => _midiFile != null);
            RewindCommand = new RelayCommand(_ => CurrentTime = TimeSpan.Zero, _ => _midiFile != null);
            ZoomInCommand = new RelayCommand(_ => HorizontalZoom = Math.Min(HorizontalZoom * 1.2, 1000));
            ZoomOutCommand = new RelayCommand(_ => HorizontalZoom = Math.Max(HorizontalZoom / 1.2, 10));

            SliderDragStartedCommand = new RelayCommand(_ => IsSliderDragging = true);
            SliderDragCompletedCommand = new RelayCommand(_ =>
            {
                IsSliderDragging = false;
                UpdateTimeRuler();
            });

            UndoCommand = new RelayCommand(_ => _undoRedoService.Undo(), _ => _undoRedoService.CanUndo);
            RedoCommand = new RelayCommand(_ => _undoRedoService.Redo(), _ => _undoRedoService.CanRedo);

            AddNoteCommand = new RelayCommand(_ => AddNoteAt(RightClickPosition), _ => CanAddNote && _midiFile != null);
            DeleteNoteCommand = new RelayCommand(_ =>
            {
                if (SelectedNotes.Any()) _ = DeleteSelectedNotesAsync();
                else if (NoteUnderCursor != null) RemoveNote(NoteUnderCursor);
            }, _ => CanDeleteNote && _midiFile != null);
            DeleteSelectedNotesCommand = new RelayCommand(async _ => await DeleteSelectedNotesAsync(), _ => SelectedNotes.Any() && _midiFile != null);
            DeleteSelectedFlagsCommand = new RelayCommand(_ => DeleteSelectedFlags(), _ => SelectedFlags.Any());

            OpenQuantizeSettingsCommand = new RelayCommand(_ => IsQuantizeSettingsVisible = true);
            QuantizeCommand = new RelayCommand(_ => QuantizeSelectedNotes(), _ => SelectedNotes.Any() && _midiFile != null);
            OpenDisplaySettingsCommand = new RelayCommand(_ => OpenDisplaySettings());
            OpenEditorSettingsCommand = new RelayCommand(OpenEditorSettings);
            OpenKeyboardMappingCommand = new RelayCommand(_ => OpenKeyboardMapping());
            CopyCommand = new RelayCommand(_ => CopySelectedNotes(), _ => SelectedNotes.Any() && _midiFile != null);
            PasteCommand = new RelayCommand(_ => PasteNotes(), _ => _clipboard.Any() && _midiFile != null);
            AddTempoEventCommand = new RelayCommand(_ => AddTempoEvent());
            RemoveTempoEventCommand = new RelayCommand(p => RemoveTempoEvent(p as TempoEventViewModel), p => p is TempoEventViewModel);
            AddControlChangeEventCommand = new RelayCommand(_ => AddControlChangeEvent());
            RemoveControlChangeEventCommand = new RelayCommand(p => RemoveControlChangeEvent(p as ControlChangeEventViewModel), p => p is ControlChangeEventViewModel);

            SplitSelectedNotesCommand = new RelayCommand(_ => SplitSelectedNotes(), _ => CanSplitNotes);
            MergeSelectedNotesCommand = new RelayCommand(_ => MergeSelectedNotes(), _ => CanMergeNotes);

            SelectAllCommand = new RelayCommand(SelectAllNotes, _ => AllNotes.Any());
            InvertSelectionCommand = new RelayCommand(InvertSelection, _ => AllNotes.Any());
            SelectSamePitchCommand = new RelayCommand(SelectSamePitch, _ => SelectedNotes.Any());
            SelectSameChannelCommand = new RelayCommand(SelectSameChannel, _ => SelectedNotes.Any());
            TransposeCommand = new RelayCommand(Transpose, _ => SelectedNotes.Any());
            ChangeVelocityCommand = new RelayCommand(ChangeVelocity, _ => SelectedNotes.Any());
            LegatoCommand = new RelayCommand(ApplyLegato, _ => SelectedNotes.Any());
            StaccatoCommand = new RelayCommand(_ => ApplyStaccato(), _ => SelectedNotes.Any());
            HumanizeCommand = new RelayCommand(Humanize, _ => SelectedNotes.Any());
            LoopSelectionCommand = new RelayCommand(LoopSelection, _ => SelectedNotes.Any());
            SetPlayheadToNoteStartCommand = new RelayCommand(SetPlayheadToNoteStart, _ => SelectedNotes.Any());
            AddFlagCommand = new RelayCommand(AddFlag);
            CreateFlagsFromSelectionCommand = new RelayCommand(CreateFlagsFromSelection, _ => SelectedNotes.Any());
            ZoomToSelectionCommand = new RelayCommand(ZoomToSelection, _ => SelectedNotes.Any());
            ColorizeByChannelCommand = new RelayCommand(ColorizeByChannel);
            DoubleDurationCommand = new RelayCommand(_ => ChangeDuration(2.0), _ => SelectedNotes.Any());
            HalveDurationCommand = new RelayCommand(_ => ChangeDuration(0.5), _ => SelectedNotes.Any());
            InvertPitchCommand = new RelayCommand(InvertPitch, _ => SelectedNotes.Count > 1);
            RetrogradeCommand = new RelayCommand(Retrograde, _ => SelectedNotes.Count > 1);
            ResetNoteColorCommand = new RelayCommand(_ => ResetNoteColor());
            DeleteSelectedFlagsCommand = new RelayCommand(_ => DeleteSelectedFlags(), _ => SelectedFlags.Any());
            DeleteAllFlagsCommand = new RelayCommand(_ => DeleteAllFlags(), _ => Flags.Any());
            AddFlagAtSelectionStartCommand = new RelayCommand(_ => AddFlagAtSelectionStart(), _ => SelectedNotes.Any());
            AddFlagAtSelectionEndCommand = new RelayCommand(_ => AddFlagAtSelectionEnd(), _ => SelectedNotes.Any());
            GoToNextFlagCommand = new RelayCommand(_ => GoToNextFlag(), _ => Flags.Any());
            GoToPreviousFlagCommand = new RelayCommand(_ => GoToPreviousFlag(), _ => Flags.Any());
            ResetZoomCommand = new RelayCommand(_ => { HorizontalZoom = 100.0; VerticalZoom = 1.0; }, _ => IsZoomed);
            RenameFlagCommand = new RelayCommand(RenameFlag, _ => SelectedFlags.Count == 1);
            SnapFlagToNearestTempoCommand = new RelayCommand(SnapFlagToNearestTempo, _ => SelectedFlag != null);
            ShowShortcutHelpCommand = new RelayCommand(_ => ShowShortcutHelp());


            SetToolbarDockCommand = new RelayCommand(param => {
                if (param is Dock dock)
                {
                    ToolbarDock = dock;
                }
            });

            SetMidiInputModeCommand = new RelayCommand(param =>
            {
                if (param is string modeStr && Enum.TryParse<MidiInputMode>(modeStr, out var mode))
                {
                    MidiInputMode = mode;
                }
            });

            SetAdditionalKeyLabelCommand = new RelayCommand(param =>
            {
                if (param is string modeStr && Enum.TryParse<AdditionalKeyLabelType>(modeStr, out var mode))
                {
                    AdditionalKeyLabel = mode;
                }
            });

            SetTuningSystemCommand = new RelayCommand(param =>
            {
                if (param is TuningSystemType tuningSystem)
                {
                    TuningSystem = tuningSystem;
                }
            });

            _undoRedoService.StateChanged += () =>
            {
                (UndoCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RedoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            };

            if (!string.IsNullOrEmpty(filePath) && filePath != "ファイルが選択されていません")
            {
                LoadingTask = LoadMidiDataAsync();
            }
            else
            {
                IsMidiFileLoaded = false;
            }
            LoadSoundFonts();
        }

        private void OnBackupTimerTick(object? sender, EventArgs e)
        {
            if (MidiEditorSettings.Default.Backup.EnableAutoBackup)
            {
                _ = SaveBackupAsync();
            }
        }

        private void UpdateBackupTimer()
        {
            if (MidiEditorSettings.Default.Backup.EnableAutoBackup)
            {
                _backupTimer.Interval = TimeSpan.FromMinutes(MidiEditorSettings.Default.Backup.BackupIntervalMinutes);
                _backupTimer.Start();
            }
            else
            {
                _backupTimer.Stop();
            }
        }

        private async Task SaveBackupAsync()
        {
            if (_midiFile == null) return;
            var backupDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "backup", "editor");
            if (!Directory.Exists(backupDir))
            {
                Directory.CreateDirectory(backupDir);
            }

            var projectName = Path.GetFileNameWithoutExtension(string.IsNullOrEmpty(_projectPath) ? _filePath : _projectPath);
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmssfff");
            var backupFileName = $"{projectName}_{timestamp}.ymidi";
            var backupFilePath = Path.Combine(backupDir, backupFileName);

            await SaveProjectAsync(backupFilePath, true);

            var backupFiles = new DirectoryInfo(backupDir)
                .GetFiles("*.ymidi")
                .Where(f => f.Name.StartsWith($"{projectName}_"))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            if (backupFiles.Count > MidiEditorSettings.Default.Backup.MaxBackupFiles)
            {
                for (int i = MidiEditorSettings.Default.Backup.MaxBackupFiles; i < backupFiles.Count; i++)
                {
                    try
                    {
                        backupFiles[i].Delete();
                    }
                    catch (Exception)
                    {
                    }
                }
            }
        }

        public async void CheckForBackup()
        {
            var abnormalShutdownFlag = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "backup", "editor", ".abnormal_shutdown");
            if (!File.Exists(abnormalShutdownFlag)) return;

            File.Delete(abnormalShutdownFlag);

            var backupDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "backup", "editor");
            if (!Directory.Exists(backupDir)) return;

            var latestBackup = new DirectoryInfo(backupDir)
                .GetFiles("*.ymidi")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (latestBackup != null)
            {
                var result = MessageBox.Show($"前回のセッションは異常終了したようです。バックアップを読み込みますか？\nバックアップ時刻: {latestBackup.LastWriteTime}", "バックアップの復元", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    await LoadMidiDataAsync(latestBackup.FullName);
                }
            }
        }

        private void Editor_Note_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorSettings.Note.NoteColor))
            {
                ResetNoteColor();
            }
            if (e.PropertyName == nameof(EditorSettings.Note.SelectedNoteColor))
            {
                foreach (var note in SelectedNotes)
                {
                    note.OnPropertyChanged(nameof(NoteViewModel.FillBrush));
                }
            }
        }

        private void Editor_Flag_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorSettings.Flag.FlagColor) || e.PropertyName == nameof(EditorSettings.Flag.SelectedFlagColor))
            {
                OnPropertyChanged(nameof(EditorSettings));
            }
        }

        private void ShowShortcutHelp()
        {
            var helpWindow = new ShortcutHelpWindow
            {
                Owner = Application.Current.MainWindow
            };
            helpWindow.ShowDialog();
        }

        private void OpenEditorSettings(object? obj)
        {
            var settingsWindow = new MidiEditorSettingsWindow(MidiEditorSettings.Default)
            {
                Owner = Application.Current.MainWindow
            };
            settingsWindow.ShowDialog();
            MidiConfiguration.Default.Save();
            UpdateBackupTimer();
        }

        private void UpdateSelectionStatus()
        {
            if (SelectedNotes.Count > 1)
            {
                SelectionStatusText = $"{SelectedNotes.Count} ノートを選択中";
            }
            else if (SelectedNotes.Count == 1 && SelectedNote != null)
            {
                SelectionStatusText = $"ノート: {SelectedNote.NoteName}, Velocity: {SelectedNote.Velocity}, Start: {SelectedNote.StartTicks}";
            }
            else if (SelectedFlags.Count > 1)
            {
                SelectionStatusText = $"{SelectedFlags.Count} フラグを選択中";
            }
            else if (SelectedFlags.Count == 1 && SelectedFlag != null)
            {
                SelectionStatusText = $"フラグ: {SelectedFlag.Name}, Time: {SelectedFlag.Time:mm\\:ss\\.fff}";
            }
            else
            {
                SelectionStatusText = "";
            }
        }

        private void UpdateTempoStatus()
        {
            CurrentTempoText = $"Tempo: {Metronome.Tempo:F2} BPM";
        }

        public void ChangeDurationForSelectedNotes(long durationChangeTicks)
        {
            if (!SelectedNotes.Any() || durationChangeTicks == 0) return;

            var commands = new List<IUndoableCommand>();
            foreach (var note in SelectedNotes)
            {
                var newDuration = note.DurationTicks + durationChangeTicks;
                if (newDuration <= 0) continue;

                commands.Add(new NoteChangeCommand(
                    note,
                    note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity,
                    note.StartTicks, newDuration, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity
                ));
            }

            if (commands.Any())
            {
                ExecuteCompositeNoteChange(commands);
            }
        }

        public void ChangeVelocityForSelectedNotes(int velocityChange)
        {
            if (!SelectedNotes.Any() || velocityChange == 0) return;

            var commands = new List<IUndoableCommand>();
            foreach (var note in SelectedNotes)
            {
                var newVelocity = Math.Clamp(note.Velocity + velocityChange, 1, 127);
                if (newVelocity == note.Velocity) continue;

                commands.Add(new NoteChangeCommand(
                    note,
                    note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity,
                    note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, newVelocity
                ));
            }
            if (commands.Any())
            {
                ExecuteCompositeNoteChange(commands);
            }
        }

        public void ChangeChannelForSelectedNotes(int newChannel)
        {
            if (!SelectedNotes.Any()) return;

            var commands = new List<IUndoableCommand>();
            foreach (var note in SelectedNotes)
            {
                if (note.Channel == newChannel) continue;

                commands.Add(new NoteChangeCommand(
                    note,
                    note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity,
                    note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, newChannel, note.Velocity
                ));
            }
            if (commands.Any())
            {
                ExecuteCompositeNoteChange(commands);
            }
        }

        public void ClearSelections(bool clearNotes = true, bool clearFlags = true)
        {
            if (clearNotes)
            {
                foreach (var note in SelectedNotes.ToList()) note.IsSelected = false;
                SelectedNotes.Clear();
                SelectedNote = null;
            }

            if (clearFlags)
            {
                foreach (var flag in SelectedFlags.ToList()) flag.IsSelected = false;
                SelectedFlags.Clear();
                SelectedFlag = null;
            }
            OnPropertyChanged(nameof(SelectedFlag));
            OnPropertyChanged(nameof(SelectedNote));
        }

        public void OnFlagSelectionChanged(FlagViewModel flag, bool isSelected)
        {
            if (isSelected)
            {
                if (!SelectedFlags.Contains(flag))
                {
                    SelectedFlags.Add(flag);
                }
            }
            else
            {
                SelectedFlags.Remove(flag);
            }
            SelectedFlag = SelectedFlags.Count == 1 ? SelectedFlags.First() : null;
            OnPropertyChanged(nameof(SelectedFlag));
        }

        private void SnapFlagToNearestTempo(object? obj)
        {
            if (SelectedFlag == null || !TempoEvents.Any()) return;

            var flagTicks = TimeToTicks(SelectedFlag.Time);
            var nearestTempoEvent = TempoEvents.OrderBy(t => Math.Abs(t.AbsoluteTime - flagTicks)).First();
            var newTime = TicksToTime(nearestTempoEvent.AbsoluteTime);

            var command = new FlagChangeCommand(SelectedFlag, SelectedFlag.Time, SelectedFlag.Name, newTime, SelectedFlag.Name);
            _undoRedoService.Execute(command);
        }

        public TimeSpan TicksToTime(long ticks)
        {
            if (_midiFile == null) return TimeSpan.Zero;
            var tempoMap = MidiProcessor.ExtractTempoMap(_midiFile, MidiConfiguration.Default);
            return MidiProcessor.TicksToTimeSpan(ticks, _midiFile.DeltaTicksPerQuarterNote, tempoMap);
        }

        private void OnAudioChunkRendered(ReadOnlySpan<float> audioBuffer)
        {
            var bufferCopy = new float[audioBuffer.Length];
            audioBuffer.CopyTo(bufferCopy);

            Task.Run(() =>
            {
                _audioMeter.Process(bufferCopy);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    SoundPeakViewModel.UpdateValues(
                        _audioMeter.PeakLeft, _audioMeter.PeakRight,
                        _audioMeter.RmsLeft, _audioMeter.RmsRight,
                        _audioMeter.VuLeft, _audioMeter.VuRight,
                        _audioMeter.MomentaryLoudness, _audioMeter.ShortTermLoudness, _audioMeter.IntegratedLoudness
                    );
                });
            });
        }

        private bool FilterCcEvents(object item)
        {
            if (string.IsNullOrEmpty(CcSearchText))
            {
                (item as ControlChangeEventViewModel)!.IsMatch = false;
                return true;
            }

            if (item is ControlChangeEventViewModel vm)
            {
                var searchText = CcSearchText.ToLower();
                bool isMatch = vm.AbsoluteTime.ToString().Contains(searchText) ||
                               vm.Channel.ToString().Contains(searchText) ||
                               vm.Controller.ToString().ToLower().Contains(searchText) ||
                               vm.ControllerValue.ToString().Contains(searchText);
                vm.IsMatch = isMatch;
                return isMatch;
            }
            return false;
        }

        private bool FilterTempoEvents(object item)
        {
            if (string.IsNullOrEmpty(TempoSearchText))
            {
                (item as TempoEventViewModel)!.IsMatch = false;
                return true;
            }

            if (item is TempoEventViewModel vm)
            {
                var searchText = TempoSearchText.ToLower();
                bool isMatch = false;
                if (decimal.TryParse(searchText, out var searchDecimal))
                {
                    var culture = CultureInfo.InvariantCulture;
                    int decimalPlaces = (searchText.Contains('.')) ? searchText.Length - searchText.IndexOf('.') - 1 : 0;
                    isMatch = Math.Round(vm.Bpm, decimalPlaces).ToString(culture).Contains(searchDecimal.ToString(culture));
                }
                else
                {
                    isMatch = vm.AbsoluteTime.ToString().Contains(searchText);
                }

                vm.IsMatch = isMatch;
                return isMatch;
            }
            return false;
        }


        private void AddTempoEvent()
        {
            if (_midiFile == null) return;
            var newTempo = new NAudioMidi.TempoEvent(500000, TimeToTicks(CurrentTime));
            if (TempoEvents.Any())
            {
                newTempo.MicrosecondsPerQuarterNote = (int)(60000000 / TempoEvents.Average(t => t.Bpm));
            }
            var vm = new TempoEventViewModel(newTempo);
            vm.PlaybackPropertyChanged += OnPlaybackPropertyChanged;
            TempoEvents.Add(vm);
            _midiFile.Events[0].Add(newTempo);
            SortAndRefreshTempoTrack();
        }

        private void OnPlaybackPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            UpdateNotePositionsAndDurations();
            UpdatePlaybackMidiData();
        }

        private void RemoveTempoEvent(TempoEventViewModel? vm)
        {
            if (vm == null || _midiFile == null) return;
            vm.PlaybackPropertyChanged -= OnPlaybackPropertyChanged;
            TempoEvents.Remove(vm);
            _midiFile.Events[0].Remove(vm.TempoEvent);
            SortAndRefreshTempoTrack();
        }

        private void AddControlChangeEvent()
        {
            if (_midiFile == null) return;
            var newCC = new NAudioMidi.ControlChangeEvent(TimeToTicks(CurrentTime), 1, NAudioMidi.MidiController.MainVolume, 100);
            var vm = new ControlChangeEventViewModel(newCC);
            vm.PlaybackPropertyChanged += OnPlaybackPropertyChanged;
            ControlChangeEvents.Add(vm);

            var track = _midiFile.Events.Tracks > 1 ? 1 : 0;
            _midiFile.Events[track].Add(newCC);

            SortAndRefreshCCTrack();
        }

        private void RemoveControlChangeEvent(ControlChangeEventViewModel? vm)
        {
            if (vm == null || _midiFile == null) return;
            vm.PlaybackPropertyChanged -= OnPlaybackPropertyChanged;
            ControlChangeEvents.Remove(vm);
            foreach (var track in _midiFile.Events)
            {
                track.Remove(vm.ControlChangeEvent);
            }
            SortAndRefreshCCTrack();
        }

        private void SortAndRefreshTempoTrack()
        {
            var sorted = TempoEvents.OrderBy(e => e.AbsoluteTime).ToList();
            TempoEvents.Clear();
            foreach (var item in sorted) TempoEvents.Add(item);
            UpdateNotePositionsAndDurations();
            UpdatePlaybackMidiData();
        }
        private void UpdateNotePositionsAndDurations()
        {
            if (_midiFile == null) return;
            var newTempoMap = MidiProcessor.ExtractTempoMap(_midiFile, MidiConfiguration.Default);
            foreach (var note in AllNotes)
            {
                note.RecalculateTimes(newTempoMap);
            }
            UpdateTimeRuler();
        }
        private void SortAndRefreshCCTrack()
        {
            var sorted = ControlChangeEvents.OrderBy(e => e.AbsoluteTime).ToList();
            ControlChangeEvents.Clear();
            foreach (var item in sorted) ControlChangeEvents.Add(item);
            UpdatePlaybackMidiData();
        }

        private async Task ExportAudioAsync()
        {
            if (_midiFile == null) return;

            var customSaveDialog = new AudioSaveFileDialog(Path.ChangeExtension(FileName, ".wav"));

            if (customSaveDialog.ShowDialog() != true)
            {
                return;
            }

            var filePath = customSaveDialog.FilePath;
            if (string.IsNullOrEmpty(filePath)) return;

            var progressViewModel = new ExportProgressViewModel
            {
                FileName = Path.GetFileName(filePath),
                StatusMessage = "レンダリング準備中...",
                IsIndeterminate = true
            };

            var progressWindow = new ExportProgressWindow(progressViewModel)
            {
                Owner = Application.Current.MainWindow
            };
            progressWindow.Show();

            try
            {
                progressViewModel.StatusMessage = "オーディオをレンダリング中...";
                using var audioSource = new MidiAudioSource(_filePath, MidiConfiguration.Default);
                var audioBuffer = await audioSource.ReadAllAsync();

                progressViewModel.IsIndeterminate = false;
                progressViewModel.StatusMessage = "ファイルに書き出し中...";

                if (customSaveDialog.SelectedFormat == "WAV")
                {
                    await AudioExporter.ExportToWavAsync(filePath, audioBuffer, audioSource.Hz, progressViewModel, customSaveDialog.SelectedBitDepth, customSaveDialog.SelectedSampleRate, customSaveDialog.SelectedChannels, customSaveDialog.NormalizationType, customSaveDialog.DitheringType, customSaveDialog.FadeLength, customSaveDialog.PreventClipping, customSaveDialog.TrimSilence);
                }
                else if (customSaveDialog.SelectedFormat == "MP3")
                {
                    await AudioExporter.ExportToMp3Async(filePath, audioBuffer, audioSource.Hz, progressViewModel, customSaveDialog.SelectedBitrate, customSaveDialog.SelectedEncodeQuality, customSaveDialog.SelectedVbrMode, customSaveDialog.Title, customSaveDialog.Artist, customSaveDialog.Album, customSaveDialog.Mp3ChannelMode, customSaveDialog.Mp3LowPassFilter);
                }


                progressViewModel.StatusMessage = "完了";
                progressViewModel.IsComplete = true;
            }
            catch (Exception ex)
            {
                progressViewModel.IsIndeterminate = false;
                progressViewModel.StatusMessage = $"エラー: {ex.Message}";
                progressViewModel.IsComplete = true;
                MessageBox.Show($"書き出し中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshPianoKeys()
        {
            PianoKeys.Clear();
            PianoKeysMap.Clear();
            for (int i = MaxNoteNumber; i >= 0; i--)
            {
                var keyVM = new PianoKeyViewModel(i, this);
                PianoKeys.Add(keyVM);
                PianoKeysMap[i] = keyVM;
            }
            OnPropertyChanged(nameof(PianoRollHeight));
        }


        private void UiUpdateTimer_Tick(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CurrentTime));
            OnPropertyChanged(nameof(PlaybackCursorPosition));

            if (LightUpKeyboardDuringPlayback)
            {
                var activeNotes = AllNotes.AsParallel()
                    .Where(n => n.StartTime <= CurrentTime && n.StartTime + n.Duration > CurrentTime)
                    .Select(n => n.NoteNumber)
                    .ToList();
                OnPlayingNotesChanged(activeNotes);
            }
        }

        public void BeginScrub() => _playbackService.BeginScrub();
        public void EndScrub() => _playbackService.EndScrub();

        private void OnPlayingNotesChanged(IEnumerable<int> activeNoteNumbers)
        {
            var activeNotesSet = new HashSet<int>(activeNoteNumbers);

            var keysToTurnOff = _currentlyLitKeys.Except(activeNotesSet).ToList();
            var keysToTurnOn = activeNotesSet.Except(_currentlyLitKeys).ToList();

            foreach (var noteNumber in keysToTurnOff)
            {
                if (PianoKeysMap.TryGetValue(noteNumber, out var keyVM))
                {
                    keyVM.IsPlaying = false;
                }
            }

            foreach (var noteNumber in keysToTurnOn)
            {
                if (PianoKeysMap.TryGetValue(noteNumber, out var keyVM))
                {
                    keyVM.IsPlaying = true;
                }
            }

            _currentlyLitKeys = activeNotesSet;
        }

        private void LoadSoundFonts()
        {
            SoundFonts.Clear();
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var sfDir = Path.Combine(assemblyLocation, MidiConfiguration.Default.SoundFont.DefaultSoundFontDirectory);
            if (Directory.Exists(sfDir))
            {
                foreach (var file in Directory.GetFiles(sfDir, "*.sf2", SearchOption.AllDirectories))
                {
                    SoundFonts.Add(Path.GetFileName(file));
                }
            }
        }

        private void CreateNewFile()
        {
            var newMidiFileWindow = new NewMidiFileWindow
            {
                Owner = Application.Current.MainWindow
            };
            if (newMidiFileWindow.ShowDialog() == true && newMidiFileWindow.ViewModel.ResultMidiEvents != null)
            {
                var vm = newMidiFileWindow.ViewModel;
                var newEvents = vm.ResultMidiEvents;

                const int defaultTempo = 120;
                const int defaultNumerator = 4;
                const int defaultDenominator = 4;

                TimeSignatureNumerator = defaultNumerator;
                TimeSignatureDenominator = defaultDenominator;

                long ticksPerBar = (long)(TimeSignatureNumerator * newEvents.DeltaTicksPerQuarterNote * (4.0 / TimeSignatureDenominator));
                LengthInBars = ticksPerBar > 0 ? (int)Math.Ceiling((double)vm.CalculatedTotalTicks / ticksPerBar) : 0;
                if (LengthInBars == 0) LengthInBars = 1;


                string tempFile = Path.GetTempFileName();
                try
                {
                    NAudioMidi.MidiFile.Export(tempFile, newEvents);
                    _midiFile = new NAudioMidi.MidiFile(tempFile, false);
                    _originalMidiFile = new NAudioMidi.MidiFile(tempFile, false);
                }
                finally
                {
                    if (File.Exists(tempFile)) File.Delete(tempFile);
                }

                _filePath = "新しいMIDIファイル.mid";
                AllNotes.Clear();
                SelectedNotes.Clear();
                SelectedNote = null;
                VisibleNotes.Clear();
                TempoEvents.Clear();
                ControlChangeEvents.Clear();
                Flags.Clear();
                SelectedFlags.Clear();

                var tempoEvents = _midiFile.Events[0].OfType<NAudioMidi.TempoEvent>().Select(e => new TempoEventViewModel(e)).ToList();
                foreach (var te in tempoEvents)
                {
                    te.PlaybackPropertyChanged += OnPlaybackPropertyChanged;
                    TempoEvents.Add(te);
                }

                Metronome.UpdateMetronome(TimeSignatureNumerator, TimeSignatureDenominator, defaultTempo);
                UpdateTempoStatus();
                StatusText = "新規ファイル作成完了";

                UpdatePlaybackMidiData();
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(PianoRollWidth));
                OnPropertyChanged(nameof(PianoRollHeight));
                UpdateTimeRuler();
                UpdateHorizontalLines();
                RenderThumbnail();
                IsMidiFileLoaded = true;
                NotesLoaded?.Invoke();

                (CloseMidiCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SaveAsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (PlayPauseCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (StopCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RewindCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteSelectedNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (QuantizeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CopyCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (PasteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        public void OnScrollChanged(double horizontalOffset, double viewportWidth, double verticalOffset, double viewportHeight)
        {
            _horizontalOffset = horizontalOffset;
            _viewportWidth = viewportWidth;
            _verticalOffset = verticalOffset;
            _viewportHeight = viewportHeight;

            VerticalScrollOffset = verticalOffset;
            OnPropertyChanged(nameof(VerticalScrollOffset));

            _scrollTimer.Stop();
            _scrollTimer.Start();
        }

        private void UpdateVisibleNotes()
        {
            if (_midiFile == null || _viewportWidth <= 0 || _viewportHeight <= 0) return;

            var visibleStartTime = TimeSpan.FromSeconds(_horizontalOffset / HorizontalZoom);
            var visibleEndTime = TimeSpan.FromSeconds((_horizontalOffset + _viewportWidth) / HorizontalZoom);
            var visibleDuration = visibleEndTime - visibleStartTime;

            var visibleStartNoteRange = (MaxNoteNumber - 1 - (_verticalOffset + _viewportHeight) / (20.0 * VerticalZoom / KeyYScale));
            var visibleEndNoteRange = (MaxNoteNumber - 1 - _verticalOffset / (20.0 * VerticalZoom / KeyYScale));
            var verticalNoteCountInView = visibleEndNoteRange - visibleStartNoteRange;

            var bufferedStartTime = visibleStartTime - visibleDuration;
            var bufferedEndTime = visibleEndTime + visibleDuration;

            var bufferedStartNote = (int)Math.Max(0, Math.Floor(visibleStartNoteRange - verticalNoteCountInView));
            var bufferedEndNote = (int)Math.Min(MaxNoteNumber, Math.Ceiling(visibleEndNoteRange + verticalNoteCountInView));

            var newVisibleNotes = AllNotes.AsParallel().Where(n =>
                (n.StartTime + n.Duration) > bufferedStartTime && n.StartTime < bufferedEndTime &&
                n.NoteNumber >= bufferedStartNote && n.NoteNumber <= bufferedEndNote
            ).ToList();

            var currentVisibleNotesSet = new HashSet<NoteViewModel>(VisibleNotes);
            var newVisibleNotesSet = new HashSet<NoteViewModel>(newVisibleNotes);

            var notesToRemove = currentVisibleNotesSet.Except(newVisibleNotesSet).ToList();
            var notesToAdd = newVisibleNotesSet.Except(currentVisibleNotesSet).ToList();

            Application.Current.Dispatcher.Invoke(() => {
                foreach (var note in notesToRemove)
                {
                    VisibleNotes.Remove(note);
                }

                foreach (var note in notesToAdd)
                {
                    VisibleNotes.Add(note);
                }
            });
        }

        private void OpenDisplaySettings()
        {
            var displaySettingsWindow = new DisplaySettingsWindow
            {
                Owner = Application.Current.MainWindow
            };
            var vm = displaySettingsWindow.ViewModel;
            vm.SelectedGridOption = GridQuantizeValue;
            vm.SelectedTimeRulerInterval = TimeRulerInterval.ToString();

            if (displaySettingsWindow.ShowDialog() == true)
            {
                GridQuantizeValue = vm.SelectedGridOption;
                TimeRulerInterval = int.Parse(vm.SelectedTimeRulerInterval);
            }
        }


        private void QuantizeSelectedNotes()
        {
            if (_midiFile == null || !SelectedNotes.Any())
            {
                return;
            }

            var quantizeTicks = GetTicksPerGrid();
            if (quantizeTicks <= 0) return;

            var strength = QuantizeSettings.Strength / 100.0;
            var swing = QuantizeSettings.Swing / 100.0;

            var commands = new List<IUndoableCommand>();
            foreach (var note in SelectedNotes.ToList())
            {
                var oldStart = note.StartTicks;

                long beat = (long)Math.Floor(note.StartTicks / quantizeTicks);
                long beatStart = (long)(beat * quantizeTicks);
                double swingOffset = 0;
                if (beat % 2 != 0)
                {
                    swingOffset = quantizeTicks * swing;
                }

                long snappedTick = beatStart + (long)swingOffset;
                long newStartTicks = (long)(note.StartTicks + (snappedTick - note.StartTicks) * strength);

                commands.Add(new NoteChangeCommand(note, oldStart, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, newStartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity));
            }

            if (commands.Any())
            {
                _undoRedoService.Execute(new CompositeCommand(commands));
                SortMidiEvents();
                UpdatePlaybackMidiData();
            }
        }

        internal double GetTicksPerGrid()
        {
            if (_midiFile == null) return 0;

            double beatDurationDenom;
            var parts = GridQuantizeValue.Split('/');
            if (parts.Length == 2 && double.TryParse(parts[1].Replace("T", ""), out double denom))
            {
                beatDurationDenom = denom;
            }
            else
            {
                beatDurationDenom = 16;
            }
            if (beatDurationDenom == 0) beatDurationDenom = 16;

            bool isTriplet = GridQuantizeValue.Contains('T');
            double ticksPerGrid = (_midiFile.DeltaTicksPerQuarterNote * 4) / beatDurationDenom;
            if (isTriplet)
            {
                ticksPerGrid *= 2.0 / 3.0;
            }
            return ticksPerGrid;
        }

        private void OnZoomTimerTick(object? sender, EventArgs e)
        {
            _zoomTimer.Stop();
            OnPropertyChanged(nameof(HorizontalZoom));
            OnPropertyChanged(nameof(VerticalZoom));
            OnPropertyChanged(nameof(PianoRollWidth));
            OnPropertyChanged(nameof(PianoRollHeight));
            OnPropertyChanged(nameof(PlaybackCursorPosition));
            UpdateTimeRuler();
        }

        private async Task LoadMidiDataAsync(string? newPath = null)
        {
            await _loadCts.CancelAsync();
            _loadCts = new CancellationTokenSource();
            var token = _loadCts.Token;

            var loadingViewModel = new ProjectLoadingViewModel();
            var loadingWindow = new ProjectLoadingWindow(loadingViewModel)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            loadingWindow.Show();

            try
            {
                var path = newPath ?? _filePath;
                if (string.IsNullOrEmpty(path) || path == "ファイルが選択されていません")
                {
                    loadingWindow.Close();
                    IsMidiFileLoaded = false;
                    return;
                }

                var (newNotes, tempMidiFile, timeSigNum, timeSigDen, tempo, tempoEvents, ccEvents, project, loadedMidiPath) = await Task.Run(async () =>
                {
                    token.ThrowIfCancellationRequested();

                    var extension = Path.GetExtension(path)?.ToLower();
                    ProjectFile? loadedProject = null;
                    string midiPathToLoad = path;

                    if (extension == ".ymidi")
                    {
                        loadingViewModel.StatusMessage = "プロジェクトファイルを解析中...";
                        loadedProject = await ProjectService.LoadProjectAsync(path);
                        midiPathToLoad = loadedProject.MidiFilePath;

                        if (!File.Exists(midiPathToLoad))
                        {
                            bool found = false;
                            await Application.Current.Dispatcher.InvokeAsync(() => {
                                var missingFileDialog = new MissingMidiFileDialog(new MissingMidiFileViewModel(midiPathToLoad)) { Owner = Application.Current.MainWindow };
                                if (missingFileDialog.ShowDialog() == true)
                                {
                                    midiPathToLoad = missingFileDialog.ViewModel.NewPath;
                                    loadedProject.MidiFilePath = midiPathToLoad;
                                    found = true;
                                }
                            });
                            if (!found)
                            {
                                throw new FileNotFoundException("プロジェクトに関連付けられたMIDIファイルが見つかりません。", midiPathToLoad);
                            }
                        }
                    }

                    if (!File.Exists(midiPathToLoad))
                    {
                        throw new FileNotFoundException("MIDIファイルが見つかりません。", midiPathToLoad);
                    }

                    loadingViewModel.StatusMessage = "MIDIデータを読み込み中...";
                    var midiFile = new NAudioMidi.MidiFile(midiPathToLoad, false);
                    var originalMidi = new NAudioMidi.MidiFile(midiPathToLoad, false);

                    if (loadedProject != null)
                    {
                        loadingViewModel.StatusMessage = "差分を適用中...";
                        ProjectService.ApplyProjectToMidi(midiFile, loadedProject);
                    }


                    using (var stream = new MemoryStream(File.ReadAllBytes(midiPathToLoad)))
                    {
                        await Application.Current.Dispatcher.InvokeAsync(() => _playbackService.LoadMidiData(stream));
                    }

                    var timeSig = midiFile.Events[0].OfType<NAudioMidi.TimeSignatureEvent>().FirstOrDefault();
                    int num = timeSig?.Numerator ?? 4;
                    int den = timeSig?.Denominator ?? 4;

                    var tempoMap = MidiProcessor.ExtractTempoMap(midiFile, MidiConfiguration.Default);
                    var tempoEvent = tempoMap.FirstOrDefault();
                    double tempoValue = tempoEvent != null ? 60000000.0 / tempoEvent.MicrosecondsPerQuarterNote : 120.0;
                    var tempos = midiFile.Events[0].OfType<NAudioMidi.TempoEvent>().Select(e => new TempoEventViewModel(e)).ToList();
                    var ccs = midiFile.Events.SelectMany(track => track).OfType<NAudioMidi.ControlChangeEvent>().Select(e => new ControlChangeEventViewModel(e)).ToList();


                    var centOffsetEvents = midiFile.Events
                        .SelectMany(track => track.OfType<TextEvent>())
                        .Where(e => e.Text.StartsWith("CENT_OFFSET:"))
                        .ToDictionary(e => (e.AbsoluteTime, e.Channel, int.Parse(e.Text.Split(':')[1].Split(',')[1])), e => int.Parse(e.Text.Split(':')[1].Split(',')[2]));


                    var noteOnEvents = midiFile.Events
                        .SelectMany((track, trackIndex) => track.OfType<NAudioMidi.NoteOnEvent>()
                        .Select(noteOn => new { noteOn, trackIndex }))
                        .Where(item => item.noteOn.OffEvent != null)
                        .OrderBy(item => item.noteOn.AbsoluteTime)
                        .ToList();

                    var notes = noteOnEvents.Select(item => {
                        var noteVm = new NoteViewModel(item.noteOn, midiFile.DeltaTicksPerQuarterNote, tempoMap, this);
                        if (centOffsetEvents.TryGetValue((item.noteOn.AbsoluteTime, item.noteOn.Channel, item.noteOn.NoteNumber), out var offset))
                        {
                            noteVm.CentOffset = offset;
                        }
                        return noteVm;
                    }).ToList();

                    return (notes, midiFile, num, den, tempoValue, tempos, ccs, loadedProject, midiPathToLoad);
                }, token);

                if (token.IsCancellationRequested)
                {
                    loadingWindow.Close();
                    return;
                }

                _filePath = path;
                _originalMidiFile = new NAudioMidi.MidiFile(loadedMidiPath, false);

                if (project != null)
                {
                    _filePath = project.MidiFilePath;
                    _projectPath = newPath ?? path;
                    (SaveProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
                else
                {
                    _projectPath = string.Empty;
                    (SaveProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }


                _midiFile = tempMidiFile;
                TimeSignatureNumerator = timeSigNum;
                TimeSignatureDenominator = timeSigDen;
                Metronome.UpdateMetronome(TimeSignatureNumerator, TimeSignatureDenominator, tempo);
                UpdateTempoStatus();
                StatusText = "準備完了";

                AllNotes.Clear();
                foreach (var note in newNotes)
                {
                    AllNotes.Add(note);
                }

                TempoEvents.Clear();
                foreach (var te in tempoEvents)
                {
                    te.PlaybackPropertyChanged += OnPlaybackPropertyChanged;
                    TempoEvents.Add(te);
                }

                ControlChangeEvents.Clear();
                foreach (var cc in ccEvents)
                {
                    cc.PlaybackPropertyChanged += OnPlaybackPropertyChanged;
                    ControlChangeEvents.Add(cc);
                }

                Flags.Clear();
                if (project != null)
                {
                    foreach (var flagOp in project.FlagOperations)
                    {
                        if (flagOp.IsAdded)
                        {
                            Flags.Add(new FlagViewModel(this, flagOp.NewTime, flagOp.NewName ?? ""));
                        }
                    }
                }


                UpdateVisibleNotes();
                OnPropertyChanged(nameof(FileName));
                OnPropertyChanged(nameof(PianoRollWidth));
                OnPropertyChanged(nameof(PianoRollHeight));
                OnPropertyChanged(nameof(MaxTime));
                UpdateTimeRuler();
                UpdateHorizontalLines();
                RenderThumbnail();
                IsMidiFileLoaded = true;
                NotesLoaded?.Invoke();
                (CloseMidiCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SaveAsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (ExportAudioCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (PlayPauseCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (StopCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RewindCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (AddNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (DeleteSelectedNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (QuantizeCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (CopyCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (PasteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの読み込み中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                CloseMidiFile();
            }
            finally
            {
                loadingWindow.Close();
            }
        }

        private async void RenderThumbnail()
        {
            if (!ShowThumbnail || _midiFile == null)
            {
                ThumbnailBitmap = null;
                return;
            }

            int width = 1024;
            int height = 60;

            var notesToRender = AllNotes.ToList();
            var maxTime = MaxTime;

            if (!notesToRender.Any())
            {
                var emptyBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
                emptyBitmap.Freeze();
                ThumbnailBitmap = emptyBitmap;
                return;
            }

            byte[]? pixels = await Task.Run(() =>
            {
                try
                {
                    var pixelData = new byte[width * height * 4];
                    var totalDuration = maxTime.TotalSeconds;
                    if (totalDuration <= 0) return pixelData;

                    int minNote = notesToRender.Min(n => n.NoteNumber);
                    int maxNote = notesToRender.Max(n => n.NoteNumber);
                    int noteRange = Math.Max(1, maxNote - minNote);

                    foreach (var note in notesToRender)
                    {
                        var xStart = (int)((note.StartTime.TotalSeconds / totalDuration) * width);
                        var xEnd = (int)(((note.StartTime + note.Duration).TotalSeconds / totalDuration) * width);
                        var y = height - 1 - (int)(((double)(note.NoteNumber - minNote) / noteRange) * (height - 1));

                        int noteHeight = 1;

                        for (int currentY = y; currentY < y + noteHeight && currentY < height; currentY++)
                        {
                            if (currentY < 0) continue;
                            for (int x = xStart; x < xEnd && x < width; x++)
                            {
                                if (x < 0) continue;
                                int index = (currentY * width + x) * 4;
                                var color = note.Color;
                                pixelData[index] = color.B;
                                pixelData[index + 1] = color.G;
                                pixelData[index + 2] = color.R;
                                pixelData[index + 3] = color.A;
                            }
                        }
                    }
                    return pixelData;
                }
                catch
                {
                    return null;
                }
            });

            if (pixels != null)
            {
                var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
                bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
                bitmap.Freeze();
                ThumbnailBitmap = bitmap;
            }
        }


        public void UpdatePlaybackMidiData()
        {
            if (_midiFile == null) return;

            string tempFile = string.Empty;
            try
            {
                tempFile = Path.GetTempFileName();
                NAudioMidi.MidiFile.Export(tempFile, _midiFile.Events);

                using (var stream = new MemoryStream(File.ReadAllBytes(tempFile)))
                {
                    _playbackService.LoadMidiData(stream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"MIDIデータの更新に失敗しました: {ex.Message}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempFile) && File.Exists(tempFile))
                {
                    try
                    {
                        File.Delete(tempFile);
                    }
                    catch { }
                }
            }
        }

        private void UpdateTimeRuler()
        {
            TimeRuler.Clear();
            GridLines.Clear();
            if (_midiFile == null) return;

            var tempoMap = MidiProcessor.ExtractTempoMap(_midiFile, MidiConfiguration.Default);
            var totalTicks = _midiFile.Events.SelectMany(t => t).Any() ? _midiFile.Events.SelectMany(t => t).Max(e => e.AbsoluteTime) : TicksPerBar * 4;
            var minTotalTicks = TicksPerBar * LengthInBars;
            totalTicks = Math.Max(totalTicks, minTotalTicks);

            var totalDuration = MidiProcessor.TicksToTimeSpan(totalTicks, _midiFile.DeltaTicksPerQuarterNote, tempoMap);
            var pianoRollWidth = Math.Max(3000, totalDuration.TotalSeconds * HorizontalZoom);

            var ticksPerGrid = GetTicksPerGrid();
            if (ticksPerGrid <= 0) return;

            for (long ticks = 0; ; ticks += (long)Math.Max(1, ticksPerGrid))
            {
                var time = MidiProcessor.TicksToTimeSpan(ticks, _midiFile.DeltaTicksPerQuarterNote, tempoMap);
                var x = time.TotalSeconds * HorizontalZoom;
                if (x > pianoRollWidth + HorizontalZoom * 2) break;
                GridLines.Add(new GridLineViewModel(x, 0));
                if (ticks > totalTicks + TicksPerBar) break;
            }

            double intervalSeconds = TimeRulerInterval;
            if (intervalSeconds <= 0) intervalSeconds = 1.0;

            for (double seconds = 0; ; seconds += intervalSeconds)
            {
                var time = TimeSpan.FromSeconds(seconds);
                var ticks = TimeToTicks(time);
                var x = MidiProcessor.TicksToTimeSpan(ticks, _midiFile.DeltaTicksPerQuarterNote, tempoMap).TotalSeconds * HorizontalZoom;

                var nextTime = TimeSpan.FromSeconds(seconds + intervalSeconds);
                var nextTicks = TimeToTicks(nextTime);
                var nextX = MidiProcessor.TicksToTimeSpan(nextTicks, _midiFile.DeltaTicksPerQuarterNote, tempoMap).TotalSeconds * HorizontalZoom;

                var width = nextX - x;

                if (width > 50)
                {
                    TimeRuler.Add(new TimeRulerViewModel(time, width, x));
                }

                if (time > totalDuration.Add(TimeSpan.FromSeconds(intervalSeconds)) || x > pianoRollWidth + 200)
                {
                    break;
                }
            }

            OnPropertyChanged(nameof(PianoRollWidth));
        }

        private void UpdateHorizontalLines()
        {
            HorizontalLines.Clear();
            for (int i = 0; i <= MaxNoteNumber; i++)
            {
                var y = (MaxNoteNumber - i) * 20.0 * VerticalZoom / KeyYScale;
                HorizontalLines.Add(new GridLineViewModel(0, y));
            }
        }

        private void SaveFile()
        {
            if (_midiFile is null || string.IsNullOrEmpty(_filePath) || _filePath == "ファイルが選択されていません")
            {
                SaveFileAs();
                return;
            }

            try
            {
                var events = new NAudioMidi.MidiEventCollection(_midiFile.FileFormat, _midiFile.DeltaTicksPerQuarterNote);
                for (int i = 0; i < _midiFile.Tracks; i++)
                {
                    events.AddTrack();
                    foreach (var ev in _midiFile.Events[i])
                    {
                        if (ev is NAudioMidi.TextEvent te && te.Text.StartsWith("CENT_OFFSET:"))
                        {
                            continue;
                        }
                        events[i].Add(ev.Clone());
                    }
                }

                foreach (var note in AllNotes)
                {
                    if (note.CentOffset != 0)
                    {
                        string text = $"CENT_OFFSET:{note.NoteOnEvent.Channel},{note.NoteNumber},{note.CentOffset}";
                        byte[] data = System.Text.Encoding.UTF8.GetBytes(text);
                        events[0].Add(new NAudioMidi.MetaEvent(NAudioMidi.MetaEventType.TextEvent, data.Length, note.StartTicks));
                        var writer = new System.IO.MemoryStream();
                        var binaryWriter = new System.IO.BinaryWriter(writer);
                        binaryWriter.Write((byte)NAudioMidi.MetaEventType.TextEvent);
                        NAudioMidi.MidiEvent.WriteVarInt(binaryWriter, data.Length);
                        binaryWriter.Write(data);
                    }
                }

                NAudioMidi.MidiFile.Export(_filePath, events);
                _playbackService.LoadMidiFile(_filePath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの保存中にエラーが発生しました: {ex.Message}");
            }
        }

        private void SaveFileAs()
        {
            if (_midiFile is null) return;
            var saveFileDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "MIDI File (*.mid;*.midi)|*.mid;*.midi",
                FileName = FileName
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                _filePath = saveFileDialog.FileName;
                _projectPath = string.Empty;
                (SaveProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
                SaveFile();
                OnPropertyChanged(nameof(FileName));
            }
        }

        public TimeSpan PositionToTime(double x)
        {
            if (MidiFile == null || HorizontalZoom == 0) return TimeSpan.Zero;
            var timeInSeconds = x / HorizontalZoom;
            return TimeSpan.FromSeconds(timeInSeconds);
        }

        public void AddNoteAt(Point position)
        {
            if (_midiFile == null) return;

            long ticks;
            if (EditorSettings.Grid.EnableSnapToGrid && !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
            {
                var ticksPerGrid = GetTicksPerGrid();
                var time = PositionToTime(position.X);
                var rawTicks = TimeToTicks(time);
                ticks = (long)(Math.Round(rawTicks / ticksPerGrid) * ticksPerGrid);
            }
            else
            {
                var time = PositionToTime(position.X);
                ticks = TimeToTicks(time);
            }

            var noteNumber = MaxNoteNumber - (int)Math.Floor(position.Y / (20.0 * VerticalZoom)) - 1;
            if (noteNumber < 0 || noteNumber > 127) return;

            var durationTicks = (long)GetTicksPerGrid();
            if (durationTicks <= 0) durationTicks = _midiFile.DeltaTicksPerQuarterNote / 4;

            RemoveOverlappingNotes(ticks, ticks + durationTicks, noteNumber);

            var velocity = 100;
            var noteOn = new NAudioMidi.NoteOnEvent(ticks, 1, noteNumber, velocity, (int)durationTicks);
            var noteOff = new NAudioMidi.NoteEvent(ticks + noteOn.NoteLength, 1, NAudioMidi.MidiCommandCode.NoteOff, noteNumber, 0);
            noteOn.OffEvent = noteOff;

            var tempoMap = MidiProcessor.ExtractTempoMap(_midiFile, MidiConfiguration.Default);
            var noteViewModel = new NoteViewModel(noteOn, _midiFile.DeltaTicksPerQuarterNote, tempoMap, this);

            _undoRedoService.Execute(new AddNoteCommand(this, noteViewModel));
        }

        internal void AddNoteInternal(NoteViewModel noteViewModel)
        {
            if (_midiFile == null) return;
            var noteOnEvent = noteViewModel.NoteOnEvent;
            _midiFile.Events[0].Add(noteOnEvent);
            if (noteOnEvent.OffEvent != null)
            {
                _midiFile.Events[0].Add(noteOnEvent.OffEvent);
            }
            SortMidiEvents();
            AllNotes.Add(noteViewModel);
            var sortedNotes = AllNotes.OrderBy(n => n.StartTicks).ToList();
            AllNotes.Clear();
            foreach (var n in sortedNotes) AllNotes.Add(n);
            UpdateVisibleNotes();
            UpdatePlaybackMidiData();
        }
        internal void SortMidiEvents()
        {
            if (_midiFile == null) return;
            foreach (var track in _midiFile.Events)
            {
                var events = track.OrderBy(e => e.AbsoluteTime).ToList();
                track.Clear();
                foreach (var e in events)
                {
                    track.Add(e);
                }
            }
        }
        public void RemoveNote(NoteViewModel noteViewModel)
        {
            _undoRedoService.Execute(new RemoveNoteCommand(this, noteViewModel));
        }

        private async Task DeleteSelectedNotesAsync()
        {
            var notesToDelete = SelectedNotes.ToList();
            if (!notesToDelete.Any()) return;

            await Task.Run(() =>
            {
                var commands = new List<IUndoableCommand>();
                foreach (var note in notesToDelete)
                {
                    commands.Add(new RemoveNoteCommand(this, note));
                }

                if (commands.Any())
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        _undoRedoService.Execute(new CompositeCommand(commands));
                        SelectedNotes.Clear();
                        SelectedNote = null;
                    });
                }
            });
        }


        internal void RemoveNoteInternal(NoteViewModel noteViewModel)
        {
            if (_midiFile == null) return;

            var noteOnEvent = noteViewModel.NoteOnEvent;
            var noteOffEvent = noteOnEvent.OffEvent;

            foreach (var track in _midiFile.Events)
            {
                track.Remove(noteOnEvent);
                if (noteOffEvent != null)
                {
                    track.Remove(noteOffEvent);
                }
            }

            AllNotes.Remove(noteViewModel);
            VisibleNotes.Remove(noteViewModel);
            if (SelectedNote == noteViewModel)
            {
                SelectedNote = null;
            }
            SelectedNotes.Remove(noteViewModel);
            UpdatePlaybackMidiData();
        }

        public void ExecuteNoteChange(NoteViewModel note, long oldStart, long oldDuration, int oldNoteNumber, int oldCentOffset, int oldChannel, int oldVelocity, long newStart, long newDuration, int newNoteNumber, int newCentOffset, int newChannel, int newVelocity)
        {
            var command = new NoteChangeCommand(note, oldStart, oldDuration, oldNoteNumber, oldCentOffset, oldChannel, oldVelocity, newStart, newDuration, newNoteNumber, newCentOffset, newChannel, newVelocity);
            _undoRedoService.Execute(command);
            SortMidiEvents();
            UpdatePlaybackMidiData();
        }


        public void ExecuteCompositeNoteChange(List<IUndoableCommand> commands)
        {
            if (commands.Any())
            {
                _undoRedoService.Execute(new CompositeCommand(commands));
                SortMidiEvents();
                UpdatePlaybackMidiData();
            }
        }

        public void OnPianoRollMouseDown(Point position, MouseButtonEventArgs e)
        {
            if (_midiFile != null)
            {
                MouseHandler.OnPianoRollMouseDown(position, e, _midiFile);
            }
        }
        public void OnPianoRollMouseUp(MouseButtonEventArgs e)
        {
            MouseHandler.OnPianoRollMouseUp(e);
        }

        public void OnPianoRollMouseMove(Point position, MouseEventArgs e)
        {
            if (_midiFile != null)
            {
                MouseHandler.OnPianoRollMouseMove(position, e);
            }
        }

        public void PlayPianoKey(int noteNumber)
        {
            _playbackService.PlayPianoKey(noteNumber);
            var keyVM = PianoKeys.FirstOrDefault(k => k.NoteNumber == noteNumber);
            if (keyVM != null) keyVM.IsKeyboardPlaying = true;
        }

        public void StopPianoKey(int noteNumber)
        {
            _playbackService.StopPianoKey(noteNumber);
            var keyVM = PianoKeys.FirstOrDefault(k => k.NoteNumber == noteNumber);
            if (keyVM != null) keyVM.IsKeyboardPlaying = false;
        }

        public void UpdateContextMenuState(Point position)
        {
            RightClickPosition = position;
            NoteUnderCursor = AllNotes.FirstOrDefault(n =>
                position.X >= n.X && position.X <= n.X + n.Width &&
                position.Y >= n.Y && position.Y <= n.Y + n.Height);

            if (NoteUnderCursor != null && !SelectedNotes.Contains(NoteUnderCursor))
            {
                foreach (var note in SelectedNotes)
                {
                    note.IsSelected = false;
                }
                SelectedNotes.Clear();
                NoteUnderCursor.IsSelected = true;
                SelectedNotes.Add(NoteUnderCursor);
                SelectedNote = NoteUnderCursor;
            }

            UpdateMergeSplitState();

            OnPropertyChanged(nameof(CanAddNote));
            OnPropertyChanged(nameof(CanDeleteNote));
            (AddNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void UpdateMergeSplitState()
        {
            CanSplitNotes = SelectedNotes.Count == 1;

            if (SelectedNotes.Count > 1)
            {
                var firstNote = SelectedNotes.First();
                CanMergeNotes = SelectedNotes.All(n => n.NoteNumber == firstNote.NoteNumber);
            }
            else
            {
                CanMergeNotes = false;
            }

            (SplitSelectedNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MergeSelectedNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }


        private void OpenKeyboardMapping()
        {
            var keyboardMappingWindow = new KeyboardMappingWindow
            {
                Owner = Application.Current.MainWindow
            };
            keyboardMappingWindow.ShowDialog();
        }

        public void HandleKeyDown(Key key)
        {
            if (MidiInputMode == MidiInputMode.Keyboard) return;

            var mapping = _keyboardMappingViewModel.Mappings.FirstOrDefault(m => m.Key.ToString().Equals(key.ToString(), StringComparison.OrdinalIgnoreCase));
            if (mapping != null)
            {
                if (MidiInputMode == MidiInputMode.Realtime && IsPlaying)
                {
                    AddNoteAtCurrentTime(mapping.NoteNumber, 100);
                }
                else if (MidiInputMode == MidiInputMode.ComputerKeyboard && PianoKeysMap.TryGetValue(mapping.NoteNumber, out var keyVm))
                {
                    if (!keyVm.IsPressed)
                    {
                        PlayPianoKey(mapping.NoteNumber);
                        keyVm.IsPressed = true;
                    }
                }
            }
        }

        public void HandleKeyUp(Key key)
        {
            if (MidiInputMode == MidiInputMode.Keyboard) return;
            var mapping = _keyboardMappingViewModel.Mappings.FirstOrDefault(m => m.Key.ToString().Equals(key.ToString(), StringComparison.OrdinalIgnoreCase));
            if (mapping != null)
            {
                if (MidiInputMode == MidiInputMode.Realtime && IsPlaying)
                {
                    StopNoteAtCurrentTime(mapping.NoteNumber);
                }
                else if (MidiInputMode == MidiInputMode.ComputerKeyboard && PianoKeysMap.TryGetValue(mapping.NoteNumber, out var keyVm))
                {
                    StopPianoKey(mapping.NoteNumber);
                    keyVm.IsPressed = false;
                }
            }
        }


        private void CopySelectedNotes()
        {
            _clipboard.Clear();
            if (!SelectedNotes.Any()) return;

            foreach (var note in SelectedNotes)
            {
                _clipboard.Add(note);
            }
        }

        private void PasteNotes()
        {
            if (!_clipboard.Any() || _midiFile == null) return;

            var pasteTimeTicks = TimeToTicks(CurrentTime);
            var firstNoteTicks = _clipboard.Min(n => n.StartTicks);

            var addedNotes = new List<NoteViewModel>();

            foreach (var originalNote in _clipboard)
            {
                var timeOffset = originalNote.StartTicks - firstNoteTicks;
                var newStartTicks = pasteTimeTicks + timeOffset;
                var newDurationTicks = originalNote.DurationTicks;

                RemoveOverlappingNotes(newStartTicks, newStartTicks + newDurationTicks, originalNote.NoteNumber);

                var noteOn = new NAudioMidi.NoteOnEvent(newStartTicks, 1, originalNote.NoteNumber, originalNote.Velocity, (int)newDurationTicks);
                var noteOff = new NAudioMidi.NoteEvent(newStartTicks + noteOn.NoteLength, 1, NAudioMidi.MidiCommandCode.NoteOff, originalNote.NoteNumber, 0);
                noteOn.OffEvent = noteOff;

                var tempoMap = MidiProcessor.ExtractTempoMap(_midiFile, MidiConfiguration.Default);
                var noteViewModel = new NoteViewModel(noteOn, _midiFile.DeltaTicksPerQuarterNote, tempoMap, this);
                addedNotes.Add(noteViewModel);
            }

            _undoRedoService.Execute(new CompositeCommand(addedNotes.Select(n => new AddNoteCommand(this, n))));
        }

        public long TimeToTicks(TimeSpan time)
        {
            if (_midiFile == null) return 0;
            var tempoMap = MidiProcessor.ExtractTempoMap(_midiFile, MidiConfiguration.Default);
            if (!tempoMap.Any())
            {
                double secondsPerTick = 0.5 / _midiFile.DeltaTicksPerQuarterNote;
                return (long)(time.TotalSeconds / secondsPerTick);
            }

            double accumulatedSeconds = 0;
            long lastTicks = 0;
            double currentTempo = tempoMap[0].MicrosecondsPerQuarterNote;
            double lastTempo = currentTempo;

            foreach (var tempoEvent in tempoMap)
            {
                if (tempoEvent.AbsoluteTime > 0)
                {
                    long deltaTicks = tempoEvent.AbsoluteTime - lastTicks;
                    if (deltaTicks > 0)
                    {
                        double deltaSeconds = (deltaTicks / (double)_midiFile.DeltaTicksPerQuarterNote) * (lastTempo / 1000000.0);
                        if (accumulatedSeconds + deltaSeconds >= time.TotalSeconds)
                        {
                            double secondsIntoSegment = time.TotalSeconds - accumulatedSeconds;
                            long ticksIntoSegment = (long)(secondsIntoSegment * (1000000.0 / lastTempo) * _midiFile.DeltaTicksPerQuarterNote);
                            return lastTicks + ticksIntoSegment;
                        }
                        accumulatedSeconds += deltaSeconds;
                    }
                }
                lastTicks = tempoEvent.AbsoluteTime;
                lastTempo = tempoEvent.MicrosecondsPerQuarterNote;
            }

            double remainingSeconds = time.TotalSeconds - accumulatedSeconds;
            long remainingTicks = (long)(remainingSeconds * (1000000.0 / lastTempo) * _midiFile.DeltaTicksPerQuarterNote);
            return lastTicks + remainingTicks;
        }

        public void AddNoteAtCurrentTime(int noteNumber, int velocity)
        {
            if (_midiFile == null || !IsPlaying || _recordingNotes.ContainsKey(noteNumber)) return;

            var startTicks = TimeToTicks(CurrentTime);

            RemoveOverlappingNotes(startTicks, startTicks + 1, noteNumber);

            var tempoMap = MidiProcessor.ExtractTempoMap(_midiFile, MidiConfiguration.Default);
            var noteOn = new NAudioMidi.NoteOnEvent(startTicks, 1, noteNumber, velocity, 0);
            var noteViewModel = new NoteViewModel(noteOn, _midiFile.DeltaTicksPerQuarterNote, tempoMap, this);

            _recordingNotes[noteNumber] = noteViewModel;

            Application.Current.Dispatcher.Invoke(() =>
            {
                AllNotes.Add(noteViewModel);
                UpdateVisibleNotes();
            });
        }

        public void StopNoteAtCurrentTime(int noteNumber)
        {
            if (_midiFile == null || !_recordingNotes.ContainsKey(noteNumber)) return;

            var noteViewModel = _recordingNotes[noteNumber];
            var noteOn = noteViewModel.NoteOnEvent;
            var endTicks = TimeToTicks(CurrentTime);
            var durationTicks = endTicks - noteOn.AbsoluteTime;

            if (durationTicks <= 10)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    AllNotes.Remove(noteViewModel);
                    UpdateVisibleNotes();
                });
                _recordingNotes.Remove(noteNumber);
                return;
            }

            RemoveOverlappingNotes(noteOn.AbsoluteTime, endTicks, noteNumber, noteViewModel);

            var oldStart = noteOn.AbsoluteTime;
            var oldDuration = noteOn.NoteLength;
            var oldNoteNumber = noteOn.NoteNumber;
            var oldCentOffset = noteViewModel.CentOffset;
            var oldChannel = noteViewModel.Channel;

            noteOn.NoteLength = (int)durationTicks;
            var noteOff = new NAudioMidi.NoteEvent(endTicks, 1, NAudioMidi.MidiCommandCode.NoteOff, noteNumber, 0);
            noteOn.OffEvent = noteOff;

            noteViewModel.UpdateNote(noteOn.AbsoluteTime, durationTicks);

            _undoRedoService.Execute(new NoteChangeCommand(noteViewModel, oldStart, (long)oldDuration, oldNoteNumber, oldCentOffset, oldChannel, noteViewModel.Velocity, noteOn.AbsoluteTime, (long)noteOn.NoteLength, noteOn.NoteNumber, noteViewModel.CentOffset, noteViewModel.Channel, noteViewModel.Velocity));

            _midiFile.Events[0].Add(noteOn);
            _midiFile.Events[0].Add(noteOff);

            UpdatePlaybackMidiData();
            UpdateVisibleNotes();

            _recordingNotes.Remove(noteNumber);
        }


        internal void RemoveOverlappingNotes(long startTicks, long endTicks, int noteNumber, NoteViewModel? noteToKeep = null)
        {
            if (_midiFile == null) return;

            switch (EditorSettings.Note.NoteOverlapBehavior)
            {
                case NoteOverlapBehavior.Keep:
                    return;

                case NoteOverlapBehavior.Overwrite:
                case NoteOverlapBehavior.Delete:
                    var overlappingNotes = AllNotes.Where(n =>
                        n != noteToKeep &&
                        n.NoteNumber == noteNumber &&
                        startTicks < n.StartTicks + n.DurationTicks &&
                        endTicks > n.StartTicks
                    ).ToList();

                    if (overlappingNotes.Any())
                    {
                        _undoRedoService.Execute(new CompositeCommand(overlappingNotes.Select(n => new RemoveNoteCommand(this, n))));
                    }
                    break;
            }
        }

        private void LoadMidiFile()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "MIDI Files (*.mid;*.midi;*.kar;*.rmi)|*.mid;*.midi;*.kar;*.rmi|All files (*.*)|*.*",
                Title = "MIDIファイルを開く"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _filePath = openFileDialog.FileName;
                _projectPath = string.Empty;
                LoadingTask = LoadMidiDataAsync();
                (SaveProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private void CloseMidiFile()
        {
            _playbackService.Stop();
            _midiFile = null;
            _originalMidiFile = null;
            AllNotes.Clear();
            VisibleNotes.Clear();
            SelectedNotes.Clear();
            SelectedNote = null;
            TimeRuler.Clear();
            GridLines.Clear();
            HorizontalLines.Clear();
            Flags.Clear();
            SelectedFlags.Clear();
            TempoEvents.Clear();
            ControlChangeEvents.Clear();
            _filePath = "ファイルが選択されていません";
            _projectPath = string.Empty;
            IsMidiFileLoaded = false;
            OnPropertyChanged(nameof(FileName));
            (CloseMidiCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveAsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveProjectAsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PlayPauseCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (StopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RewindCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AddNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteSelectedNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (QuantizeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CopyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PasteCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        private void SplitSelectedNotes()
        {
            if (!CanSplitNotes || _midiFile == null) return;

            var noteToSplit = SelectedNotes.First();
            var splitTime = PositionToTime(RightClickPosition.X);
            var splitTick = TimeToTicks(splitTime);

            if (splitTick <= noteToSplit.StartTicks || splitTick >= noteToSplit.StartTicks + noteToSplit.DurationTicks)
            {
                splitTick = noteToSplit.StartTicks + noteToSplit.DurationTicks / 2;
            }


            var commands = new List<IUndoableCommand>();

            commands.Add(new RemoveNoteCommand(this, noteToSplit));

            var tempoMap = MidiProcessor.ExtractTempoMap(_midiFile, MidiConfiguration.Default);

            var duration1 = splitTick - noteToSplit.StartTicks;
            if (duration1 > 0)
            {
                var noteOn1 = new NAudioMidi.NoteOnEvent(noteToSplit.StartTicks, 1, noteToSplit.NoteNumber, noteToSplit.Velocity, (int)duration1);
                var noteOff1 = new NAudioMidi.NoteEvent(noteToSplit.StartTicks + duration1, 1, NAudioMidi.MidiCommandCode.NoteOff, noteToSplit.NoteNumber, 0);
                noteOn1.OffEvent = noteOff1;
                var noteVm1 = new NoteViewModel(noteOn1, _midiFile.DeltaTicksPerQuarterNote, tempoMap, this) { CentOffset = noteToSplit.CentOffset };
                commands.Add(new AddNoteCommand(this, noteVm1));
            }

            var duration2 = (noteToSplit.StartTicks + noteToSplit.DurationTicks) - splitTick;
            if (duration2 > 0)
            {
                var noteOn2 = new NAudioMidi.NoteOnEvent(splitTick, 1, noteToSplit.NoteNumber, noteToSplit.Velocity, (int)duration2);
                var noteOff2 = new NAudioMidi.NoteEvent(splitTick + duration2, 1, NAudioMidi.MidiCommandCode.NoteOff, noteToSplit.NoteNumber, 0);
                noteOn2.OffEvent = noteOff2;
                var noteVm2 = new NoteViewModel(noteOn2, _midiFile.DeltaTicksPerQuarterNote, tempoMap, this) { CentOffset = noteToSplit.CentOffset };
                commands.Add(new AddNoteCommand(this, noteVm2));
            }

            _undoRedoService.Execute(new CompositeCommand(commands));
        }

        private void MergeSelectedNotes()
        {
            if (!CanMergeNotes || _midiFile == null) return;

            var orderedNotes = SelectedNotes.OrderBy(n => n.StartTicks).ToList();
            var firstNote = orderedNotes.First();
            var lastNote = orderedNotes.Last();

            var newStartTick = firstNote.StartTicks;
            var newEndTick = lastNote.StartTicks + lastNote.DurationTicks;
            var newDuration = newEndTick - newStartTick;

            var commands = new List<IUndoableCommand>();

            foreach (var note in orderedNotes)
            {
                commands.Add(new RemoveNoteCommand(this, note));
            }

            var tempoMap = MidiProcessor.ExtractTempoMap(_midiFile, MidiConfiguration.Default);
            var noteOn = new NAudioMidi.NoteOnEvent(newStartTick, 1, firstNote.NoteNumber, firstNote.Velocity, (int)newDuration);
            var noteOff = new NAudioMidi.NoteEvent(newEndTick, 1, NAudioMidi.MidiCommandCode.NoteOff, firstNote.NoteNumber, 0);
            noteOn.OffEvent = noteOff;
            var newNoteVm = new NoteViewModel(noteOn, _midiFile.DeltaTicksPerQuarterNote, tempoMap, this) { CentOffset = firstNote.CentOffset };
            commands.Add(new AddNoteCommand(this, newNoteVm));

            _undoRedoService.Execute(new CompositeCommand(commands));
        }

        private void SelectAllNotes(object? obj)
        {
            if (AllNotes.All(n => n.IsSelected))
            {
                foreach (var note in AllNotes)
                {
                    note.IsSelected = false;
                }
                SelectedNotes.Clear();
            }
            else
            {
                SelectedNotes.Clear();
                foreach (var note in AllNotes)
                {
                    note.IsSelected = true;
                    SelectedNotes.Add(note);
                }
            }
            SelectedNote = SelectedNotes.FirstOrDefault();
        }


        private void InvertSelection(object? obj)
        {
            var allNotes = AllNotes.ToList();
            var selected = SelectedNotes.ToList();
            SelectedNotes.Clear();
            foreach (var note in allNotes)
            {
                note.IsSelected = !selected.Contains(note);
                if (note.IsSelected)
                {
                    SelectedNotes.Add(note);
                }
            }
            SelectedNote = SelectedNotes.FirstOrDefault();
        }

        private void SelectSamePitch(object? obj)
        {
            if (!SelectedNotes.Any()) return;
            var pitch = SelectedNotes.First().NoteNumber;
            foreach (var note in AllNotes)
            {
                if (note.NoteNumber == pitch)
                {
                    note.IsSelected = true;
                    if (!SelectedNotes.Contains(note))
                    {
                        SelectedNotes.Add(note);
                    }
                }
            }
        }

        private void SelectSameChannel(object? obj)
        {
            if (!SelectedNotes.Any()) return;
            var channel = SelectedNotes.First().Channel;
            foreach (var note in AllNotes)
            {
                if (note.Channel == channel)
                {
                    note.IsSelected = true;
                    if (!SelectedNotes.Contains(note))
                    {
                        SelectedNotes.Add(note);
                    }
                }
            }
        }

        private void Transpose(object? obj)
        {
            var dialog = new TransposeDialog();
            if (dialog.ShowDialog() == true)
            {
                var semitones = dialog.ViewModel.TotalSemitones;
                if (semitones == 0) return;

                var commands = new List<IUndoableCommand>();
                foreach (var note in SelectedNotes)
                {
                    var newNoteNumber = note.NoteNumber + semitones;
                    if (newNoteNumber >= 0 && newNoteNumber <= 127)
                    {
                        commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, note.DurationTicks, newNoteNumber, note.CentOffset, note.Channel, note.Velocity));
                    }
                }
                if (commands.Any())
                {
                    ExecuteCompositeNoteChange(commands);
                }
            }
        }

        private void ChangeVelocity(object? obj)
        {
            var dialog = new VelocityDialog();
            if (dialog.ShowDialog() == true)
            {
                var commands = new List<IUndoableCommand>();
                var notes = SelectedNotes.OrderBy(n => n.StartTicks).ToList();

                for (int i = 0; i < notes.Count; i++)
                {
                    var note = notes[i];
                    int newVelocity;
                    if (dialog.ViewModel.IsFixedValueMode)
                    {
                        newVelocity = dialog.ViewModel.FixedValue;
                    }
                    else
                    {
                        float fraction = (notes.Count > 1) ? (float)i / (notes.Count - 1) : 0;
                        newVelocity = (int)(dialog.ViewModel.RampStartValue + (dialog.ViewModel.RampEndValue - dialog.ViewModel.RampStartValue) * fraction);
                    }

                    newVelocity = Math.Clamp(newVelocity, 1, 127);
                    commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, newVelocity));
                }
                if (commands.Any())
                {
                    ExecuteCompositeNoteChange(commands);
                }
            }
        }

        private void ApplyLegato(object? obj)
        {
            var commands = new List<IUndoableCommand>();
            var notesByPitch = SelectedNotes.GroupBy(n => n.NoteNumber).ToDictionary(g => g.Key, g => g.OrderBy(n => n.StartTicks).ToList());

            foreach (var pitchGroup in notesByPitch.Values)
            {
                for (int i = 0; i < pitchGroup.Count - 1; i++)
                {
                    var currentNote = pitchGroup[i];
                    var nextNote = pitchGroup[i + 1];
                    var newDuration = nextNote.StartTicks - currentNote.StartTicks;
                    if (newDuration > 0 && newDuration != currentNote.DurationTicks)
                    {
                        commands.Add(new NoteChangeCommand(currentNote, currentNote.StartTicks, currentNote.DurationTicks, currentNote.NoteNumber, currentNote.CentOffset, currentNote.Channel, currentNote.Velocity, currentNote.StartTicks, newDuration, currentNote.NoteNumber, currentNote.CentOffset, currentNote.Channel, currentNote.Velocity));
                    }
                }
            }

            if (commands.Any())
            {
                ExecuteCompositeNoteChange(commands);
            }
        }

        public void ApplyStaccato(object? obj = null, double percentage = 0.5)
        {
            var commands = new List<IUndoableCommand>();
            foreach (var note in SelectedNotes)
            {
                var newDuration = (long)(note.DurationTicks * percentage);
                if (newDuration > 0 && newDuration != note.DurationTicks)
                {
                    commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, newDuration, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity));
                }
            }

            if (commands.Any())
            {
                ExecuteCompositeNoteChange(commands);
            }
        }

        private void Humanize(object? obj)
        {
            var dialog = new HumanizeDialog();
            if (dialog.ShowDialog() == true)
            {
                var commands = new List<IUndoableCommand>();
                var random = new Random();
                foreach (var note in SelectedNotes)
                {
                    var timingOffset = (long)((random.NextDouble() * 2 - 1) * dialog.ViewModel.TimingAmount);
                    var velocityOffset = (int)((random.NextDouble() * 2 - 1) * dialog.ViewModel.VelocityAmount);

                    var newStart = note.StartTicks + timingOffset;
                    var newVelocity = Math.Clamp(note.Velocity + velocityOffset, 1, 127);

                    commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, newStart, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, newVelocity));
                }
                if (commands.Any())
                {
                    ExecuteCompositeNoteChange(commands);
                }
            }
        }

        private void LoopSelection(object? obj)
        {
            if (!SelectedNotes.Any())
            {
                _playbackService.SetLoop(false, TimeSpan.Zero, TimeSpan.Zero);
                return;
            }

            var minTick = SelectedNotes.Min(n => n.StartTicks);
            var maxTick = SelectedNotes.Max(n => n.StartTicks + n.DurationTicks);
            var startTime = TicksToTime(minTick);
            var endTime = TicksToTime(maxTick);

            _playbackService.SetLoop(true, startTime, endTime);

            if (CurrentTime < startTime || CurrentTime >= endTime)
            {
                CurrentTime = startTime;
            }

            if (!IsPlaying)
            {
                PlayPauseCommand.Execute(null);
            }
        }

        private void SetPlayheadToNoteStart(object? obj)
        {
            if (!SelectedNotes.Any()) return;
            var firstNote = SelectedNotes.OrderBy(n => n.StartTicks).First();
            CurrentTime = firstNote.StartTime;
        }

        private void AddFlag(object? obj)
        {
            var time = PositionToTime(RightClickPosition.X);
            if (time < TimeSpan.Zero) time = TimeSpan.Zero;
            var newFlag = new FlagViewModel(this, time, $"Flag {Flags.Count + 1}");
            ClearSelections();
            _undoRedoService.Execute(new AddFlagCommand(this, newFlag));
            newFlag.IsSelected = true;
        }

        private void CreateFlagsFromSelection(object? obj)
        {
            if (!SelectedNotes.Any()) return;
            var commands = new List<IUndoableCommand>();
            var minTick = SelectedNotes.Min(n => n.StartTicks);
            commands.Add(new AddFlagCommand(this, new FlagViewModel(this, TicksToTime(minTick), $"Start")));
            var maxTick = SelectedNotes.Max(n => n.StartTicks + n.DurationTicks);
            commands.Add(new AddFlagCommand(this, new FlagViewModel(this, TicksToTime(maxTick), $"End")));
            _undoRedoService.Execute(new CompositeCommand(commands));
        }

        private void DeleteSelectedFlags()
        {
            var flagsToRemove = SelectedFlags.ToList();
            if (!flagsToRemove.Any()) return;
            var commands = flagsToRemove.Select(flag => new RemoveFlagCommand(this, flag)).ToList();
            _undoRedoService.Execute(new CompositeCommand(commands));
            SelectedFlags.Clear();
        }

        private void DeleteAllFlags()
        {
            if (!Flags.Any()) return;
            var commands = Flags.Select(flag => new RemoveFlagCommand(this, flag)).ToList();
            _undoRedoService.Execute(new CompositeCommand(commands));
            SelectedFlags.Clear();
        }
        private void AddFlagAtSelectionStart(object? obj = null)
        {
            if (!SelectedNotes.Any()) return;
            var minTick = SelectedNotes.Min(n => n.StartTicks);
            var newFlag = new FlagViewModel(this, TicksToTime(minTick), $"Start");
            _undoRedoService.Execute(new AddFlagCommand(this, newFlag));
        }
        private void AddFlagAtSelectionEnd(object? obj = null)
        {
            if (!SelectedNotes.Any()) return;
            var maxTick = SelectedNotes.Max(n => n.StartTicks + n.DurationTicks);
            var newFlag = new FlagViewModel(this, TicksToTime(maxTick), $"End");
            _undoRedoService.Execute(new AddFlagCommand(this, newFlag));
        }

        private void GoToNextFlag()
        {
            var orderedFlags = Flags.OrderBy(f => f.Time).ToList();
            var nextFlag = orderedFlags.FirstOrDefault(f => f.Time > CurrentTime);
            if (nextFlag == null)
            {
                nextFlag = orderedFlags.FirstOrDefault();
            }
            if (nextFlag != null)
            {
                CurrentTime = nextFlag.Time;
            }
        }

        private void GoToPreviousFlag()
        {
            var orderedFlags = Flags.OrderByDescending(f => f.Time).ToList();
            var prevFlag = orderedFlags.FirstOrDefault(f => f.Time < CurrentTime);
            if (prevFlag == null)
            {
                prevFlag = orderedFlags.FirstOrDefault();
            }
            if (prevFlag != null)
            {
                CurrentTime = prevFlag.Time;
            }
        }


        private void ZoomToSelection(object? obj)
        {
            if (!SelectedNotes.Any()) return;

            var minTick = SelectedNotes.Min(n => n.StartTicks);
            var maxTick = SelectedNotes.Max(n => n.StartTicks + n.DurationTicks);
            var minNote = SelectedNotes.Min(n => n.NoteNumber);
            var maxNote = SelectedNotes.Max(n => n.NoteNumber);

            var startTime = TicksToTime(minTick);
            var endTime = TicksToTime(maxTick);
            var duration = endTime - startTime;

            if (duration.TotalSeconds > 0)
            {
                HorizontalZoom = _viewportWidth / duration.TotalSeconds;
            }

            var noteHeight = 20.0 / KeyYScale;
            var requiredHeight = (maxNote - minNote + 1) * noteHeight * VerticalZoom;
            if (requiredHeight > 0)
            {
                VerticalZoom *= _viewportHeight / requiredHeight;
            }
        }

        internal Color GetColorForChannel(int channel)
        {
            var channelColors = MidiEditorSettings.Default.Note.ChannelColors;
            if (channelColors != null && channelColors.Count > 0)
            {
                return channelColors[(channel - 1) % channelColors.Count];
            }
            return Color.FromRgb(30, 144, 255);
        }

        private void ColorizeByChannel(object? obj)
        {
            foreach (var note in AllNotes)
            {
                note.Color = GetColorForChannel(note.Channel);
            }
        }

        private void ResetNoteColor(object? obj = null)
        {
            var defaultColor = MidiEditorSettings.Default.Note.NoteColor;
            foreach (var note in AllNotes)
            {
                note.Color = defaultColor;
            }
        }

        private void ChangeDuration(double factor)
        {
            var commands = new List<IUndoableCommand>();
            foreach (var note in SelectedNotes)
            {
                var newDuration = (long)(note.DurationTicks * factor);
                if (newDuration > 0 && newDuration != note.DurationTicks)
                {
                    commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, newDuration, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity));
                }
            }
            if (commands.Any())
            {
                ExecuteCompositeNoteChange(commands);
            }
        }

        private void InvertPitch(object? obj)
        {
            if (SelectedNotes.Count < 2) return;

            var sortedNotes = SelectedNotes.OrderBy(n => n.NoteNumber).ToList();
            var minPitch = sortedNotes.First().NoteNumber;
            var maxPitch = sortedNotes.Last().NoteNumber;
            var pivot = (minPitch + maxPitch) / 2.0;

            var commands = new List<IUndoableCommand>();
            foreach (var note in SelectedNotes)
            {
                var diff = note.NoteNumber - pivot;
                var newNoteNumber = (int)Math.Round(pivot - diff);
                newNoteNumber = Math.Clamp(newNoteNumber, 0, 127);
                commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, note.DurationTicks, newNoteNumber, note.CentOffset, note.Channel, note.Velocity));
            }
            if (commands.Any())
            {
                ExecuteCompositeNoteChange(commands);
            }
        }

        private void Retrograde(object? obj)
        {
            if (SelectedNotes.Count < 2) return;

            var orderedNotes = SelectedNotes.OrderBy(n => n.StartTicks).ToList();
            var totalDuration = orderedNotes.Last().StartTicks - orderedNotes.First().StartTicks;

            var commands = new List<IUndoableCommand>();
            var newPositions = new List<long>();

            for (int i = 0; i < orderedNotes.Count; i++)
            {
                var note = orderedNotes[i];
                var opposite = orderedNotes[orderedNotes.Count - 1 - i];
                newPositions.Add(orderedNotes.First().StartTicks + (totalDuration - (opposite.StartTicks - orderedNotes.First().StartTicks)));
            }

            for (int i = 0; i < orderedNotes.Count; i++)
            {
                var note = orderedNotes[i];
                commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, newPositions[i], note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity));
            }

            if (commands.Any())
            {
                ExecuteCompositeNoteChange(commands);
            }
        }

        private void RenameFlag(object? obj)
        {
            var flag = SelectedFlags.FirstOrDefault();
            if (flag == null) return;

            var dialog = new RenameFlagWindow(flag.Name)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() == true)
            {
                var command = new FlagChangeCommand(flag, flag.Time, flag.Name, flag.Time, dialog.ViewModel.FlagName);
                _undoRedoService.Execute(command);
            }
        }

        private async Task SaveProjectAsync(string? path = null, bool isBackup = false)
        {
            if (_midiFile == null || _originalMidiFile == null) return;
            var project = await ProjectService.CreateProjectFileAsync(_filePath, _originalMidiFile, _midiFile, AllNotes, Flags, false);
            await ProjectService.SaveProjectAsync(project, path ?? _projectPath, false);
            if (!isBackup)
            {
                StatusText = "プロジェクトを保存しました。";
            }
        }

        private async Task SaveProjectAsAsync()
        {
            if (_midiFile == null || _originalMidiFile == null) return;

            var saveFileDialog = new ProjectSaveFileDialog(Path.ChangeExtension(FileName, ".ymidi"));

            if (saveFileDialog.ShowDialog() == true && !string.IsNullOrEmpty(saveFileDialog.FilePath))
            {
                _projectPath = saveFileDialog.FilePath;
                var project = await ProjectService.CreateProjectFileAsync(_filePath, _originalMidiFile, _midiFile, AllNotes, Flags, saveFileDialog.SaveAllData);
                await ProjectService.SaveProjectAsync(project, _projectPath, saveFileDialog.CompressProject);
                StatusText = "プロジェクトを名前を付けて保存しました。";
                OnPropertyChanged(nameof(FileName));
                (SaveProjectCommand as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }

        private async Task LoadProjectAsync()
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "YukkuriMIDI Project (*.ymidi)|*.ymidi",
                Title = "プロジェクトファイルを開く"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                _projectPath = openFileDialog.FileName;
                await LoadMidiDataAsync(_projectPath);
                OnPropertyChanged(nameof(FileName));
            }
        }


        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            MidiEditorSettings.Default.Note.PropertyChanged -= Editor_Note_PropertyChanged;
            MidiEditorSettings.Default.Flag.PropertyChanged -= Editor_Flag_PropertyChanged;

            _uiUpdateTimer.Tick -= UiUpdateTimer_Tick;
            _uiUpdateTimer.Stop();

            _zoomTimer.Tick -= OnZoomTimerTick;
            _zoomTimer.Stop();

            _backupTimer.Tick -= OnBackupTimerTick;
            _backupTimer.Stop();

            _loadCts.Cancel();

            _playbackService.AudioChunkRendered -= OnAudioChunkRendered;
            _playbackService.Dispose();
            _midiInputService.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}


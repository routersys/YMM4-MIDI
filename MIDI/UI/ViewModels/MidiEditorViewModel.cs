using MIDI.Configuration.Models;
using MIDI.Core.Audio;
using MIDI.UI.Commands;
using MIDI.UI.ViewModels.MidiEditor;
using MIDI.UI.ViewModels.MidiEditor.Logic;
using MIDI.UI.ViewModels.MidiEditor.Modals;
using MIDI.UI.ViewModels.MidiEditor.Rendering;
using MIDI.UI.ViewModels.MidiEditor.Services;
using System.Windows.Controls;

namespace MIDI.UI.ViewModels
{
    public partial class MidiEditorViewModel : ViewModelBase
    {
        public PlaybackService PlaybackService { get; }
        public MidiInputService MidiInputService { get; }
        public UndoRedoService UndoRedoService { get; } = new UndoRedoService();

        public MidiFileIOService MidiFileIOService { get; }
        public PianoRollRenderer PianoRollRenderer { get; }
        public NoteEditorManager NoteEditorManager { get; }
        public ViewManager ViewManager { get; }
        public SelectionManager SelectionManager { get; }
        public PlaybackControlManager PlaybackControlManager { get; }
        public TrackEventManager TrackEventManager { get; }
        public DialogManager DialogManager { get; }
        public InputEventManager InputEventManager { get; }
        private readonly PianoRollMouseHandler _pianoRollMouseHandler;

        public MetronomeViewModel Metronome { get; }
        public SoundPeakViewModel SoundPeakViewModel { get; }
        public MultiNoteEditorViewModel MultiNoteEditor { get; }
        public KeyboardMappingViewModel KeyboardMappingViewModel { get; }
        public QuantizeSettingsViewModel QuantizeSettings { get; } = new QuantizeSettingsViewModel();

        private readonly AudioMeter _audioMeter;
        public Task LoadingTask { get; set; } = Task.CompletedTask;
        public Dock ToolbarDock { get; set; } = Dock.Top;

        public int LengthInBars { get; set; } = 16;
        public int TimeSignatureNumerator { get; set; } = 4;
        public int TimeSignatureDenominator { get; set; } = 4;
        public long TicksPerBar => MidiFile != null ? (long)(TimeSignatureNumerator * MidiFile.DeltaTicksPerQuarterNote * (4.0 / TimeSignatureDenominator)) : 480 * 4;
        public int MaxNoteNumber => TuningSystem switch { TuningSystemType.TwentyFourToneEqualTemperament => 255, _ => 127 };
        public double KeyYScale => TuningSystem switch { TuningSystemType.TwentyFourToneEqualTemperament => 2.0, _ => 1.0 };
        public double NoteHeight => 20.0 * VerticalZoom / KeyYScale;
        public System.TimeSpan MaxTime
        {
            get
            {
                if (MidiFile == null) return System.TimeSpan.FromSeconds(30);
                var totalTicks = MidiFile.Events.SelectMany(t => t).Any() ? MidiFile.Events.SelectMany(t => t).Max(e => e.AbsoluteTime) : 0;
                var minTotalTicks = TicksPerBar * LengthInBars;
                totalTicks = System.Math.Max(totalTicks, minTotalTicks);
                var tempoMap = MidiProcessor.ExtractTempoMap(MidiFile, MidiConfiguration.Default);
                return MidiProcessor.TicksToTimeSpan(totalTicks, MidiFile.DeltaTicksPerQuarterNote, tempoMap);
            }
        }

        public MidiEditorViewModel(string? filePath)
        {
            _filePath = filePath ?? "ファイルが選択されていません";

            PlaybackService = new PlaybackService(this);
            Metronome = new MetronomeViewModel(PlaybackService);
            SoundPeakViewModel = new SoundPeakViewModel();
            _audioMeter = new AudioMeter(MidiConfiguration.Default.Audio.SampleRate);
            SoundPeakViewModel.ResetLoudnessAction = () => _audioMeter.ResetLoudness();
            KeyboardMappingViewModel = new KeyboardMappingViewModel();
            MidiInputService = new MidiInputService(this, KeyboardMappingViewModel);
            MultiNoteEditor = new MultiNoteEditorViewModel(this);

            MidiFileIOService = new MidiFileIOService(this);
            PianoRollRenderer = new PianoRollRenderer(this);
            NoteEditorManager = new NoteEditorManager(this);
            ViewManager = new ViewManager(this);
            SelectionManager = new SelectionManager(this);
            PlaybackControlManager = new PlaybackControlManager(this);
            TrackEventManager = new TrackEventManager(this);
            DialogManager = new DialogManager(this);
            InputEventManager = new InputEventManager(this, KeyboardMappingViewModel);
            _pianoRollMouseHandler = new PianoRollMouseHandler(this);

            InitializeInfrastructure();
            InitializeCommands();
            InitializeEventSubscriptions();

            RefreshPianoKeys();
            LoadSoundFonts();

            if (!string.IsNullOrEmpty(filePath) && filePath != "ファイルが選択されていません")
            {
                LoadingTask = MidiFileIOService.LoadMidiDataAsync();
            }
            else
            {
                IsMidiFileLoaded = false;
            }
        }

        private void InitializeEventSubscriptions()
        {
            SelectedNotes.CollectionChanged += (s, e) =>
            {
                IsMultipleNotesSelected = SelectedNotes.Count > 1;
                SelectedNote = SelectedNotes.Count == 1 ? SelectedNotes.First() : null;
                SelectionManager.UpdateSelectionStatus();
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
                SelectionManager.UpdateSelectionStatus();
                (DeleteSelectedFlagsCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RenameFlagCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (SnapFlagToNearestTempoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            };

            UndoRedoService.StateChanged += () =>
            {
                (UndoCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (RedoCommand as RelayCommand)?.RaiseCanExecuteChanged();
            };

            MidiInputService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MidiInputService.IsMidiInputEnabled)) OnPropertyChanged(nameof(MidiInputService.IsMidiInputEnabled));
                else if (e.PropertyName == nameof(MidiInputService.SelectedMidiInputDevice)) OnPropertyChanged(nameof(MidiInputService.SelectedMidiInputDevice));
            };

            Metronome.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(MetronomeViewModel.Tempo)) UpdateTempoStatus(); };
            PlaybackService.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(PlaybackService.IsPlaying)) OnPropertyChanged(nameof(PlayButtonIcon)); };
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
            ViewManager.UpdatePianoRollSize();
        }

        public void UpdateTempoStatus() => CurrentTempoText = $"Tempo: {Metronome.Tempo:F2} BPM";

        private void LoadSoundFonts()
        {
            SoundFonts.Clear();
            var assemblyLocation = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            var sfDir = System.IO.Path.Combine(assemblyLocation, MidiConfiguration.Default.SoundFont.DefaultSoundFontDirectory);
            if (System.IO.Directory.Exists(sfDir))
            {
                foreach (var file in System.IO.Directory.GetFiles(sfDir, "*.sf2", System.IO.SearchOption.AllDirectories))
                {
                    SoundFonts.Add(System.IO.Path.GetFileName(file));
                }
            }
        }
    }
}
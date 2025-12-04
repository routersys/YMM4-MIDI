using MIDI.Configuration.Models;
using MIDI.UI.Commands;
using MIDI.UI.ViewModels.MidiEditor;
using System.Windows.Controls;
using System.Windows.Input;

namespace MIDI.UI.ViewModels
{
    public partial class MidiEditorViewModel
    {
        public ICommand NewCommand { get; private set; } = null!;
        public ICommand LoadMidiCommand { get; private set; } = null!;
        public ICommand CloseMidiCommand { get; private set; } = null!;
        public ICommand SaveCommand { get; private set; } = null!;
        public ICommand SaveAsCommand { get; private set; } = null!;
        public ICommand LoadProjectCommand { get; private set; } = null!;
        public ICommand SaveProjectCommand { get; private set; } = null!;
        public ICommand SaveProjectAsCommand { get; private set; } = null!;
        public ICommand ExportAudioCommand { get; private set; } = null!;

        public ICommand PlayPauseCommand { get; private set; } = null!;
        public ICommand StopCommand { get; private set; } = null!;
        public ICommand RewindCommand { get; private set; } = null!;

        public ICommand ZoomInCommand { get; private set; } = null!;
        public ICommand ZoomOutCommand { get; private set; } = null!;
        public ICommand ResetZoomCommand { get; private set; } = null!;
        public ICommand StartEditingHorizontalZoomCommand { get; private set; } = null!;
        public ICommand FinishEditingHorizontalZoomCommand { get; private set; } = null!;
        public ICommand StartEditingVerticalZoomCommand { get; private set; } = null!;
        public ICommand FinishEditingVerticalZoomCommand { get; private set; } = null!;
        public ICommand SliderDragStartedCommand { get; private set; } = null!;
        public ICommand SliderDragCompletedCommand { get; private set; } = null!;

        public ICommand UndoCommand { get; private set; } = null!;
        public ICommand RedoCommand { get; private set; } = null!;

        public ICommand AddNoteCommand { get; private set; } = null!;
        public ICommand DeleteNoteCommand { get; private set; } = null!;
        public ICommand DeleteSelectedNotesCommand { get; private set; } = null!;
        public ICommand CopyCommand { get; private set; } = null!;
        public ICommand PasteCommand { get; private set; } = null!;
        public ICommand SelectAllCommand { get; private set; } = null!;
        public ICommand InvertSelectionCommand { get; private set; } = null!;
        public ICommand SelectSamePitchCommand { get; private set; } = null!;
        public ICommand SelectSameChannelCommand { get; private set; } = null!;

        public ICommand OpenQuantizeSettingsCommand { get; private set; } = null!;
        public ICommand QuantizeCommand { get; private set; } = null!;
        public ICommand OpenDisplaySettingsCommand { get; private set; } = null!;
        public ICommand OpenEditorSettingsCommand { get; private set; } = null!;
        public ICommand OpenKeyboardMappingCommand { get; private set; } = null!;
        public ICommand ShowShortcutHelpCommand { get; private set; } = null!;

        public ICommand SetMidiInputModeCommand { get; private set; } = null!;
        public ICommand SetAdditionalKeyLabelCommand { get; private set; } = null!;
        public ICommand SetTuningSystemCommand { get; private set; } = null!;
        public ICommand SetToolbarDockCommand { get; private set; } = null!;

        public ICommand AddTempoEventCommand { get; private set; } = null!;
        public ICommand RemoveTempoEventCommand { get; private set; } = null!;
        public ICommand AddControlChangeEventCommand { get; private set; } = null!;
        public ICommand RemoveControlChangeEventCommand { get; private set; } = null!;
        public ICommand ResetCcSearchCommand { get; private set; } = null!;
        public ICommand ResetTempoSearchCommand { get; private set; } = null!;

        public ICommand SplitSelectedNotesCommand { get; private set; } = null!;
        public ICommand MergeSelectedNotesCommand { get; private set; } = null!;
        public ICommand TransposeCommand { get; private set; } = null!;
        public ICommand ChangeVelocityCommand { get; private set; } = null!;
        public ICommand LegatoCommand { get; private set; } = null!;
        public ICommand StaccatoCommand { get; private set; } = null!;
        public ICommand HumanizeCommand { get; private set; } = null!;
        public ICommand LoopSelectionCommand { get; private set; } = null!;
        public ICommand SetPlayheadToNoteStartCommand { get; private set; } = null!;
        public ICommand ColorizeByChannelCommand { get; private set; } = null!;
        public ICommand DoubleDurationCommand { get; private set; } = null!;
        public ICommand HalveDurationCommand { get; private set; } = null!;
        public ICommand InvertPitchCommand { get; private set; } = null!;
        public ICommand RetrogradeCommand { get; private set; } = null!;
        public ICommand ResetNoteColorCommand { get; private set; } = null!;

        public ICommand AddFlagCommand { get; private set; } = null!;
        public ICommand CreateFlagsFromSelectionCommand { get; private set; } = null!;
        public ICommand ZoomToSelectionCommand { get; private set; } = null!;
        public ICommand DeleteSelectedFlagsCommand { get; private set; } = null!;
        public ICommand DeleteAllFlagsCommand { get; private set; } = null!;
        public ICommand AddFlagAtSelectionStartCommand { get; private set; } = null!;
        public ICommand AddFlagAtSelectionEndCommand { get; private set; } = null!;
        public ICommand GoToNextFlagCommand { get; private set; } = null!;
        public ICommand GoToPreviousFlagCommand { get; private set; } = null!;
        public ICommand RenameFlagCommand { get; private set; } = null!;
        public ICommand SnapFlagToNearestTempoCommand { get; private set; } = null!;

        private void InitializeCommands()
        {
            NewCommand = new RelayCommand(_ => MidiFileIOService.CreateNewFile());
            LoadMidiCommand = new RelayCommand(_ => MidiFileIOService.LoadMidiFile());
            CloseMidiCommand = new RelayCommand(_ => MidiFileIOService.CloseMidiFile(), _ => MidiFile != null);
            SaveCommand = new RelayCommand(_ => MidiFileIOService.SaveFile(), _ => MidiFile != null);
            SaveAsCommand = new RelayCommand(_ => MidiFileIOService.SaveFileAs(), _ => MidiFile != null);
            LoadProjectCommand = new RelayCommand(async _ => await MidiFileIOService.LoadProjectAsync());
            SaveProjectCommand = new RelayCommand(async _ => await MidiFileIOService.SaveProjectAsync(), _ => !string.IsNullOrEmpty(ProjectPath));
            SaveProjectAsCommand = new RelayCommand(async _ => await MidiFileIOService.SaveProjectAsAsync(), _ => MidiFile != null);
            ExportAudioCommand = new RelayCommand(async _ => await MidiFileIOService.ExportAudioAsync(), _ => MidiFile != null);

            PlayPauseCommand = new RelayCommand(_ => PlaybackControlManager.PlayPause(), _ => MidiFile != null);
            StopCommand = new RelayCommand(_ => PlaybackControlManager.Stop(), _ => MidiFile != null);
            RewindCommand = new RelayCommand(_ => PlaybackControlManager.Rewind(), _ => MidiFile != null);

            ZoomInCommand = new RelayCommand(_ => HorizontalZoom = Math.Min(HorizontalZoom * 1.2, 1000));
            ZoomOutCommand = new RelayCommand(_ => HorizontalZoom = Math.Max(HorizontalZoom / 1.2, 10));
            ResetZoomCommand = new RelayCommand(_ => { HorizontalZoom = 100.0; VerticalZoom = 1.0; }, _ => IsZoomed);

            StartEditingHorizontalZoomCommand = new RelayCommand(_ => { TempHorizontalZoomInput = HorizontalZoom.ToString("F1"); IsEditingHorizontalZoom = true; });
            FinishEditingHorizontalZoomCommand = new RelayCommand(_ => { if (double.TryParse(TempHorizontalZoomInput, out double val)) HorizontalZoom = Math.Clamp(val, 10, 1000); IsEditingHorizontalZoom = false; });
            StartEditingVerticalZoomCommand = new RelayCommand(_ => { TempVerticalZoomInput = VerticalZoom.ToString("F1"); IsEditingVerticalZoom = true; });
            FinishEditingVerticalZoomCommand = new RelayCommand(_ => { if (double.TryParse(TempVerticalZoomInput, out double val)) VerticalZoom = Math.Clamp(val, 0.5, 5); IsEditingVerticalZoom = false; });
            SliderDragStartedCommand = new RelayCommand(_ => { IsSliderDragging = true; IsEditingHorizontalZoom = false; IsEditingVerticalZoom = false; });
            SliderDragCompletedCommand = new RelayCommand(_ => { IsSliderDragging = false; ViewManager.UpdateTimeRuler(); });

            UndoCommand = new RelayCommand(_ => UndoRedoService.Undo(), _ => UndoRedoService.CanUndo);
            RedoCommand = new RelayCommand(_ => UndoRedoService.Redo(), _ => UndoRedoService.CanRedo);

            AddNoteCommand = new RelayCommand(_ => NoteEditorManager.AddNoteAt(RightClickPosition), _ => CanAddNote && MidiFile != null);
            DeleteNoteCommand = new RelayCommand(_ => { if (SelectedNotes.Count > 0) DeleteSelectedNotesCommand.Execute(null); else if (NoteUnderCursor != null) NoteEditorManager.RemoveNote(NoteUnderCursor); }, _ => CanDeleteNote && MidiFile != null);
            DeleteSelectedNotesCommand = new RelayCommand(_ => NoteEditorManager.DeleteSelectedNotes(), _ => SelectedNotes.Count > 0 && MidiFile != null);
            CopyCommand = new RelayCommand(_ => SelectionManager.CopySelectedNotes(), _ => SelectedNotes.Count > 0 && MidiFile != null);
            PasteCommand = new RelayCommand(_ => NoteEditorManager.PasteNotes(SelectionManager.GetClipboard()), _ => SelectionManager.GetClipboard().Count > 0 && MidiFile != null);

            SelectAllCommand = new RelayCommand(_ => SelectionManager.SelectAllNotes(), _ => AllNotes.Count > 0);
            InvertSelectionCommand = new RelayCommand(_ => SelectionManager.InvertSelection(), _ => AllNotes.Count > 0);
            SelectSamePitchCommand = new RelayCommand(_ => SelectionManager.SelectSamePitch(), _ => SelectedNotes.Count > 0);
            SelectSameChannelCommand = new RelayCommand(_ => SelectionManager.SelectSameChannel(), _ => SelectedNotes.Count > 0);

            OpenQuantizeSettingsCommand = new RelayCommand(_ => IsQuantizeSettingsVisible = true);
            QuantizeCommand = new RelayCommand(_ => NoteEditorManager.QuantizeSelectedNotes(), _ => SelectedNotes.Count > 0 && MidiFile != null);
            OpenDisplaySettingsCommand = new RelayCommand(_ => ViewManager.OpenDisplaySettings());
            OpenEditorSettingsCommand = new RelayCommand(_ => DialogManager.OpenEditorSettings());
            OpenKeyboardMappingCommand = new RelayCommand(_ => DialogManager.OpenKeyboardMapping());
            ShowShortcutHelpCommand = new RelayCommand(_ => DialogManager.ShowShortcutHelp());

            SetMidiInputModeCommand = new RelayCommand(p => InputEventManager.SetMidiInputMode(p as string ?? ""));
            SetAdditionalKeyLabelCommand = new RelayCommand(p => InputEventManager.SetAdditionalKeyLabel(p as string ?? ""));
            SetTuningSystemCommand = new RelayCommand(p => InputEventManager.SetTuningSystem((TuningSystemType)p!));
            SetToolbarDockCommand = new RelayCommand(p => ToolbarDock = (Dock)p!);

            AddTempoEventCommand = new RelayCommand(_ => TrackEventManager.AddTempoEvent());
            RemoveTempoEventCommand = new RelayCommand(p => TrackEventManager.RemoveTempoEvent(p as TempoEventViewModel), p => p is TempoEventViewModel);
            AddControlChangeEventCommand = new RelayCommand(_ => TrackEventManager.AddControlChangeEvent());
            RemoveControlChangeEventCommand = new RelayCommand(p => TrackEventManager.RemoveControlChangeEvent(p as ControlChangeEventViewModel), p => p is ControlChangeEventViewModel);
            ResetCcSearchCommand = new RelayCommand(_ => CcSearchText = string.Empty);
            ResetTempoSearchCommand = new RelayCommand(_ => TempoSearchText = string.Empty);

            SplitSelectedNotesCommand = new RelayCommand(_ => NoteEditorManager.SplitSelectedNotes(RightClickPosition), _ => CanSplitNotes);
            MergeSelectedNotesCommand = new RelayCommand(_ => NoteEditorManager.MergeSelectedNotes(), _ => CanMergeNotes);
            TransposeCommand = new RelayCommand(_ => NoteEditorManager.Transpose(), _ => SelectedNotes.Count > 0);
            ChangeVelocityCommand = new RelayCommand(_ => NoteEditorManager.ChangeVelocity(), _ => SelectedNotes.Count > 0);
            LegatoCommand = new RelayCommand(_ => NoteEditorManager.ChangeDuration(1.0), _ => SelectedNotes.Count > 0);
            StaccatoCommand = new RelayCommand(_ => NoteEditorManager.ChangeDuration(0.5), _ => SelectedNotes.Count > 0);
            HumanizeCommand = new RelayCommand(_ => NoteEditorManager.Humanize(), _ => SelectedNotes.Count > 0);
            LoopSelectionCommand = new RelayCommand(_ => SelectionManager.LoopSelection(), _ => SelectedNotes.Count > 0);
            SetPlayheadToNoteStartCommand = new RelayCommand(_ => { if (SelectedNotes.Count > 0) CurrentTime = SelectedNotes[0].StartTime; }, _ => SelectedNotes.Count > 0);
            ColorizeByChannelCommand = new RelayCommand(_ => { foreach (var n in AllNotes) n.Color = GetColorForChannel(n.Channel); PianoRollRenderer.RequestRedraw(true); });
            DoubleDurationCommand = new RelayCommand(_ => NoteEditorManager.ChangeDuration(2.0), _ => SelectedNotes.Count > 0);
            HalveDurationCommand = new RelayCommand(_ => NoteEditorManager.ChangeDuration(0.5), _ => SelectedNotes.Count > 0);
            InvertPitchCommand = new RelayCommand(_ => NoteEditorManager.InvertPitch(), _ => SelectedNotes.Count > 1);
            RetrogradeCommand = new RelayCommand(_ => NoteEditorManager.Retrograde(), _ => SelectedNotes.Count > 1);
            ResetNoteColorCommand = new RelayCommand(_ => { foreach (var n in AllNotes) n.Color = EditorSettings.Note.NoteColor; PianoRollRenderer.RequestRedraw(true); });

            AddFlagCommand = new RelayCommand(_ => { var t = ViewManager.PositionToTime(RightClickPosition.X); Flags.Add(new FlagViewModel(this, t < System.TimeSpan.Zero ? System.TimeSpan.Zero : t, $"Flag {Flags.Count + 1}")); });
            CreateFlagsFromSelectionCommand = new RelayCommand(_ => { if (SelectedNotes.Count > 0) { Flags.Add(new FlagViewModel(this, ViewManager.TicksToTime(SelectedNotes[0].StartTicks), "Start")); } });
            ZoomToSelectionCommand = new RelayCommand(_ => ViewManager.ZoomToSelection(), _ => SelectedNotes.Count > 0);
            DeleteSelectedFlagsCommand = new RelayCommand(_ => { foreach (var f in new System.Collections.Generic.List<FlagViewModel>(SelectedFlags)) Flags.Remove(f); SelectedFlags.Clear(); }, _ => SelectedFlags.Count > 0);
            DeleteAllFlagsCommand = new RelayCommand(_ => Flags.Clear(), _ => Flags.Count > 0);
            AddFlagAtSelectionStartCommand = new RelayCommand(_ => CreateFlagsFromSelectionCommand.Execute(null));
            AddFlagAtSelectionEndCommand = new RelayCommand(_ => { if (SelectedNotes.Count > 0) { Flags.Add(new FlagViewModel(this, ViewManager.TicksToTime(SelectedNotes[SelectedNotes.Count - 1].StartTicks + SelectedNotes[SelectedNotes.Count - 1].DurationTicks), "End")); } });
            GoToNextFlagCommand = new RelayCommand(_ => { });
            GoToPreviousFlagCommand = new RelayCommand(_ => { });
            RenameFlagCommand = new RelayCommand(_ => SelectionManager.RenameFlag(), _ => SelectedFlags.Count == 1);
            SnapFlagToNearestTempoCommand = new RelayCommand(_ => { }, _ => SelectedFlag != null);
        }
    }
}
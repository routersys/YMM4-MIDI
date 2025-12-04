using MIDI.Configuration.Models;
using MIDI.UI.Commands;
using MIDI.UI.ViewModels.MidiEditor;
using NAudio.Midi;
using System.Windows;
using System.Windows.Media;

namespace MIDI.UI.ViewModels
{
    public partial class MidiEditorViewModel
    {
        public void RequestRedraw(bool fullRedraw) => PianoRollRenderer.RequestRedraw(fullRedraw);
        public void RequestNoteRedraw(NoteViewModel note) => PianoRollRenderer.RequestNoteRedraw(note);
        public void ThemeChanged(bool isDarkMode) => PianoRollRenderer.ThemeChanged(isDarkMode);

        public TimeSpan PositionToTime(double x) => ViewManager.PositionToTime(x);
        public long TimeToTicks(TimeSpan time) => ViewManager.TimeToTicks(time);
        public TimeSpan TicksToTime(long ticks) => ViewManager.TicksToTime(ticks);
        public TimeSpan TicksToTime(long ticks, MidiFile? file = null)
        {
            var f = file ?? MidiFile;
            if (f == null) return TimeSpan.Zero;
            var tempoMap = MidiProcessor.ExtractTempoMap(f, MidiConfiguration.Default);
            return MidiProcessor.TicksToTimeSpan(ticks, f.DeltaTicksPerQuarterNote, tempoMap);
        }
        public double GetTicksPerGrid() => ViewManager.GetTicksPerGrid();

        public void PlayPianoKey(int noteNumber)
        {
            PlaybackService.PlayPianoKey(noteNumber);
            if (PianoKeysMap.TryGetValue(noteNumber, out var keyVM)) keyVM.IsKeyboardPlaying = true;
        }

        public void StopPianoKey(int noteNumber)
        {
            PlaybackService.StopPianoKey(noteNumber);
            if (PianoKeysMap.TryGetValue(noteNumber, out var keyVM)) keyVM.IsKeyboardPlaying = false;
        }

        public void BeginScrub() => PlaybackService.BeginScrub();
        public void EndScrub() => PlaybackService.EndScrub();
        public void SetCurrentTimeFromArrowKey(TimeSpan time) => PlaybackControlManager.SetCurrentTimeFromArrowKey(time);

        public NoteViewModel? AddNoteAt(Point position) => NoteEditorManager.AddNoteAt(position);
        public void AddNoteAtCurrentTime(int noteNumber, int velocity) => NoteEditorManager.AddNoteAtCurrentTime(noteNumber, velocity);
        public void StopNoteAtCurrentTime(int noteNumber) => NoteEditorManager.StopNoteAtCurrentTime(noteNumber);
        public void AddNoteInternal(NoteViewModel noteViewModel) => NoteEditorManager.AddNoteInternal(noteViewModel);
        public void RemoveNote(NoteViewModel noteViewModel) => NoteEditorManager.RemoveNote(noteViewModel);
        public void RemoveNoteInternal(NoteViewModel noteViewModel) => NoteEditorManager.RemoveNoteInternal(noteViewModel);
        public void RemoveOverlappingNotes(long startTicks, long endTicks, int noteNumber, NoteViewModel? noteToKeep = null) => NoteEditorManager.RemoveOverlappingNotes(startTicks, endTicks, noteNumber, noteToKeep);

        public void ChangeChannelForSelectedNotes(int channel)
        {
            if (!SelectedNotes.Any()) return;
            var commands = new List<IUndoableCommand>();
            foreach (var note in SelectedNotes)
            {
                if (note.Channel == channel) continue;
                commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, channel, note.Velocity));
            }
            ExecuteCompositeNoteChange(commands);
        }

        public void ChangeDurationForSelectedNotes(long durationChangeTicks)
        {
            if (!SelectedNotes.Any() || durationChangeTicks == 0) return;
            var commands = new List<IUndoableCommand>();
            foreach (var note in SelectedNotes)
            {
                var newDuration = note.DurationTicks + durationChangeTicks;
                if (newDuration <= 0) continue;
                commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, newDuration, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity));
            }
            ExecuteCompositeNoteChange(commands);
        }

        public void ChangeVelocityForSelectedNotes(int velocityChange)
        {
            if (!SelectedNotes.Any() || velocityChange == 0) return;
            var commands = new List<IUndoableCommand>();
            foreach (var note in SelectedNotes)
            {
                var newVelocity = Math.Clamp(note.Velocity + velocityChange, 1, 127);
                if (newVelocity == note.Velocity) continue;
                commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, newVelocity));
            }
            ExecuteCompositeNoteChange(commands);
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
            ExecuteCompositeNoteChange(commands);
        }

        public void ExecuteCompositeNoteChange(List<IUndoableCommand> commands)
        {
            if (commands.Any()) UndoRedoService.Execute(new CompositeCommand(commands));
            SortMidiEvents();
            UpdatePlaybackMidiData();
        }

        public void ClearSelections(bool clearNotes = true, bool clearFlags = true) => SelectionManager.ClearSelections(clearNotes, clearFlags);

        public Color GetColorForChannel(int channel)
        {
            var channelColors = EditorSettings.Note.ChannelColors;
            if (channelColors != null && channelColors.Count > 0)
                return channelColors[(channel - 1) % channelColors.Count];
            return Color.FromRgb(30, 144, 255);
        }

        public void SaveLayout()
        {
            MidiEditorSettings.Default.SaveLayout(PianoKeysWidth.Value);
        }

        public void UpdateContextMenuState(Point position)
        {
            RightClickPosition = position;
            NoteUnderCursor = HitTestNote(position);

            if (NoteUnderCursor != null && !SelectedNotes.Contains(NoteUnderCursor))
            {
                SelectionManager.ClearSelections();
                NoteUnderCursor.IsSelected = true;
                SelectedNotes.Add(NoteUnderCursor);
                SelectedNote = NoteUnderCursor;
            }

            UpdateMergeSplitState();
            RaiseCanExecuteChanged();
        }

        public void UpdateMergeSplitState()
        {
            CanSplitNotes = SelectedNotes.Count == 1;
            CanMergeNotes = SelectedNotes.Count > 1 && SelectedNotes.All(n => n.NoteNumber == SelectedNotes.First().NoteNumber);
            (SplitSelectedNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MergeSelectedNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public NoteViewModel? HitTestNote(Point position)
        {
            if (MidiFile == null) return null;
            var time = ViewManager.PositionToTime(position.X);
            var noteNumber = MaxNoteNumber - (int)Math.Floor(position.Y / NoteHeight);
            if (noteNumber < 0 || noteNumber > MaxNoteNumber) return null;
            return AllNotes.FirstOrDefault(n => n.NoteNumber == noteNumber && n.StartTime <= time && (n.StartTime + n.Duration) >= time);
        }

        public List<NoteViewModel> HitTestNotes(Rect rect)
        {
            var minTime = ViewManager.PositionToTime(rect.Left);
            var maxTime = ViewManager.PositionToTime(rect.Right);
            var minNote = MaxNoteNumber - (int)Math.Floor(rect.Bottom / NoteHeight);
            var maxNote = MaxNoteNumber - (int)Math.Floor(rect.Top / NoteHeight);
            return AllNotes.Where(n => n.StartTime < maxTime && (n.StartTime + n.Duration) > minTime && n.NoteNumber >= minNote && n.NoteNumber <= maxNote).ToList();
        }

        public void OnPlayingNotesChanged(IEnumerable<int> activeNoteNumbers)
        {
            var activeNotesSet = new HashSet<int>(activeNoteNumbers);
            var keysToTurnOff = _currentlyLitKeys.Except(activeNotesSet).ToList();
            var keysToTurnOn = activeNotesSet.Except(_currentlyLitKeys).ToList();

            foreach (var noteNumber in keysToTurnOff)
                if (PianoKeysMap.TryGetValue(noteNumber, out var keyVM)) keyVM.IsPlaying = false;

            foreach (var noteNumber in keysToTurnOn)
                if (PianoKeysMap.TryGetValue(noteNumber, out var keyVM)) keyVM.IsPlaying = true;

            _currentlyLitKeys = activeNotesSet;
        }
        private HashSet<int> _currentlyLitKeys = new HashSet<int>();

        public void OnFlagSelectionChanged(FlagViewModel flag, bool isSelected)
        {
            if (isSelected)
            {
                if (!SelectedFlags.Contains(flag)) SelectedFlags.Add(flag);
            }
            else
            {
                SelectedFlags.Remove(flag);
            }
            SelectionManager.UpdateSelectionStatus();
            (DeleteSelectedFlagsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (RenameFlagCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SnapFlagToNearestTempoCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }

        public void SortMidiEvents()
        {
            if (MidiFile == null) return;
            foreach (var track in MidiFile.Events)
            {
                var events = track.OrderBy(e => e.AbsoluteTime).ToList();
                track.Clear();
                foreach (var e in events) track.Add(e);
            }
        }
        public void UpdatePlaybackMidiData()
        {
            if (MidiFile == null) return;
            string tempFile = string.Empty;
            try
            {
                tempFile = System.IO.Path.GetTempFileName();
                NAudio.Midi.MidiFile.Export(tempFile, MidiFile.Events);
                using (var stream = new System.IO.MemoryStream(System.IO.File.ReadAllBytes(tempFile)))
                {
                    PlaybackService.LoadMidiData(stream);
                }
            }
            catch (Exception ex) { MessageBox.Show($"MIDIデータの更新に失敗しました: {ex.Message}"); }
            finally { if (System.IO.File.Exists(tempFile)) try { System.IO.File.Delete(tempFile); } catch { } }
        }

        public void RaiseNotesLoaded() => NotesLoaded?.Invoke();
        public void RaiseCanExecuteChanged()
        {
            (AddNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteNoteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (DeleteSelectedNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (QuantizeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CopyCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (PasteCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SplitSelectedNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (MergeSelectedNotesCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        public event Action? NotesLoaded;
    }
}
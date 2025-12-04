using MIDI.Configuration.Models;
using System.Windows;
using System.Windows.Input;

namespace MIDI.UI.ViewModels.MidiEditor.Logic
{
    public class PianoRollInputHandler
    {
        public enum DragMode { None, Move, ResizeLeft, ResizeRight, Scrub, Select, DragFlag }
        public event Action<NoteViewModel?, bool>? NoteSelected;

        private readonly MidiEditorViewModel _vm;
        private Point _dragStartPoint;
        private readonly Dictionary<NoteViewModel, (long startTicks, TimeSpan startTime, long durationTicks, TimeSpan durationTime, int noteNumber, int centOffset, int channel, int velocity)> _dragStartNoteData = new();
        private readonly Dictionary<FlagViewModel, (TimeSpan time, string name)> _dragStartFlagData = new();
        public DragMode CurrentDragMode { get; internal set; } = DragMode.None;
        private const double ResizeHandleWidth = 8.0;

        public PianoRollInputHandler(MidiEditorViewModel vm)
        {
            _vm = vm;
        }

        public DragMode GetDragModeForPosition(Point position, NoteViewModel? note)
        {
            if (note != null)
            {
                double noteX = _vm.ViewManager.TicksToTime(note.StartTicks).TotalSeconds * _vm.HorizontalZoom;
                double noteWidth = _vm.ViewManager.TicksToTime(note.DurationTicks).TotalSeconds * _vm.HorizontalZoom;

                if (position.X <= noteX + ResizeHandleWidth) return DragMode.ResizeLeft;
                if (position.X >= noteX + noteWidth - ResizeHandleWidth) return DragMode.ResizeRight;
                return DragMode.Move;
            }

            var flagUnderCursor = _vm.Flags.FirstOrDefault(f => Math.Abs(position.X - f.X) < 10);
            if (flagUnderCursor != null)
            {
                return DragMode.DragFlag;
            }

            return DragMode.Select;
        }


        public void OnPianoRollMouseDown(Point position, MouseButtonEventArgs e)
        {
            if (_vm.MidiFile == null) return;

            var noteUnderCursor = _vm.HitTestNote(position);
            var flagUnderCursor = _vm.Flags.FirstOrDefault(f => Math.Abs(position.X - f.X) < 10);

            bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            _dragStartPoint = position;

            if (e.ClickCount == 2)
            {
                if (noteUnderCursor != null) _vm.NoteEditorManager.RemoveNote(noteUnderCursor);
                else _vm.NoteEditorManager.AddNoteAt(position);
                e.Handled = true;
                return;
            }

            if (flagUnderCursor != null)
            {
                CurrentDragMode = DragMode.DragFlag;
                _vm.SelectionManager.ClearSelections(clearNotes: true, clearFlags: false);
                if (!flagUnderCursor.IsSelected)
                {
                    if (!isCtrlPressed)
                    {
                        foreach (var flag in _vm.SelectedFlags.ToList()) flag.IsSelected = false;
                    }
                    flagUnderCursor.IsSelected = true;
                }
                else if (isCtrlPressed)
                {
                    flagUnderCursor.IsSelected = false;
                }

                _dragStartFlagData.Clear();
                foreach (var flag in _vm.SelectedFlags)
                {
                    _dragStartFlagData[flag] = (flag.Time, flag.Name);
                }
            }
            else if (noteUnderCursor != null)
            {
                _vm.SelectionManager.ClearSelections(clearNotes: false, clearFlags: true);
                if (!noteUnderCursor.IsSelected)
                {
                    if (!isCtrlPressed)
                    {
                        foreach (var note in _vm.SelectedNotes.ToList()) note.IsSelected = false;
                        _vm.SelectedNotes.Clear();
                    }
                    NoteSelected?.Invoke(noteUnderCursor, isCtrlPressed);
                }
                else if (isCtrlPressed)
                {
                    NoteSelected?.Invoke(noteUnderCursor, isCtrlPressed);
                }

                CurrentDragMode = GetDragModeForPosition(position, noteUnderCursor);
                _dragStartNoteData.Clear();
                foreach (var note in _vm.SelectedNotes)
                {
                    var startTime = _vm.ViewManager.TicksToTime(note.StartTicks);
                    var durationTime = _vm.ViewManager.TicksToTime(note.StartTicks + note.DurationTicks) - startTime;
                    _dragStartNoteData[note] = (note.StartTicks, startTime, note.DurationTicks, durationTime, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity);
                    note.IsEditing = true;
                }
                if (_vm.SelectedNotes.Count > 1 && (CurrentDragMode == DragMode.ResizeLeft || CurrentDragMode == DragMode.ResizeRight))
                {
                    CurrentDragMode = DragMode.Move;
                }
            }
            else
            {
                if (MidiEditorSettings.Default.Input.PianoRollMouseMode == PianoRollMouseMode.Editor && !isShiftPressed && !isCtrlPressed)
                {
                    var newNote = _vm.NoteEditorManager.AddNoteAt(position);
                    if (newNote != null)
                    {
                        _vm.SelectionManager.ClearSelections();
                        newNote.IsSelected = true;
                        _vm.SelectedNotes.Add(newNote);
                        _vm.SelectedNote = newNote;

                        CurrentDragMode = DragMode.ResizeRight;
                        _dragStartNoteData.Clear();
                        var startTime = _vm.ViewManager.TicksToTime(newNote.StartTicks);
                        var durationTime = _vm.ViewManager.TicksToTime(newNote.StartTicks + newNote.DurationTicks) - startTime;
                        _dragStartNoteData[newNote] = (newNote.StartTicks, startTime, newNote.DurationTicks, durationTime, newNote.NoteNumber, newNote.CentOffset, newNote.Channel, newNote.Velocity);
                        newNote.IsEditing = true;
                    }
                }
                else if (isShiftPressed)
                {
                    if (!isCtrlPressed) _vm.SelectionManager.ClearSelections();
                    CurrentDragMode = DragMode.Select;
                    _vm.SelectionRectangle = new Rect(_dragStartPoint, new Size(0, 0));
                }
                else
                {
                    if (!isCtrlPressed) _vm.SelectionManager.ClearSelections();
                    CurrentDragMode = DragMode.Scrub;
                    if (_vm.IsPlaying) _vm.PlayPauseCommand.Execute(null);
                    _vm.PlaybackService.BeginScrub();
                    _vm.CurrentTime = _vm.ViewManager.PositionToTime(position.X);
                }
            }
            e.Handled = true;
        }

        public void OnPianoRollMouseMove(Point position, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _vm.MidiFile == null) return;

            var dragStartTimeOnScreen = _vm.ViewManager.PositionToTime(_dragStartPoint.X);
            var dragCurrentTimeOnScreen = _vm.ViewManager.PositionToTime(position.X);
            var timeDelta = dragCurrentTimeOnScreen - dragStartTimeOnScreen;

            if (CurrentDragMode == DragMode.DragFlag)
            {
                foreach (var flag in _vm.SelectedFlags)
                {
                    var (originalTime, _) = _dragStartFlagData[flag];
                    var newTime = originalTime + timeDelta;
                    if (newTime < TimeSpan.Zero) newTime = TimeSpan.Zero;
                    flag.Time = newTime;
                }
                return;
            }

            if (CurrentDragMode == DragMode.Scrub)
            {
                _vm.CurrentTime = _vm.ViewManager.PositionToTime(position.X);
                return;
            }

            if (CurrentDragMode == DragMode.Select)
            {
                var rect = new Rect(_dragStartPoint, position);
                _vm.SelectionRectangle = rect;
                return;
            }

            if (_vm.SelectedNotes.Any() && (CurrentDragMode == DragMode.Move || CurrentDragMode == DragMode.ResizeLeft || CurrentDragMode == DragMode.ResizeRight))
            {
                var deltaX = position.X - _dragStartPoint.X;
                var deltaY = position.Y - _dragStartPoint.Y;

                var timeAtZero = _vm.ViewManager.PositionToTime(0);
                var timeAtDeltaX = _vm.ViewManager.PositionToTime(deltaX);
                var tickDelta = _vm.ViewManager.TimeToTicks(timeAtDeltaX - timeAtZero);

                bool isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);
                var noteToResize = _vm.SelectedNotes.FirstOrDefault();

                switch (CurrentDragMode)
                {
                    case DragMode.Move:
                        foreach (var note in _vm.SelectedNotes)
                        {
                            var (originalStartTicks, _, originalDurationTicks, _, startNoteNumber, startCentOffset, _, _) = _dragStartNoteData[note];
                            var newStartTicks = originalStartTicks + tickDelta;

                            if (_vm.EditorSettings.Grid.EnableSnapToGrid && !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                            {
                                var ticksPerGrid = _vm.ViewManager.GetTicksPerGrid();
                                if (ticksPerGrid > 0) newStartTicks = (long)(Math.Round((double)newStartTicks / ticksPerGrid) * ticksPerGrid);
                            }

                            if (isAltPressed && _vm.TuningSystem == TuningSystemType.Microtonal)
                            {
                                int centDelta = (int)Math.Round(-deltaY / (20.0 * _vm.VerticalZoom / _vm.KeyYScale) * 100.0);
                                note.CentOffset = Math.Clamp(startCentOffset + centDelta, -50, 50);
                            }
                            else
                            {
                                var noteDelta = -(int)Math.Round(deltaY / (20.0 * _vm.VerticalZoom / _vm.KeyYScale));
                                int newNoteNumber = Math.Clamp(startNoteNumber + noteDelta, 0, _vm.MaxNoteNumber);
                                note.NoteNumber = newNoteNumber;
                            }
                            note.UpdateNote(newStartTicks, originalDurationTicks);
                        }
                        break;
                    case DragMode.ResizeRight:
                        if (noteToResize != null)
                        {
                            var (startTicks, _, durationTicks, _, _, _, _, _) = _dragStartNoteData[noteToResize];
                            var newEndTicks = startTicks + durationTicks + tickDelta;

                            if (_vm.EditorSettings.Grid.EnableSnapToGrid && !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                            {
                                var ticksPerGrid = _vm.ViewManager.GetTicksPerGrid();
                                if (ticksPerGrid > 0) newEndTicks = (long)(Math.Round((double)newEndTicks / ticksPerGrid) * ticksPerGrid);
                            }
                            var newDurationTicks = Math.Max(1, newEndTicks - startTicks);
                            noteToResize.UpdateNote(startTicks, newDurationTicks);
                        }
                        break;
                    case DragMode.ResizeLeft:
                        if (noteToResize != null)
                        {
                            var (startTicks, _, durationTicks, _, _, _, _, _) = _dragStartNoteData[noteToResize];
                            var newStartTicks = startTicks + tickDelta;

                            if (_vm.EditorSettings.Grid.EnableSnapToGrid && !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                            {
                                var ticksPerGrid = _vm.ViewManager.GetTicksPerGrid();
                                if (ticksPerGrid > 0) newStartTicks = (long)(Math.Round((double)newStartTicks / ticksPerGrid) * ticksPerGrid);
                            }

                            var endTicks = startTicks + durationTicks;
                            var newDurationTicks = Math.Max(1, endTicks - newStartTicks);
                            if (newDurationTicks == 1) newStartTicks = endTicks - 1;
                            noteToResize.UpdateNote(newStartTicks, newDurationTicks);
                        }
                        break;
                }
            }
        }

        public void OnPianoRollMouseUp(MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Right)
            {
                _vm.UpdateContextMenuState(e.GetPosition(e.Source as IInputElement));
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                var originalDragMode = CurrentDragMode;
                CurrentDragMode = DragMode.None;

                if (originalDragMode == DragMode.DragFlag)
                {
                    var commands = new List<IUndoableCommand>();
                    foreach (var flag in _vm.SelectedFlags)
                    {
                        if (_dragStartFlagData.TryGetValue(flag, out var originalData))
                        {
                            commands.Add(new FlagChangeCommand(flag, originalData.time, originalData.name, flag.Time, flag.Name));
                        }
                    }
                    _vm.ExecuteCompositeNoteChange(commands);
                    _dragStartFlagData.Clear();
                }
                else if (originalDragMode == DragMode.Scrub)
                {
                    _vm.PlaybackService.EndScrub();
                }
                else if (originalDragMode == DragMode.Select)
                {
                    var selectionRect = _vm.SelectionRectangle;
                    _vm.SelectionRectangle = new Rect();
                    var notesInRect = _vm.HitTestNotes(selectionRect);
                    bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                    if (!isCtrlPressed)
                    {
                        foreach (var note in _vm.SelectedNotes.ToList())
                        {
                            if (!notesInRect.Contains(note))
                            {
                                note.IsSelected = false;
                                _vm.SelectedNotes.Remove(note);
                            }
                        }
                    }
                    foreach (var note in notesInRect)
                    {
                        if (!note.IsSelected)
                        {
                            note.IsSelected = true;
                            if (!_vm.SelectedNotes.Contains(note)) _vm.SelectedNotes.Add(note);
                        }
                    }
                    _vm.SelectedNote = _vm.SelectedNotes.FirstOrDefault();
                }
                else if (originalDragMode == DragMode.Move || originalDragMode == DragMode.ResizeLeft || originalDragMode == DragMode.ResizeRight)
                {
                    if (_vm.SelectedNotes.Any())
                    {
                        var commands = new List<IUndoableCommand>();
                        var notesToFinalize = new List<NoteViewModel>();
                        foreach (var note in _vm.SelectedNotes)
                        {
                            if (_dragStartNoteData.TryGetValue(note, out var originalData))
                            {
                                commands.Add(new NoteChangeCommand(note, originalData.startTicks, originalData.durationTicks, originalData.noteNumber, originalData.centOffset, originalData.channel, originalData.velocity, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity));
                            }
                            notesToFinalize.Add(note);
                        }
                        _vm.ExecuteCompositeNoteChange(commands);
                        foreach (var note in notesToFinalize) note.IsEditing = false;
                    }
                }
                _dragStartNoteData.Clear();
            }
        }

        public void OnTimeBarMouseDown(Point position)
        {
            if (_vm.IsPlaying) _vm.PlayPauseCommand.Execute(null);
            CurrentDragMode = DragMode.Scrub;
            _vm.PlaybackService.BeginScrub();
            OnTimeBarMouseMove(position);
        }

        public void OnTimeBarMouseMove(Point position)
        {
            if (CurrentDragMode == DragMode.Scrub)
            {
                _vm.CurrentTime = _vm.ViewManager.PositionToTime(position.X);
            }
        }

        public void OnTimeBarMouseUp()
        {
            if (CurrentDragMode == DragMode.Scrub)
            {
                _vm.PlaybackService.EndScrub();
                CurrentDragMode = DragMode.None;
            }
        }
    }
}
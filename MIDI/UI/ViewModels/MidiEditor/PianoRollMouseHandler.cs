using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using NAudioMidi = NAudio.Midi;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class PianoRollMouseHandler
    {
        public enum DragMode { None, Move, ResizeLeft, ResizeRight, Scrub, Select, DragFlag }
        public event Action<NoteViewModel?, bool>? NoteSelected;

        private readonly MidiEditorViewModel _viewModel;
        private readonly ObservableCollection<NoteViewModel> _notes;
        private Point _dragStartPoint;
        private readonly Dictionary<NoteViewModel, (long startTicks, TimeSpan startTime, long durationTicks, TimeSpan durationTime, int noteNumber, int centOffset, int channel, int velocity)> _dragStartNoteData = new();
        private readonly Dictionary<FlagViewModel, (TimeSpan time, string name)> _dragStartFlagData = new();
        public DragMode CurrentDragMode { get; internal set; } = DragMode.None;
        private const double ResizeHandleWidth = 8.0;

        public PianoRollMouseHandler(MidiEditorViewModel viewModel, ObservableCollection<NoteViewModel> notes)
        {
            _viewModel = viewModel;
            _notes = notes;
        }

        private long PositionToTicks(double x)
        {
            if (_viewModel.MidiFile == null) return 0;
            var time = _viewModel.PositionToTime(x);
            var ticks = _viewModel.TimeToTicks(time);
            if (_viewModel.EditorSettings.Grid.EnableSnapToGrid && !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
            {
                var ticksPerGrid = _viewModel.GetTicksPerGrid();
                if (ticksPerGrid > 0)
                {
                    return (long)(Math.Round(ticks / ticksPerGrid) * ticksPerGrid);
                }
            }
            return ticks;
        }

        public DragMode GetDragModeForPosition(Point position, NoteViewModel? note)
        {
            if (note != null)
            {
                double noteX = _viewModel.TicksToTime(note.StartTicks).TotalSeconds * _viewModel.HorizontalZoom;
                double noteWidth = _viewModel.TicksToTime(note.DurationTicks).TotalSeconds * _viewModel.HorizontalZoom;

                if (position.X <= noteX + ResizeHandleWidth) return DragMode.ResizeLeft;
                if (position.X >= noteX + noteWidth - ResizeHandleWidth) return DragMode.ResizeRight;
                return DragMode.Move;
            }

            var flagUnderCursor = _viewModel.Flags.FirstOrDefault(f => Math.Abs(position.X - f.X) < 10);
            if (flagUnderCursor != null)
            {
                return DragMode.DragFlag;
            }

            return DragMode.Select;
        }


        public void OnPianoRollMouseDown(Point position, MouseButtonEventArgs e, NAudioMidi.MidiFile midiFile)
        {
            if (_viewModel.MidiFile == null) return;

            var noteUnderCursor = _viewModel.HitTestNote(position);

            var flagUnderCursor = _viewModel.Flags.FirstOrDefault(f => Math.Abs(position.X - f.X) < 10);

            bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
            bool isShiftPressed = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);

            _dragStartPoint = position;

            if (e.ClickCount == 2)
            {
                if (noteUnderCursor != null)
                {
                    _viewModel.RemoveNote(noteUnderCursor);
                }
                else
                {
                    _viewModel.AddNoteAt(position);
                }
                e.Handled = true;
                return;
            }

            if (flagUnderCursor != null)
            {
                CurrentDragMode = DragMode.DragFlag;
                _viewModel.ClearSelections(clearNotes: true, clearFlags: false);
                if (!flagUnderCursor.IsSelected)
                {
                    if (!isCtrlPressed)
                    {
                        foreach (var flag in _viewModel.SelectedFlags.ToList()) flag.IsSelected = false;
                    }
                    flagUnderCursor.IsSelected = true;
                }
                else if (isCtrlPressed)
                {
                    flagUnderCursor.IsSelected = false;
                }

                _dragStartFlagData.Clear();
                foreach (var flag in _viewModel.SelectedFlags)
                {
                    _dragStartFlagData[flag] = (flag.Time, flag.Name);
                }
            }
            else if (noteUnderCursor != null)
            {
                _viewModel.ClearSelections(clearNotes: false, clearFlags: true);
                if (!noteUnderCursor.IsSelected)
                {
                    if (!isCtrlPressed)
                    {
                        foreach (var note in _viewModel.SelectedNotes.ToList())
                        {
                            note.IsSelected = false;
                        }
                        _viewModel.SelectedNotes.Clear();
                    }
                    NoteSelected?.Invoke(noteUnderCursor, isCtrlPressed);
                }
                else if (isCtrlPressed)
                {
                    NoteSelected?.Invoke(noteUnderCursor, isCtrlPressed);
                }

                CurrentDragMode = GetDragModeForPosition(position, noteUnderCursor);
                _dragStartNoteData.Clear();
                foreach (var note in _viewModel.SelectedNotes)
                {
                    var startTime = _viewModel.TicksToTime(note.StartTicks);
                    var durationTime = _viewModel.TicksToTime(note.StartTicks + note.DurationTicks) - startTime;
                    _dragStartNoteData[note] = (note.StartTicks, startTime, note.DurationTicks, durationTime, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity);
                    note.IsEditing = true;
                }
                if (_viewModel.SelectedNotes.Count > 1 && (CurrentDragMode == DragMode.ResizeLeft || CurrentDragMode == DragMode.ResizeRight))
                {
                    CurrentDragMode = DragMode.Move;
                }
            }
            else
            {
                if (isShiftPressed)
                {
                    if (!isCtrlPressed)
                    {
                        _viewModel.ClearSelections();
                    }
                    CurrentDragMode = DragMode.Select;
                    _viewModel.SelectionRectangle = new Rect(_dragStartPoint, new Size(0, 0));
                }
                else
                {
                    if (!isCtrlPressed)
                    {
                        _viewModel.ClearSelections();
                    }
                    CurrentDragMode = DragMode.Scrub;
                    if (_viewModel.IsPlaying)
                    {
                        _viewModel.PlayPauseCommand.Execute(null);
                    }
                    _viewModel.BeginScrub();
                    _viewModel.CurrentTime = _viewModel.PositionToTime(position.X);
                }
            }
            e.Handled = true;
        }

        public void OnPianoRollMouseMove(Point position, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _viewModel.MidiFile == null) return;

            var dragStartTimeOnScreen = _viewModel.PositionToTime(_dragStartPoint.X);
            var dragCurrentTimeOnScreen = _viewModel.PositionToTime(position.X);
            var timeDelta = dragCurrentTimeOnScreen - dragStartTimeOnScreen;

            if (CurrentDragMode == DragMode.DragFlag)
            {
                foreach (var flag in _viewModel.SelectedFlags)
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
                _viewModel.CurrentTime = _viewModel.PositionToTime(position.X);
                return;
            }

            if (CurrentDragMode == DragMode.Select)
            {
                var rect = new Rect(_dragStartPoint, position);
                _viewModel.SelectionRectangle = rect;
                return;
            }

            if (_viewModel.SelectedNotes.Any() && (CurrentDragMode == DragMode.Move || CurrentDragMode == DragMode.ResizeLeft || CurrentDragMode == DragMode.ResizeRight))
            {
                var deltaX = position.X - _dragStartPoint.X;
                var deltaY = position.Y - _dragStartPoint.Y;

                var timeAtZero = _viewModel.PositionToTime(0);
                var timeAtDeltaX = _viewModel.PositionToTime(deltaX);
                var tickDelta = _viewModel.TimeToTicks(timeAtDeltaX - timeAtZero);


                bool isAltPressed = Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

                var noteToResize = _viewModel.SelectedNotes.FirstOrDefault();

                switch (CurrentDragMode)
                {
                    case DragMode.Move:
                        foreach (var note in _viewModel.SelectedNotes)
                        {
                            var (originalStartTicks, _, originalDurationTicks, _, startNoteNumber, startCentOffset, _, _) = _dragStartNoteData[note];
                            var newStartTicks = originalStartTicks + tickDelta;

                            if (_viewModel.EditorSettings.Grid.EnableSnapToGrid && !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                            {
                                var ticksPerGrid = _viewModel.GetTicksPerGrid();
                                if (ticksPerGrid > 0)
                                {
                                    newStartTicks = (long)(Math.Round((double)newStartTicks / ticksPerGrid) * ticksPerGrid);
                                }
                            }

                            if (isAltPressed && _viewModel.TuningSystem == Configuration.Models.TuningSystemType.Microtonal)
                            {
                                int centDelta = (int)Math.Round(-deltaY / (20.0 * _viewModel.VerticalZoom / _viewModel.KeyYScale) * 100.0);
                                note.CentOffset = Math.Clamp(startCentOffset + centDelta, -50, 50);
                            }
                            else
                            {
                                var noteDelta = -(int)Math.Round(deltaY / (20.0 * _viewModel.VerticalZoom / _viewModel.KeyYScale));
                                int newNoteNumber = Math.Clamp(startNoteNumber + noteDelta, 0, _viewModel.MaxNoteNumber);
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

                            if (_viewModel.EditorSettings.Grid.EnableSnapToGrid && !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                            {
                                var ticksPerGrid = _viewModel.GetTicksPerGrid();
                                if (ticksPerGrid > 0)
                                {
                                    newEndTicks = (long)(Math.Round((double)newEndTicks / ticksPerGrid) * ticksPerGrid);
                                }
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

                            if (_viewModel.EditorSettings.Grid.EnableSnapToGrid && !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                            {
                                var ticksPerGrid = _viewModel.GetTicksPerGrid();
                                if (ticksPerGrid > 0)
                                {
                                    newStartTicks = (long)(Math.Round((double)newStartTicks / ticksPerGrid) * ticksPerGrid);
                                }
                            }

                            var endTicks = startTicks + durationTicks;
                            var newDurationTicks = Math.Max(1, endTicks - newStartTicks);
                            if (newDurationTicks == 1)
                            {
                                newStartTicks = endTicks - 1;
                            }
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
                _viewModel.UpdateContextMenuState(e.GetPosition(e.Source as IInputElement));
                return;
            }

            if (e.ChangedButton == MouseButton.Left)
            {
                var originalDragMode = CurrentDragMode;
                CurrentDragMode = DragMode.None;

                if (originalDragMode == DragMode.DragFlag)
                {
                    var commands = new List<IUndoableCommand>();
                    foreach (var flag in _viewModel.SelectedFlags)
                    {
                        if (_dragStartFlagData.TryGetValue(flag, out var originalData))
                        {
                            commands.Add(new FlagChangeCommand(flag, originalData.time, originalData.name, flag.Time, flag.Name));
                        }
                    }
                    _viewModel.ExecuteCompositeNoteChange(commands);
                    _dragStartFlagData.Clear();
                }
                else if (originalDragMode == DragMode.Scrub)
                {
                    _viewModel.EndScrub();
                }
                else if (originalDragMode == DragMode.Select)
                {
                    var selectionRect = _viewModel.SelectionRectangle;
                    _viewModel.SelectionRectangle = new Rect();

                    var notesInRect = _viewModel.HitTestNotes(selectionRect);

                    bool isCtrlPressed = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
                    if (!isCtrlPressed)
                    {
                        foreach (var note in _viewModel.SelectedNotes.ToList())
                        {
                            if (!notesInRect.Contains(note))
                            {
                                note.IsSelected = false;
                                _viewModel.SelectedNotes.Remove(note);
                            }
                        }
                    }

                    foreach (var note in notesInRect)
                    {
                        if (!note.IsSelected)
                        {
                            note.IsSelected = true;
                            if (!_viewModel.SelectedNotes.Contains(note))
                            {
                                _viewModel.SelectedNotes.Add(note);
                            }
                        }
                    }
                    _viewModel.SelectedNote = _viewModel.SelectedNotes.FirstOrDefault();
                }
                else if (originalDragMode == DragMode.Move || originalDragMode == DragMode.ResizeLeft || originalDragMode == DragMode.ResizeRight)
                {
                    if (_viewModel.SelectedNotes.Any())
                    {
                        var commands = new List<IUndoableCommand>();
                        var notesToFinalize = new List<NoteViewModel>();

                        foreach (var note in _viewModel.SelectedNotes)
                        {
                            if (_dragStartNoteData.TryGetValue(note, out var originalData))
                            {
                                commands.Add(new NoteChangeCommand(note, originalData.startTicks, originalData.durationTicks, originalData.noteNumber, originalData.centOffset, originalData.channel, originalData.velocity, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity));
                            }
                            notesToFinalize.Add(note);
                        }
                        _viewModel.ExecuteCompositeNoteChange(commands);

                        foreach (var note in notesToFinalize)
                        {
                            note.IsEditing = false;
                        }
                    }
                }

                _dragStartNoteData.Clear();
            }
        }

        public void OnTimeBarMouseDown(Point position)
        {
            if (_viewModel.IsPlaying)
            {
                _viewModel.PlayPauseCommand.Execute(null);
            }
            CurrentDragMode = DragMode.Scrub;
            _viewModel.BeginScrub();
            OnTimeBarMouseMove(position);
        }

        public void OnTimeBarMouseMove(Point position)
        {
            if (CurrentDragMode == DragMode.Scrub)
            {
                _viewModel.CurrentTime = _viewModel.PositionToTime(position.X);
            }
        }

        public void OnTimeBarMouseUp()
        {
            if (CurrentDragMode == DragMode.Scrub)
            {
                _viewModel.EndScrub();
                CurrentDragMode = DragMode.None;
            }
        }
    }
}
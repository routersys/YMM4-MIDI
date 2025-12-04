using MIDI.Configuration.Models;
using MIDI.UI.Views.MidiEditor.Modals;
using System.Windows;
using System.Windows.Input;
using NAudioMidi = NAudio.Midi;

namespace MIDI.UI.ViewModels.MidiEditor.Logic
{
    public class NoteEditorManager
    {
        private readonly MidiEditorViewModel _vm;
        private readonly Dictionary<int, NoteViewModel> _recordingNotes = new Dictionary<int, NoteViewModel>();

        public NoteEditorManager(MidiEditorViewModel vm)
        {
            _vm = vm;
        }

        public NoteViewModel? AddNoteAt(Point position)
        {
            if (_vm.MidiFile == null) return null;

            long ticks;
            if (_vm.EditorSettings.Grid.EnableSnapToGrid && !(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
            {
                var ticksPerGrid = _vm.ViewManager.GetTicksPerGrid();
                var time = _vm.ViewManager.PositionToTime(position.X);
                var rawTicks = _vm.ViewManager.TimeToTicks(time);
                ticks = (long)(Math.Round(rawTicks / ticksPerGrid) * ticksPerGrid);
            }
            else
            {
                var time = _vm.ViewManager.PositionToTime(position.X);
                ticks = _vm.ViewManager.TimeToTicks(time);
            }

            var noteNumber = _vm.MaxNoteNumber - (int)Math.Floor(position.Y / _vm.NoteHeight);
            if (noteNumber < 0 || noteNumber > _vm.MaxNoteNumber) return null;

            var durationTicks = (long)_vm.ViewManager.GetTicksPerGrid();
            if (durationTicks <= 0) durationTicks = _vm.MidiFile.DeltaTicksPerQuarterNote / 4;

            RemoveOverlappingNotes(ticks, ticks + durationTicks, noteNumber);

            var velocity = 100;
            var noteOn = new NAudioMidi.NoteOnEvent(ticks, 1, noteNumber, velocity, (int)durationTicks);
            var noteOff = new NAudioMidi.NoteEvent(ticks + noteOn.NoteLength, 1, NAudioMidi.MidiCommandCode.NoteOff, noteNumber, 0);
            noteOn.OffEvent = noteOff;

            var tempoMap = MidiProcessor.ExtractTempoMap(_vm.MidiFile, MidiConfiguration.Default);
            var noteViewModel = new NoteViewModel(noteOn, _vm.MidiFile.DeltaTicksPerQuarterNote, tempoMap, _vm);

            _vm.UndoRedoService.Execute(new AddNoteCommand(_vm, noteViewModel));
            return noteViewModel;
        }

        public void AddNoteAtCurrentTime(int noteNumber, int velocity)
        {
            if (_vm.MidiFile == null || !_vm.IsPlaying || _recordingNotes.ContainsKey(noteNumber)) return;

            var startTicks = _vm.ViewManager.TimeToTicks(_vm.CurrentTime);
            RemoveOverlappingNotes(startTicks, startTicks + 1, noteNumber);

            var tempoMap = MidiProcessor.ExtractTempoMap(_vm.MidiFile, MidiConfiguration.Default);
            var noteOn = new NAudioMidi.NoteOnEvent(startTicks, 1, noteNumber, velocity, 0);
            var noteViewModel = new NoteViewModel(noteOn, _vm.MidiFile.DeltaTicksPerQuarterNote, tempoMap, _vm);

            _recordingNotes[noteNumber] = noteViewModel;

            Application.Current.Dispatcher.Invoke(() =>
            {
                _vm.AllNotes.Add(noteViewModel);
                _vm.RequestNoteRedraw(noteViewModel);
            });
        }

        public void StopNoteAtCurrentTime(int noteNumber)
        {
            if (_vm.MidiFile == null || !_recordingNotes.ContainsKey(noteNumber)) return;

            var noteViewModel = _recordingNotes[noteNumber];
            var noteOn = noteViewModel.NoteOnEvent;
            var endTicks = _vm.ViewManager.TimeToTicks(_vm.CurrentTime);
            var durationTicks = endTicks - noteOn.AbsoluteTime;

            if (durationTicks <= 10)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    _vm.AllNotes.Remove(noteViewModel);
                    _vm.RequestNoteRedraw(noteViewModel);
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

            _vm.UndoRedoService.Execute(new NoteChangeCommand(noteViewModel, oldStart, (long)oldDuration, oldNoteNumber, oldCentOffset, oldChannel, noteViewModel.Velocity, noteOn.AbsoluteTime, (long)noteOn.NoteLength, noteOn.NoteNumber, noteViewModel.CentOffset, noteViewModel.Channel, noteViewModel.Velocity));

            _vm.MidiFile.Events[0].Add(noteOn);
            _vm.MidiFile.Events[0].Add(noteOff);

            _vm.UpdatePlaybackMidiData();
            _vm.RequestNoteRedraw(noteViewModel);

            _recordingNotes.Remove(noteNumber);
        }

        public void AddNoteInternal(NoteViewModel noteViewModel)
        {
            if (_vm.MidiFile == null) return;
            var noteOnEvent = noteViewModel.NoteOnEvent;
            _vm.MidiFile.Events[0].Add(noteOnEvent);
            if (noteOnEvent.OffEvent != null)
            {
                _vm.MidiFile.Events[0].Add(noteOnEvent.OffEvent);
            }
            _vm.SortMidiEvents();
            _vm.AllNotes.Add(noteViewModel);
            var sortedNotes = _vm.AllNotes.OrderBy(n => n.StartTicks).ToList();
            _vm.AllNotes.Clear();
            foreach (var n in sortedNotes) _vm.AllNotes.Add(n);
            _vm.RequestNoteRedraw(noteViewModel);
            _vm.UpdatePlaybackMidiData();
        }

        public void RemoveNote(NoteViewModel noteViewModel)
        {
            _vm.UndoRedoService.Execute(new RemoveNoteCommand(_vm, noteViewModel));
        }

        public void DeleteSelectedNotes()
        {
            if (!_vm.SelectedNotes.Any()) return;
            var commands = _vm.SelectedNotes.Select(n => new RemoveNoteCommand(_vm, n)).ToList();
            _vm.UndoRedoService.Execute(new CompositeCommand(commands));
        }

        public void RemoveNoteInternal(NoteViewModel noteViewModel)
        {
            if (_vm.MidiFile == null) return;

            var noteOnEvent = noteViewModel.NoteOnEvent;
            var noteOffEvent = noteOnEvent.OffEvent;

            foreach (var track in _vm.MidiFile.Events)
            {
                track.Remove(noteOnEvent);
                if (noteOffEvent != null) track.Remove(noteOffEvent);
            }

            _vm.AllNotes.Remove(noteViewModel);
            noteViewModel.IsSelected = false;
            noteViewModel.IsEditing = false;
            _vm.SelectedNotes.Remove(noteViewModel);
            if (_vm.SelectedNote == noteViewModel) _vm.SelectedNote = null;

            _vm.RequestNoteRedraw(noteViewModel);
            _vm.UpdatePlaybackMidiData();
        }

        public void RemoveOverlappingNotes(long startTicks, long endTicks, int noteNumber, NoteViewModel? noteToKeep = null)
        {
            if (_vm.MidiFile == null) return;
            switch (_vm.EditorSettings.Note.NoteOverlapBehavior)
            {
                case NoteOverlapBehavior.Keep: return;
                case NoteOverlapBehavior.Overwrite:
                case NoteOverlapBehavior.Delete:
                    var overlappingNotes = _vm.AllNotes.Where(n =>
                        n != noteToKeep &&
                        n.NoteNumber == noteNumber &&
                        startTicks < n.StartTicks + n.DurationTicks &&
                        endTicks > n.StartTicks
                    ).ToList();

                    if (overlappingNotes.Any())
                    {
                        _vm.UndoRedoService.Execute(new CompositeCommand(overlappingNotes.Select(n => new RemoveNoteCommand(_vm, n))));
                    }
                    break;
            }
        }

        public void SplitSelectedNotes(Point? clickPosition)
        {
            if (!_vm.CanSplitNotes || _vm.MidiFile == null || clickPosition == null) return;

            var noteToSplit = _vm.SelectedNotes.First();
            var splitTime = _vm.ViewManager.PositionToTime(clickPosition.Value.X);
            var splitTick = _vm.ViewManager.TimeToTicks(splitTime);

            if (splitTick <= noteToSplit.StartTicks || splitTick >= noteToSplit.StartTicks + noteToSplit.DurationTicks)
            {
                splitTick = noteToSplit.StartTicks + noteToSplit.DurationTicks / 2;
            }

            var commands = new List<IUndoableCommand>();
            commands.Add(new RemoveNoteCommand(_vm, noteToSplit));
            var tempoMap = MidiProcessor.ExtractTempoMap(_vm.MidiFile, MidiConfiguration.Default);

            var duration1 = splitTick - noteToSplit.StartTicks;
            if (duration1 > 0)
            {
                var noteOn1 = new NAudioMidi.NoteOnEvent(noteToSplit.StartTicks, 1, noteToSplit.NoteNumber, noteToSplit.Velocity, (int)duration1);
                var noteOff1 = new NAudioMidi.NoteEvent(noteToSplit.StartTicks + duration1, 1, NAudioMidi.MidiCommandCode.NoteOff, noteToSplit.NoteNumber, 0);
                noteOn1.OffEvent = noteOff1;
                var noteVm1 = new NoteViewModel(noteOn1, _vm.MidiFile.DeltaTicksPerQuarterNote, tempoMap, _vm) { CentOffset = noteToSplit.CentOffset };
                commands.Add(new AddNoteCommand(_vm, noteVm1));
            }

            var duration2 = (noteToSplit.StartTicks + noteToSplit.DurationTicks) - splitTick;
            if (duration2 > 0)
            {
                var noteOn2 = new NAudioMidi.NoteOnEvent(splitTick, 1, noteToSplit.NoteNumber, noteToSplit.Velocity, (int)duration2);
                var noteOff2 = new NAudioMidi.NoteEvent(splitTick + duration2, 1, NAudioMidi.MidiCommandCode.NoteOff, noteToSplit.NoteNumber, 0);
                noteOn2.OffEvent = noteOff2;
                var noteVm2 = new NoteViewModel(noteOn2, _vm.MidiFile.DeltaTicksPerQuarterNote, tempoMap, _vm) { CentOffset = noteToSplit.CentOffset };
                commands.Add(new AddNoteCommand(_vm, noteVm2));
            }

            _vm.UndoRedoService.Execute(new CompositeCommand(commands));
        }

        public void MergeSelectedNotes()
        {
            if (!_vm.CanMergeNotes || _vm.MidiFile == null) return;

            var orderedNotes = _vm.SelectedNotes.OrderBy(n => n.StartTicks).ToList();
            var firstNote = orderedNotes.First();
            var lastNote = orderedNotes.Last();

            var newStartTick = firstNote.StartTicks;
            var newEndTick = lastNote.StartTicks + lastNote.DurationTicks;
            var newDuration = newEndTick - newStartTick;

            var commands = new List<IUndoableCommand>();
            foreach (var note in orderedNotes) commands.Add(new RemoveNoteCommand(_vm, note));

            var tempoMap = MidiProcessor.ExtractTempoMap(_vm.MidiFile, MidiConfiguration.Default);
            var noteOn = new NAudioMidi.NoteOnEvent(newStartTick, 1, firstNote.NoteNumber, firstNote.Velocity, (int)newDuration);
            var noteOff = new NAudioMidi.NoteEvent(newEndTick, 1, NAudioMidi.MidiCommandCode.NoteOff, firstNote.NoteNumber, 0);
            noteOn.OffEvent = noteOff;
            var newNoteVm = new NoteViewModel(noteOn, _vm.MidiFile.DeltaTicksPerQuarterNote, tempoMap, _vm) { CentOffset = firstNote.CentOffset };
            commands.Add(new AddNoteCommand(_vm, newNoteVm));

            _vm.UndoRedoService.Execute(new CompositeCommand(commands));
        }

        public void QuantizeSelectedNotes()
        {
            if (_vm.MidiFile == null || !_vm.SelectedNotes.Any()) return;
            var quantizeTicks = _vm.ViewManager.GetTicksPerGrid();
            if (quantizeTicks <= 0) return;

            var strength = _vm.QuantizeSettings.Strength / 100.0;
            var swing = _vm.QuantizeSettings.Swing / 100.0;
            var commands = new List<IUndoableCommand>();

            foreach (var note in _vm.SelectedNotes.ToList())
            {
                long beat = (long)Math.Floor(note.StartTicks / quantizeTicks);
                long beatStart = (long)(beat * quantizeTicks);
                double swingOffset = (beat % 2 != 0) ? quantizeTicks * swing : 0;
                long snappedTick = beatStart + (long)swingOffset;
                long newStartTicks = (long)(note.StartTicks + (snappedTick - note.StartTicks) * strength);

                commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, newStartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity));
            }

            if (commands.Any()) _vm.ExecuteCompositeNoteChange(commands);
        }

        public void Transpose()
        {
            var dialog = new TransposeDialog();
            if (dialog.ShowDialog() == true)
            {
                var semitones = dialog.ViewModel.TotalSemitones;
                if (semitones == 0) return;
                var commands = new List<IUndoableCommand>();
                foreach (var note in _vm.SelectedNotes)
                {
                    var newNoteNumber = note.NoteNumber + semitones;
                    if (newNoteNumber >= 0 && newNoteNumber <= 127)
                    {
                        commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, note.DurationTicks, newNoteNumber, note.CentOffset, note.Channel, note.Velocity));
                    }
                }
                if (commands.Any()) _vm.ExecuteCompositeNoteChange(commands);
            }
        }

        public void ChangeVelocity()
        {
            var dialog = new VelocityDialog();
            if (dialog.ShowDialog() == true)
            {
                var commands = new List<IUndoableCommand>();
                var notes = _vm.SelectedNotes.OrderBy(n => n.StartTicks).ToList();
                for (int i = 0; i < notes.Count; i++)
                {
                    var note = notes[i];
                    int newVelocity = dialog.ViewModel.IsFixedValueMode ? dialog.ViewModel.FixedValue :
                        (int)(dialog.ViewModel.RampStartValue + (dialog.ViewModel.RampEndValue - dialog.ViewModel.RampStartValue) * (notes.Count > 1 ? (float)i / (notes.Count - 1) : 0));
                    newVelocity = Math.Clamp(newVelocity, 1, 127);
                    commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, newVelocity));
                }
                if (commands.Any()) _vm.ExecuteCompositeNoteChange(commands);
            }
        }

        public void Humanize()
        {
            var dialog = new HumanizeDialog();
            if (dialog.ShowDialog() == true)
            {
                var commands = new List<IUndoableCommand>();
                var random = new Random();
                foreach (var note in _vm.SelectedNotes)
                {
                    var timingOffset = (long)((random.NextDouble() * 2 - 1) * dialog.ViewModel.TimingAmount);
                    var velocityOffset = (int)((random.NextDouble() * 2 - 1) * dialog.ViewModel.VelocityAmount);
                    var newStart = note.StartTicks + timingOffset;
                    var newVelocity = Math.Clamp(note.Velocity + velocityOffset, 1, 127);
                    commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, newStart, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, newVelocity));
                }
                if (commands.Any()) _vm.ExecuteCompositeNoteChange(commands);
            }
        }

        public void PasteNotes(List<NoteViewModel> clipboard)
        {
            if (!clipboard.Any() || _vm.MidiFile == null) return;
            var pasteTimeTicks = _vm.ViewManager.TimeToTicks(_vm.CurrentTime);
            var firstNoteTicks = clipboard.Min(n => n.StartTicks);
            var addedNotes = new List<NoteViewModel>();
            var tempoMap = MidiProcessor.ExtractTempoMap(_vm.MidiFile, MidiConfiguration.Default);

            foreach (var originalNote in clipboard)
            {
                var timeOffset = originalNote.StartTicks - firstNoteTicks;
                var newStartTicks = pasteTimeTicks + timeOffset;
                var newDurationTicks = originalNote.DurationTicks;
                RemoveOverlappingNotes(newStartTicks, newStartTicks + newDurationTicks, originalNote.NoteNumber);
                var noteOn = new NAudioMidi.NoteOnEvent(newStartTicks, 1, originalNote.NoteNumber, originalNote.Velocity, (int)newDurationTicks);
                var noteOff = new NAudioMidi.NoteEvent(newStartTicks + noteOn.NoteLength, 1, NAudioMidi.MidiCommandCode.NoteOff, originalNote.NoteNumber, 0);
                noteOn.OffEvent = noteOff;
                var noteViewModel = new NoteViewModel(noteOn, _vm.MidiFile.DeltaTicksPerQuarterNote, tempoMap, _vm);
                addedNotes.Add(noteViewModel);
            }
            _vm.UndoRedoService.Execute(new CompositeCommand(addedNotes.Select(n => new AddNoteCommand(_vm, n))));
        }

        public void ChangeDuration(double factor)
        {
            var commands = new List<IUndoableCommand>();
            foreach (var note in _vm.SelectedNotes)
            {
                var newDuration = (long)(note.DurationTicks * factor);
                if (newDuration > 0 && newDuration != note.DurationTicks)
                {
                    commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, newDuration, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity));
                }
            }
            if (commands.Any()) _vm.ExecuteCompositeNoteChange(commands);
        }

        public void InvertPitch()
        {
            if (_vm.SelectedNotes.Count < 2) return;
            var sortedNotes = _vm.SelectedNotes.OrderBy(n => n.NoteNumber).ToList();
            var pivot = (sortedNotes.First().NoteNumber + sortedNotes.Last().NoteNumber) / 2.0;
            var commands = new List<IUndoableCommand>();
            foreach (var note in _vm.SelectedNotes)
            {
                var newNoteNumber = Math.Clamp((int)Math.Round(pivot - (note.NoteNumber - pivot)), 0, 127);
                commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, note.StartTicks, note.DurationTicks, newNoteNumber, note.CentOffset, note.Channel, note.Velocity));
            }
            if (commands.Any()) _vm.ExecuteCompositeNoteChange(commands);
        }

        public void Retrograde()
        {
            if (_vm.SelectedNotes.Count < 2) return;
            var orderedNotes = _vm.SelectedNotes.OrderBy(n => n.StartTicks).ToList();
            var totalDuration = orderedNotes.Last().StartTicks - orderedNotes.First().StartTicks;
            var commands = new List<IUndoableCommand>();
            for (int i = 0; i < orderedNotes.Count; i++)
            {
                var note = orderedNotes[i];
                var opposite = orderedNotes[orderedNotes.Count - 1 - i];
                var newPos = orderedNotes.First().StartTicks + (totalDuration - (opposite.StartTicks - orderedNotes.First().StartTicks));
                commands.Add(new NoteChangeCommand(note, note.StartTicks, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity, newPos, note.DurationTicks, note.NoteNumber, note.CentOffset, note.Channel, note.Velocity));
            }
            if (commands.Any()) _vm.ExecuteCompositeNoteChange(commands);
        }
    }
}
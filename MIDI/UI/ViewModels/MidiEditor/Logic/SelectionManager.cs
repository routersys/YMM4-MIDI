using MIDI.UI.Views.MidiEditor.Modals;
using System.Windows;

namespace MIDI.UI.ViewModels.MidiEditor.Logic
{
    public class SelectionManager
    {
        private readonly MidiEditorViewModel _vm;
        private readonly List<NoteViewModel> _clipboard = new List<NoteViewModel>();

        public SelectionManager(MidiEditorViewModel vm)
        {
            _vm = vm;
        }

        public void CopySelectedNotes()
        {
            _clipboard.Clear();
            if (!_vm.SelectedNotes.Any()) return;
            foreach (var note in _vm.SelectedNotes) _clipboard.Add(note);
        }

        public List<NoteViewModel> GetClipboard() => _clipboard;

        public void ClearSelections(bool clearNotes = true, bool clearFlags = true)
        {
            if (clearNotes)
            {
                var oldSelections = _vm.SelectedNotes.ToList();
                _vm.SelectedNotes.Clear();
                foreach (var note in oldSelections) note.IsSelected = false;
                _vm.SelectedNote = null;
            }

            if (clearFlags)
            {
                foreach (var flag in _vm.SelectedFlags.ToList()) flag.IsSelected = false;
                _vm.SelectedFlags.Clear();
                _vm.SelectedFlag = null;
            }
            _vm.OnPropertyChanged(nameof(_vm.SelectedFlag));
            _vm.OnPropertyChanged(nameof(_vm.SelectedNote));
        }

        public void SelectAllNotes()
        {
            if (_vm.AllNotes.All(n => n.IsSelected))
            {
                foreach (var note in _vm.AllNotes) note.IsSelected = false;
                _vm.SelectedNotes.Clear();
            }
            else
            {
                _vm.SelectedNotes.Clear();
                foreach (var note in _vm.AllNotes)
                {
                    note.IsSelected = true;
                    _vm.SelectedNotes.Add(note);
                }
            }
            _vm.SelectedNote = _vm.SelectedNotes.FirstOrDefault();
        }

        public void InvertSelection()
        {
            var allNotes = _vm.AllNotes.ToList();
            var selected = _vm.SelectedNotes.ToList();
            _vm.SelectedNotes.Clear();
            foreach (var note in allNotes)
            {
                note.IsSelected = !selected.Contains(note);
                if (note.IsSelected) _vm.SelectedNotes.Add(note);
            }
            _vm.SelectedNote = _vm.SelectedNotes.FirstOrDefault();
        }

        public void SelectSamePitch()
        {
            if (!_vm.SelectedNotes.Any()) return;
            var pitch = _vm.SelectedNotes.First().NoteNumber;
            foreach (var note in _vm.AllNotes)
            {
                if (note.NoteNumber == pitch)
                {
                    note.IsSelected = true;
                    if (!_vm.SelectedNotes.Contains(note)) _vm.SelectedNotes.Add(note);
                }
            }
        }

        public void SelectSameChannel()
        {
            if (!_vm.SelectedNotes.Any()) return;
            var channel = _vm.SelectedNotes.First().Channel;
            foreach (var note in _vm.AllNotes)
            {
                if (note.Channel == channel)
                {
                    note.IsSelected = true;
                    if (!_vm.SelectedNotes.Contains(note)) _vm.SelectedNotes.Add(note);
                }
            }
        }

        public void LoopSelection()
        {
            if (!_vm.SelectedNotes.Any())
            {
                _vm.PlaybackService.SetLoop(false, System.TimeSpan.Zero, System.TimeSpan.Zero);
                return;
            }

            var minTick = _vm.SelectedNotes.Min(n => n.StartTicks);
            var maxTick = _vm.SelectedNotes.Max(n => n.StartTicks + n.DurationTicks);
            var startTime = _vm.ViewManager.TicksToTime(minTick);
            var endTime = _vm.ViewManager.TicksToTime(maxTick);

            _vm.PlaybackService.SetLoop(true, startTime, endTime);

            if (_vm.CurrentTime < startTime || _vm.CurrentTime >= endTime) _vm.CurrentTime = startTime;
            if (!_vm.IsPlaying) _vm.PlayPauseCommand.Execute(null);
        }

        public void UpdateSelectionStatus()
        {
            if (_vm.SelectedNotes.Count > 1)
            {
                _vm.SelectionStatusText = $"{_vm.SelectedNotes.Count} ノートを選択中";
            }
            else if (_vm.SelectedNotes.Count == 1 && _vm.SelectedNote != null)
            {
                _vm.SelectionStatusText = $"ノート: {_vm.SelectedNote.NoteName}, Velocity: {_vm.SelectedNote.Velocity}, Start: {_vm.SelectedNote.StartTicks}";
            }
            else if (_vm.SelectedFlags.Count > 1)
            {
                _vm.SelectionStatusText = $"{_vm.SelectedFlags.Count} フラグを選択中";
            }
            else if (_vm.SelectedFlags.Count == 1 && _vm.SelectedFlag != null)
            {
                _vm.SelectionStatusText = $"フラグ: {_vm.SelectedFlag.Name}, Time: {_vm.SelectedFlag.Time:mm\\:ss\\.fff}";
            }
            else
            {
                _vm.SelectionStatusText = "";
            }
        }

        public void RenameFlag()
        {
            var flag = _vm.SelectedFlags.FirstOrDefault();
            if (flag == null) return;
            var dialog = new RenameFlagWindow(flag.Name) { Owner = Application.Current.MainWindow };
            if (dialog.ShowDialog() == true)
            {
                var command = new FlagChangeCommand(flag, flag.Time, flag.Name, flag.Time, dialog.ViewModel.FlagName);
                _vm.UndoRedoService.Execute(command);
            }
        }
    }
}
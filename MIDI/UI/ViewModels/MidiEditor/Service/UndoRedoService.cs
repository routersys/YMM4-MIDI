using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NAudioMidi = NAudio.Midi;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public interface IUndoableCommand
    {
        void Execute();
        void Undo();
    }

    public class UndoRedoService
    {
        private readonly Stack<IUndoableCommand> _undoStack = new Stack<IUndoableCommand>();
        private readonly Stack<IUndoableCommand> _redoStack = new Stack<IUndoableCommand>();

        public event Action? StateChanged;

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void Execute(IUndoableCommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear();
            OnStateChanged();
        }

        public void Undo()
        {
            if (CanUndo)
            {
                var command = _undoStack.Pop();
                command.Undo();
                _redoStack.Push(command);
                OnStateChanged();
            }
        }

        public void Redo()
        {
            if (CanRedo)
            {
                var command = _redoStack.Pop();
                command.Execute();
                _undoStack.Push(command);
                OnStateChanged();
            }
        }

        private void OnStateChanged()
        {
            StateChanged?.Invoke();
        }
    }

    public class NoteChangeCommand : IUndoableCommand
    {
        private readonly NoteViewModel _note;
        private readonly MidiEditorViewModel _viewModel;
        private readonly long _oldStartTicks;
        private readonly long _oldDurationTicks;
        private readonly int _oldNoteNumber;
        private readonly int _oldCentOffset;
        private readonly int _oldChannel;
        private readonly int _oldVelocity;
        private readonly long _newStartTicks;
        private readonly long _newDurationTicks;
        private readonly int _newNoteNumber;
        private readonly int _newCentOffset;
        private readonly int _newChannel;
        public int NewVelocity { get; set; }

        public NoteChangeCommand(NoteViewModel note, long oldStartTicks, long oldDurationTicks, int oldNoteNumber, int oldCentOffset, int oldChannel, int oldVelocity, long newStartTicks, long newDurationTicks, int newNoteNumber, int newCentOffset, int newChannel, int newVelocity)
        {
            _note = note;
            _viewModel = note.GetParentViewModel();
            _oldStartTicks = oldStartTicks;
            _oldDurationTicks = oldDurationTicks;
            _oldNoteNumber = oldNoteNumber;
            _oldCentOffset = oldCentOffset;
            _oldChannel = oldChannel;
            _oldVelocity = oldVelocity;
            _newStartTicks = newStartTicks;
            _newDurationTicks = newDurationTicks;
            _newNoteNumber = newNoteNumber;
            _newCentOffset = newCentOffset;
            _newChannel = newChannel;
            NewVelocity = newVelocity;
        }

        public void Execute()
        {
            var currentStart = _note.StartTicks;
            var currentDuration = _note.DurationTicks;
            var currentNoteNum = _note.NoteNumber;
            var currentCent = _note.CentOffset;
            var currentChannel = _note.Channel;
            var currentVel = _note.Velocity;

            _note.NoteNumber = _oldNoteNumber;
            _note.CentOffset = _oldCentOffset;
            _note.Channel = _oldChannel;
            _note.Velocity = _oldVelocity;
            _note.UpdateNote(_oldStartTicks, _oldDurationTicks);
            _viewModel.RequestNoteRedraw(_note);

            _note.NoteNumber = _newNoteNumber;
            _note.CentOffset = _newCentOffset;
            _note.Channel = _newChannel;
            _note.Velocity = NewVelocity;
            _note.UpdateNote(_newStartTicks, _newDurationTicks);
        }

        public void Undo()
        {
            _viewModel.RequestNoteRedraw(_note);
            _note.NoteNumber = _oldNoteNumber;
            _note.CentOffset = _oldCentOffset;
            _note.Channel = _oldChannel;
            _note.Velocity = _oldVelocity;
            _note.UpdateNote(_oldStartTicks, _oldDurationTicks);
            _viewModel.RequestNoteRedraw(_note);
        }
    }

    public class AddNoteCommand : IUndoableCommand
    {
        private readonly MidiEditorViewModel _viewModel;
        private readonly NoteViewModel _noteViewModel;

        public AddNoteCommand(MidiEditorViewModel viewModel, NoteViewModel noteViewModel)
        {
            _viewModel = viewModel;
            _noteViewModel = noteViewModel;
        }

        public void Execute()
        {
            _noteViewModel.IsSelected = false;
            _noteViewModel.IsEditing = false;
            _viewModel.AddNoteInternal(_noteViewModel);
        }

        public void Undo()
        {
            _viewModel.RemoveNoteInternal(_noteViewModel);
        }
    }

    public class RemoveNoteCommand : IUndoableCommand
    {
        private readonly MidiEditorViewModel _viewModel;
        private readonly NoteViewModel _noteViewModel;
        private readonly bool _wasSelected;
        private readonly bool _wasEditing;

        public RemoveNoteCommand(MidiEditorViewModel viewModel, NoteViewModel noteViewModel)
        {
            _viewModel = viewModel;
            _noteViewModel = noteViewModel;
            _wasSelected = noteViewModel.IsSelected;
            _wasEditing = noteViewModel.IsEditing;
        }

        public void Execute()
        {
            _viewModel.RemoveNoteInternal(_noteViewModel);
        }

        public void Undo()
        {
            _viewModel.AddNoteInternal(_noteViewModel);
            if (_wasSelected)
            {
                _noteViewModel.IsSelected = true;
                if (!_viewModel.SelectedNotes.Contains(_noteViewModel))
                {
                    _viewModel.SelectedNotes.Add(_noteViewModel);
                }
            }
            if (_wasEditing)
            {
                _noteViewModel.IsEditing = true;
            }
        }
    }

    public class AddFlagCommand : IUndoableCommand
    {
        private readonly MidiEditorViewModel _viewModel;
        private readonly FlagViewModel _flag;

        public AddFlagCommand(MidiEditorViewModel viewModel, FlagViewModel flag)
        {
            _viewModel = viewModel;
            _flag = flag;
        }

        public void Execute() => _viewModel.Flags.Add(_flag);
        public void Undo() => _viewModel.Flags.Remove(_flag);
    }

    public class RemoveFlagCommand : IUndoableCommand
    {
        private readonly MidiEditorViewModel _viewModel;
        private readonly FlagViewModel _flag;

        public RemoveFlagCommand(MidiEditorViewModel viewModel, FlagViewModel flag)
        {
            _viewModel = viewModel;
            _flag = flag;
        }

        public void Execute() => _viewModel.Flags.Remove(_flag);
        public void Undo() => _viewModel.Flags.Add(_flag);
    }

    public class FlagChangeCommand : IUndoableCommand
    {
        private readonly FlagViewModel _flag;
        private readonly TimeSpan _oldTime;
        private readonly string _oldName;
        private readonly TimeSpan _newTime;
        private readonly string _newName;

        public FlagChangeCommand(FlagViewModel flag, TimeSpan oldTime, string oldName, TimeSpan newTime, string newName)
        {
            _flag = flag;
            _oldTime = oldTime;
            _oldName = oldName;
            _newTime = newTime;
            _newName = newName;
        }

        public void Execute()
        {
            _flag.Time = _newTime;
            _flag.Name = _newName;
        }

        public void Undo()
        {
            _flag.Time = _oldTime;
            _flag.Name = _oldName;
        }
    }

    public class CompositeCommand : IUndoableCommand
    {
        private readonly List<IUndoableCommand> _commands = new List<IUndoableCommand>();

        public CompositeCommand(IEnumerable<IUndoableCommand> commands)
        {
            _commands.AddRange(commands);
        }

        public void Execute()
        {
            foreach (var command in _commands)
            {
                command.Execute();
            }
        }

        public void Undo()
        {
            foreach (var command in Enumerable.Reverse(_commands))
            {
                command.Undo();
            }
        }
    }
}
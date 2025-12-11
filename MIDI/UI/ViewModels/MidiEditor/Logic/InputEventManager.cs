using MIDI.Configuration.Models;
using MIDI.UI.ViewModels.MidiEditor.Modals;
using System.Windows.Input;
using MIDI.UI.ViewModels.MidiEditor.Enums;

namespace MIDI.UI.ViewModels.MidiEditor.Logic
{
    public class InputEventManager
    {
        private readonly MidiEditorViewModel _vm;
        private readonly KeyboardMappingViewModel _keyboardMappingViewModel;

        public InputEventManager(MidiEditorViewModel vm, KeyboardMappingViewModel keyboardMappingViewModel)
        {
            _vm = vm;
            _keyboardMappingViewModel = keyboardMappingViewModel;
        }

        public void HandleKeyDown(Key key)
        {
            if (_vm.MidiInputMode == MidiInputMode.Keyboard) return;

            var mapping = _keyboardMappingViewModel.Mappings.FirstOrDefault(m => m.Key.ToString().Equals(key.ToString(), StringComparison.OrdinalIgnoreCase));
            if (mapping != null)
            {
                if (_vm.MidiInputMode == MidiInputMode.Realtime && _vm.IsPlaying)
                {
                    _vm.NoteEditorManager.AddNoteAtCurrentTime(mapping.NoteNumber, 100);
                }
                else if (_vm.MidiInputMode == MidiInputMode.ComputerKeyboard && _vm.PianoKeysMap.TryGetValue(mapping.NoteNumber, out var keyVm))
                {
                    if (!keyVm.IsPressed)
                    {
                        _vm.PlayPianoKey(mapping.NoteNumber);
                        keyVm.IsPressed = true;
                    }
                }
            }
        }

        public void HandleKeyUp(Key key)
        {
            if (_vm.MidiInputMode == MidiInputMode.Keyboard) return;
            var mapping = _keyboardMappingViewModel.Mappings.FirstOrDefault(m => m.Key.ToString().Equals(key.ToString(), StringComparison.OrdinalIgnoreCase));
            if (mapping != null)
            {
                if (_vm.MidiInputMode == MidiInputMode.Realtime && _vm.IsPlaying)
                {
                    _vm.NoteEditorManager.StopNoteAtCurrentTime(mapping.NoteNumber);
                }
                else if (_vm.MidiInputMode == MidiInputMode.ComputerKeyboard && _vm.PianoKeysMap.TryGetValue(mapping.NoteNumber, out var keyVm))
                {
                    _vm.StopPianoKey(mapping.NoteNumber);
                    keyVm.IsPressed = false;
                }
            }
        }

        public void SetMidiInputMode(string modeStr)
        {
            if (Enum.TryParse<MidiInputMode>(modeStr, out var mode))
            {
                _vm.MidiInputMode = mode;
            }
        }

        public void SetAdditionalKeyLabel(string modeStr)
        {
            if (Enum.TryParse<AdditionalKeyLabelType>(modeStr, out var mode))
            {
                _vm.AdditionalKeyLabel = mode;
            }
        }

        public void SetTuningSystem(TuningSystemType tuningSystem)
        {
            _vm.TuningSystem = tuningSystem;
        }
    }
}
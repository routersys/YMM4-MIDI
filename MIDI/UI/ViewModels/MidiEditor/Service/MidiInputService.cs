using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using NAudioMidi = NAudio.Midi;
using System.Collections.Generic;
using MIDI.UI.ViewModels.MidiEditor.Modals;
using MIDI.Configuration.Models;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class MidiInputService : ViewModelBase, IDisposable
    {
        private readonly MidiEditorViewModel _editorViewModel;
        private NAudioMidi.MidiIn? _midiIn;
        private readonly KeyboardMappingViewModel _keyboardMappingViewModel;

        public ObservableCollection<string> MidiInputDevices { get; } = new ObservableCollection<string>();

        public MidiInputMode InputMode { get; set; }

        private bool _isMidiInputEnabled;
        public bool IsMidiInputEnabled
        {
            get => _isMidiInputEnabled;
            set
            {
                if (SetField(ref _isMidiInputEnabled, value))
                {
                    MidiConfiguration.Default.Debug.EnableMidiInput = value;
                    MidiConfiguration.Default.Save();
                    if (value)
                    {
                        StartMidiIn();
                    }
                    else
                    {
                        StopMidiIn();
                    }
                }
            }
        }

        private string _selectedMidiInputDevice = "";
        public string SelectedMidiInputDevice
        {
            get => _selectedMidiInputDevice;
            set
            {
                if (SetField(ref _selectedMidiInputDevice, value))
                {
                    MidiConfiguration.Default.Debug.MidiInputDevice = value;
                    MidiConfiguration.Default.Save();
                    if (IsMidiInputEnabled)
                    {
                        StopMidiIn();
                        StartMidiIn();
                    }
                }
            }
        }
        public MidiInputService(MidiEditorViewModel editorViewModel, KeyboardMappingViewModel keyboardMappingViewModel)
        {
            _editorViewModel = editorViewModel;
            _keyboardMappingViewModel = keyboardMappingViewModel;
            LoadMidiDevices();
            IsMidiInputEnabled = MidiConfiguration.Default.Debug.EnableMidiInput;
            InputMode = MidiEditorSettings.Default.Input.MidiInputMode;
            if (IsMidiInputEnabled)
            {
                StartMidiIn();
            }
        }

        private void LoadMidiDevices()
        {
            for (int i = 0; i < NAudioMidi.MidiIn.NumberOfDevices; i++)
            {
                MidiInputDevices.Add(NAudioMidi.MidiIn.DeviceInfo(i).ProductName);
            }
            if (!string.IsNullOrEmpty(MidiConfiguration.Default.Debug.MidiInputDevice) && MidiInputDevices.Contains(MidiConfiguration.Default.Debug.MidiInputDevice))
            {
                SelectedMidiInputDevice = MidiConfiguration.Default.Debug.MidiInputDevice;
            }
            else if (MidiInputDevices.Any())
            {
                SelectedMidiInputDevice = MidiInputDevices[0];
            }
        }

        private void StartMidiIn()
        {
            if (string.IsNullOrEmpty(SelectedMidiInputDevice)) return;

            int deviceId = -1;
            for (int i = 0; i < NAudioMidi.MidiIn.NumberOfDevices; i++)
            {
                if (NAudioMidi.MidiIn.DeviceInfo(i).ProductName == SelectedMidiInputDevice)
                {
                    deviceId = i;
                    break;
                }
            }

            if (deviceId == -1) return;

            try
            {
                _midiIn = new NAudioMidi.MidiIn(deviceId);
                _midiIn.MessageReceived += OnMidiInMessage;
                _midiIn.Start();
                _editorViewModel.PlayPianoKey(0);
                _editorViewModel.StopPianoKey(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"MIDI入力の開始に失敗しました: {ex.Message}");
            }
        }

        private void StopMidiIn()
        {
            _midiIn?.Stop();
            _midiIn?.Dispose();
            _midiIn = null;
        }

        private void OnMidiInMessage(object? sender, NAudioMidi.MidiInMessageEventArgs e)
        {

            if (InputMode == MidiInputMode.Realtime)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    switch (e.MidiEvent.CommandCode)
                    {
                        case NAudioMidi.MidiCommandCode.NoteOn:
                            var noteOn = (NAudioMidi.NoteOnEvent)e.MidiEvent;
                            if (noteOn.Velocity > 0)
                            {
                                _editorViewModel.PlayPianoKey(noteOn.NoteNumber);
                                _editorViewModel.AddNoteAtCurrentTime(noteOn.NoteNumber, noteOn.Velocity);
                            }
                            else
                            {
                                _editorViewModel.StopPianoKey(noteOn.NoteNumber);
                                _editorViewModel.StopNoteAtCurrentTime(noteOn.NoteNumber);
                            }
                            break;
                        case NAudioMidi.MidiCommandCode.NoteOff:
                            var noteOff = (NAudioMidi.NoteEvent)e.MidiEvent;
                            _editorViewModel.StopPianoKey(noteOff.NoteNumber);
                            _editorViewModel.StopNoteAtCurrentTime(noteOff.NoteNumber);
                            break;
                    }
                });
                return;
            }

            switch (e.MidiEvent.CommandCode)
            {
                case NAudioMidi.MidiCommandCode.NoteOn:
                    var noteOn = (NAudioMidi.NoteOnEvent)e.MidiEvent;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_editorViewModel.PianoKeysMap.TryGetValue(noteOn.NoteNumber, out var keyVm))
                        {
                            if (noteOn.Velocity > 0)
                            {
                                if (!keyVm.IsPressed)
                                {
                                    _editorViewModel.PlayPianoKey(noteOn.NoteNumber);
                                    keyVm.IsPressed = true;
                                }
                            }
                            else
                            {
                                _editorViewModel.StopPianoKey(noteOn.NoteNumber);
                                keyVm.IsPressed = false;
                            }
                        }
                    });
                    break;
                case NAudioMidi.MidiCommandCode.NoteOff:
                    var noteOff = (NAudioMidi.NoteEvent)e.MidiEvent;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (_editorViewModel.PianoKeysMap.TryGetValue(noteOff.NoteNumber, out var keyVm))
                        {
                            _editorViewModel.StopPianoKey(noteOff.NoteNumber);
                            keyVm.IsPressed = false;
                        }
                    });
                    break;
            }
        }
        public void Dispose()
        {
            StopMidiIn();
        }
    }
}
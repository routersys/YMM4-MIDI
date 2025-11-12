using MIDI.Configuration.Models;
using MIDI.UI.Commands;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;

namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class PianoKeyMappingViewModel : ViewModelBase
    {
        public int NoteNumber { get; }
        public string NoteName { get; }
        public string JapaneseNoteName { get; }
        public bool IsBlackKey { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetField(ref _isSelected, value);
        }

        public PianoKeyMappingViewModel(int noteNumber)
        {
            NoteNumber = noteNumber;
            NoteName = GetNoteName(noteNumber, out var japaneseNoteName);
            JapaneseNoteName = japaneseNoteName;
            int noteInOctave = noteNumber % 12;
            IsBlackKey = new[] { 1, 3, 6, 8, 10 }.Contains(noteInOctave);
        }

        private string GetNoteName(int noteNumber, out string japaneseNoteName)
        {
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            string[] japaneseNoteNames = { "ド", "ド#", "レ", "レ#", "ミ", "ファ", "ファ#", "ソ", "ソ#", "ラ", "ラ#", "シ" };
            int octave = (noteNumber / 12) - 1;
            japaneseNoteName = japaneseNoteNames[noteNumber % 12];
            return $"{noteNames[noteNumber % 12]}{octave}";
        }
    }

    public class KeyboardMapping : ViewModelBase, ICloneable
    {
        [JsonIgnore]
        private Action<KeyboardMapping>? _onDeleted;

        private string _key = "";
        public string Key
        {
            get => _key;
            set
            {
                if (SetField(ref _key, value) && string.IsNullOrEmpty(value))
                {
                    _onDeleted?.Invoke(this);
                }
            }
        }

        private int _noteNumber;
        public int NoteNumber
        {
            get => _noteNumber;
            set
            {
                if (SetField(ref _noteNumber, value))
                {
                    OnPropertyChanged(nameof(NoteName));
                }
            }
        }

        public string NoteName
        {
            get => GetNoteName(NoteNumber);
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    _onDeleted?.Invoke(this);
                    return;
                }
                if (TryParseNoteName(value, out int noteNumber))
                {
                    NoteNumber = noteNumber;
                }
            }
        }

        public KeyboardMapping() { }

        public KeyboardMapping(Action<KeyboardMapping>? onDeleted)
        {
            _onDeleted = onDeleted;
        }

        public void SetOnDeletedAction(Action<KeyboardMapping>? onDeleted)
        {
            _onDeleted = onDeleted;
        }

        private string GetNoteName(int noteNumber)
        {
            if (noteNumber < 0 || noteNumber > 127) return "無効";
            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = (noteNumber / 12) - 1;
            return $"{noteNames[noteNumber % 12]}{octave}";
        }

        private bool TryParseNoteName(string name, out int noteNumber)
        {
            noteNumber = 0;
            if (string.IsNullOrWhiteSpace(name)) return false;

            var match = Regex.Match(name.ToUpper().Trim(), @"^([A-G]#?)(-?[0-9]+)$");
            if (!match.Success) return false;

            string noteNamePart = match.Groups[1].Value;
            if (!int.TryParse(match.Groups[2].Value, out int octave)) return false;

            string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int noteIndex = Array.IndexOf(noteNames, noteNamePart);

            if (noteIndex == -1) return false;

            noteNumber = (octave + 1) * 12 + noteIndex;
            return noteNumber >= 0 && noteNumber <= 127;
        }

        public object Clone()
        {
            return new KeyboardMapping(_onDeleted)
            {
                Key = this.Key,
                NoteNumber = this.NoteNumber
            };
        }
    }


    public class KeyboardMappingViewModel : ViewModelBase
    {
        public ObservableCollection<KeyboardMapping> Mappings { get; }
        public ObservableCollection<PianoKeyMappingViewModel> PianoKeys { get; } = new ObservableCollection<PianoKeyMappingViewModel>();

        private PianoKeyMappingViewModel? _selectedPianoKey;

        public ICommand ResetToDefaultCommand { get; }
        public ICommand AddMappingCommand { get; }

        public KeyboardMappingViewModel()
        {
            for (int i = 127; i >= 0; i--)
            {
                PianoKeys.Add(new PianoKeyMappingViewModel(i));
            }

            Mappings = MidiEditorSettings.Default.KeyboardMappings;
            foreach (var mapping in Mappings)
            {
                mapping.SetOnDeletedAction(OnMappingDeleted);
                SubscribeToMappingChanges(mapping);
            }
            if (!Mappings.Any())
            {
                LoadDefaultMappings();
            }


            ResetToDefaultCommand = new RelayCommand(_ => LoadDefaultMappings());
            AddMappingCommand = new RelayCommand(_ => AddNewMapping());
        }

        private void AddNewMapping()
        {
            var newMapping = new KeyboardMapping(OnMappingDeleted);
            SubscribeToMappingChanges(newMapping);
            Mappings.Add(newMapping);
        }

        private void SubscribeToMappingChanges(KeyboardMapping mapping)
        {
            (mapping as INotifyPropertyChanged).PropertyChanged += (s, e) =>
            {
                if (s is KeyboardMapping changedMapping && (string.IsNullOrEmpty(changedMapping.Key) || string.IsNullOrEmpty(changedMapping.NoteName)))
                {
                    OnMappingDeleted(changedMapping);
                }
            };
        }

        private void OnMappingDeleted(KeyboardMapping mapping)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (Mappings.Contains(mapping))
                {
                    Mappings.Remove(mapping);
                }
            });
        }

        public void SelectPianoKey(PianoKeyMappingViewModel key)
        {
            if (_selectedPianoKey != null)
            {
                _selectedPianoKey.IsSelected = false;
            }
            _selectedPianoKey = key;
            _selectedPianoKey.IsSelected = true;
        }

        public void AssignKey(Key key)
        {
            if (_selectedPianoKey == null) return;

            var keyStr = key.ToString();
            var existingMapping = Mappings.FirstOrDefault(m => m.Key.Equals(keyStr, StringComparison.OrdinalIgnoreCase));
            if (existingMapping != null)
            {
                existingMapping.NoteNumber = _selectedPianoKey.NoteNumber;
            }
            else
            {
                var newMapping = new KeyboardMapping(OnMappingDeleted) { Key = keyStr, NoteNumber = _selectedPianoKey.NoteNumber };
                SubscribeToMappingChanges(newMapping);
                Mappings.Add(newMapping);
            }
        }


        private void LoadDefaultMappings()
        {
            Mappings.Clear();
            var defaultMappings = new List<KeyboardMapping>
            {
                new KeyboardMapping(OnMappingDeleted) { Key = "A", NoteNumber = 60 }, // C4
                new KeyboardMapping(OnMappingDeleted) { Key = "W", NoteNumber = 61 }, // C#4
                new KeyboardMapping(OnMappingDeleted) { Key = "S", NoteNumber = 62 }, // D4
                new KeyboardMapping(OnMappingDeleted) { Key = "E", NoteNumber = 63 }, // D#4
                new KeyboardMapping(OnMappingDeleted) { Key = "D", NoteNumber = 64 }, // E4
                new KeyboardMapping(OnMappingDeleted) { Key = "F", NoteNumber = 65 }, // F4
                new KeyboardMapping(OnMappingDeleted) { Key = "T", NoteNumber = 66 }, // F#4
                new KeyboardMapping(OnMappingDeleted) { Key = "G", NoteNumber = 67 }, // G4
                new KeyboardMapping(OnMappingDeleted) { Key = "Y", NoteNumber = 68 }, // G#4
                new KeyboardMapping(OnMappingDeleted) { Key = "H", NoteNumber = 69 }, // A4
                new KeyboardMapping(OnMappingDeleted) { Key = "U", NoteNumber = 70 }, // A#4
                new KeyboardMapping(OnMappingDeleted) { Key = "J", NoteNumber = 71 }, // B4
                new KeyboardMapping(OnMappingDeleted) { Key = "K", NoteNumber = 72 }  // C5
            };
            foreach (var mapping in defaultMappings)
            {
                SubscribeToMappingChanges(mapping);
                Mappings.Add(mapping);
            }
        }

        public void SaveMappings()
        {
            MidiEditorSettings.Default.Save();
        }
    }
}
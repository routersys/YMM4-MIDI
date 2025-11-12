using System.Linq;
using MIDI.Configuration.Models;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class PianoKeyViewModel : ViewModelBase
    {
        private readonly MidiEditorViewModel _parentViewModel;
        public int NoteNumber { get; }
        public string NoteName { get; }
        public string JapaneseNoteName { get; }
        public string AdditionalNoteName { get; }
        public bool IsBlackKey { get; }
        public double Height => 20 * _parentViewModel.VerticalZoom / _parentViewModel.KeyYScale;

        private bool _isPressed;
        public bool IsPressed
        {
            get => _isPressed;
            set => SetField(ref _isPressed, value);
        }

        private bool _isKeyboardPlaying;
        public bool IsKeyboardPlaying
        {
            get => _isKeyboardPlaying;
            set => SetField(ref _isKeyboardPlaying, value);
        }

        private bool _isPlaying;
        public bool IsPlaying
        {
            get => _isPlaying;
            set => SetField(ref _isPlaying, value);
        }


        public PianoKeyViewModel(int noteNumber, MidiEditorViewModel parentViewModel)
        {
            NoteNumber = noteNumber;
            _parentViewModel = parentViewModel;
            NoteName = GetNoteName(noteNumber, out var japaneseNoteName, out var additionalNoteName);
            JapaneseNoteName = japaneseNoteName;
            AdditionalNoteName = additionalNoteName;

            if (_parentViewModel.TuningSystem == TuningSystemType.TwentyFourToneEqualTemperament)
            {
                int noteInOctave = noteNumber % 24;
                IsBlackKey = new[] { 2, 3, 6, 7, 10, 11, 14, 15, 18, 19, 22, 23 }.Contains(noteInOctave);
            }
            else
            {
                int noteInOctave = noteNumber % 12;
                IsBlackKey = new[] { 1, 3, 6, 8, 10 }.Contains(noteInOctave);
            }


            _parentViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(MidiEditorViewModel.VerticalZoom))
                {
                    OnPropertyChanged(nameof(Height));
                }
            };
        }
        private string GetNoteName(int noteNumber, out string japaneseNoteName, out string additionalNoteName)
        {
            if (_parentViewModel.TuningSystem == TuningSystemType.TwentyFourToneEqualTemperament)
            {
                string[] noteNames = { "C", "C+", "C#", "C#+", "D", "D+", "D#", "D#+", "E", "E+", "F", "F+", "F#", "F#+", "G", "G+", "G#", "G#+", "A", "A+", "A#", "A#+", "B", "B+" };
                string[] japaneseNoteNames = { "ド", "ド+", "ド#", "ド#+", "レ", "レ+", "レ#", "レ#+", "ミ", "ミ+", "ファ", "ファ+", "ファ#", "ファ#+", "ソ", "ソ+", "ソ#", "ソ#+", "ラ", "ラ+", "ラ#", "ラ#+", "シ", "シ+" };
                string[] irohaNoteNames = { "ハ", "ハ+", "嬰ハ", "嬰ハ+", "ニ", "ニ+", "嬰ニ", "嬰ニ+", "ホ", "ホ+", "ヘ", "ヘ+", "嬰ヘ", "嬰ヘ+", "ト", "ト+", "嬰ト", "嬰ト+", "イ", "イ+", "嬰イ", "嬰イ+", "ロ", "ロ+" };
                int octave = (noteNumber / 24) - 1;
                int noteInOctave = noteNumber % 24;

                japaneseNoteName = japaneseNoteNames[noteInOctave];

                additionalNoteName = "";
                var additionalKeyLabelType = MidiEditorSettings.Default.View.AdditionalKeyLabel;
                if (additionalKeyLabelType != AdditionalKeyLabelType.None)
                {
                    string[] additionalNames = additionalKeyLabelType == AdditionalKeyLabelType.DoReMi ? japaneseNoteNames : irohaNoteNames;
                    additionalNoteName = additionalNames[noteInOctave];
                }

                return $"{noteNames[noteInOctave]}{octave}";
            }
            else
            {
                string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
                string[] japaneseNoteNames = { "ド", "ド#", "レ", "レ#", "ミ", "ファ", "ファ#", "ソ", "ソ#", "ラ", "ラ#", "シ" };
                string[] irohaNoteNames = { "ハ", "嬰ハ", "ニ", "嬰ニ", "ホ", "ヘ", "嬰ヘ", "ト", "嬰ト", "イ", "嬰イ", "ロ" };

                int octave = (noteNumber / 12) - 1;
                int noteInOctave = noteNumber % 12;

                japaneseNoteName = japaneseNoteNames[noteInOctave];

                additionalNoteName = "";
                var additionalKeyLabelType = MidiEditorSettings.Default.View.AdditionalKeyLabel;
                if (additionalKeyLabelType != AdditionalKeyLabelType.None)
                {
                    string[] additionalNames = additionalKeyLabelType == AdditionalKeyLabelType.DoReMi ? japaneseNoteNames : irohaNoteNames;
                    additionalNoteName = additionalNames[noteInOctave];
                }

                return $"{noteNames[noteInOctave]}{octave}";
            }
        }
    }
}
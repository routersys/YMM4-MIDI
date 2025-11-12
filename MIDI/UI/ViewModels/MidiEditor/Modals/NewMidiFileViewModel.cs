using System;
using System.Windows;
using System.Windows.Input;
using MIDI.UI.Commands;
using NAudio.Midi;

namespace MIDI.UI.ViewModels.MidiEditor.Modals
{
    public class NewMidiFileViewModel : ViewModelBase
    {
        private int _lengthInSeconds = 60;
        public int LengthInSeconds
        {
            get => _lengthInSeconds;
            set => SetField(ref _lengthInSeconds, value);
        }

        private string _errorMessage = "";
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetField(ref _errorMessage, value);
        }

        public ICommand CreateCommand { get; }

        public MidiEventCollection? ResultMidiEvents { get; private set; }
        public long CalculatedTotalTicks { get; private set; } = 0;

        public NewMidiFileViewModel()
        {
            CreateCommand = new RelayCommand(Create);
        }

        private void Create(object? parameter)
        {
            if (parameter is not Window window) return;

            if (LengthInSeconds <= 0)
            {
                ErrorMessage = "長さは0より大きい必要があります。";
                return;
            }

            ErrorMessage = "";

            const int ticksPerQuarterNote = 480;
            const int tempoBpm = 120;
            const int microsecondsPerQuarterNote = 60000000 / tempoBpm;
            const int timeSignatureNumerator = 4;
            const int timeSignatureDenominator = 4;

            var midiEvents = new MidiEventCollection(1, ticksPerQuarterNote);
            midiEvents.AddTrack();
            midiEvents[0].Add(new TimeSignatureEvent(0, timeSignatureNumerator, timeSignatureDenominator, 24, 8));
            midiEvents[0].Add(new TempoEvent(microsecondsPerQuarterNote, 0));

            double secondsPerTick = (double)microsecondsPerQuarterNote / 1000000.0 / ticksPerQuarterNote;
            CalculatedTotalTicks = (long)(LengthInSeconds / secondsPerTick);

            midiEvents[0].Add(new MetaEvent(MetaEventType.EndTrack, 0, CalculatedTotalTicks));

            ResultMidiEvents = midiEvents;

            window.DialogResult = true;
            window.Close();
        }
    }
}

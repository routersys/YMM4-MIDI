using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using MIDI.Configuration.Models;
using MIDI.UI.Commands;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class MetronomeViewModel : ViewModelBase
    {
        private readonly PlaybackService _playbackService;

        public ICommand ToggleMetronomeCommand { get; }
        public ICommand BeginEditTempoCommand { get; }
        public ICommand EndEditTempoCommand { get; }

        public bool IsEnabled
        {
            get => MidiEditorSettings.Default.Metronome.MetronomeEnabled;
            set
            {
                if (MidiEditorSettings.Default.Metronome.MetronomeEnabled != value)
                {
                    MidiEditorSettings.Default.Metronome.MetronomeEnabled = value;
                    MidiEditorSettings.Default.Save();
                    OnPropertyChanged();
                }
            }
        }

        private bool _autoTempoChange = true;
        public bool AutoTempoChange
        {
            get => _autoTempoChange;
            set => SetField(ref _autoTempoChange, value);
        }

        public double Volume
        {
            get => MidiEditorSettings.Default.Metronome.MetronomeVolume;
            set
            {
                if (MidiEditorSettings.Default.Metronome.MetronomeVolume != value)
                {
                    MidiEditorSettings.Default.Metronome.MetronomeVolume = value;
                    MidiEditorSettings.Default.Save();
                    OnPropertyChanged();
                }
            }
        }

        private int _beatsPerMeasure = 4;
        public int BeatsPerMeasure
        {
            get => _beatsPerMeasure;
            set => SetField(ref _beatsPerMeasure, value);
        }

        private int _beatValue = 4;
        public int BeatValue
        {
            get => _beatValue;
            set => SetField(ref _beatValue, value);
        }

        private double _tempo = 120;
        public double Tempo
        {
            get => _tempo;
            set
            {
                var valueToSet = value;
                if (!IsTempoInEditMode)
                {
                    valueToSet = Math.Max(10, Math.Min(380, value));
                }

                if (SetField(ref _tempo, valueToSet))
                {
                    OnPropertyChanged(nameof(PendulumDuration));
                    var newWeightPos = (380.0 - _tempo) / 370.0 * 150.0;
                    SetField(ref _weightPosition, newWeightPos, nameof(WeightPosition));
                }
            }
        }

        public double PendulumDuration => 60.0 / Tempo;

        private bool _isTempoInEditMode;
        public bool IsTempoInEditMode
        {
            get => _isTempoInEditMode;
            set
            {
                if (SetField(ref _isTempoInEditMode, value) && !value)
                {
                    Tempo = Math.Max(10, Math.Min(380, Tempo));
                }
            }
        }

        private double _weightPosition;
        public double WeightPosition
        {
            get => _weightPosition;
            set
            {
                var clampedValue = Math.Max(0, Math.Min(150, value));
                if (SetField(ref _weightPosition, clampedValue))
                {
                    Tempo = 380.0 - (_weightPosition / 150.0) * 370.0;
                }
            }
        }

        public MetronomeViewModel(PlaybackService playbackService)
        {
            _playbackService = playbackService;
            ToggleMetronomeCommand = new RelayCommand(_ => IsEnabled = !IsEnabled);
            BeginEditTempoCommand = new RelayCommand(_ => IsTempoInEditMode = true);
            EndEditTempoCommand = new RelayCommand(_ => IsTempoInEditMode = false);

            _weightPosition = (380.0 - Tempo) / 370.0 * 150.0;
        }

        public void UpdateMetronome(int beatsPerMeasure, int beatValue, double tempo)
        {
            BeatsPerMeasure = beatsPerMeasure;
            BeatValue = beatValue;
            Tempo = tempo;
        }
    }
}
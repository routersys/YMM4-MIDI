using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MIDI.Configuration.Models
{
    public class MidiSettings : INotifyPropertyChanged
    {
        private int _defaultTempo = 500000;
        public int DefaultTempo { get => _defaultTempo; set => SetField(ref _defaultTempo, value); }

        private double _pitchBendRange = 2.0;
        public double PitchBendRange { get => _pitchBendRange; set => SetField(ref _pitchBendRange, value); }

        private int _minVelocity = 1;
        public int MinVelocity { get => _minVelocity; set => SetField(ref _minVelocity, value); }

        private bool _processControlChanges = true;
        public bool ProcessControlChanges { get => _processControlChanges; set => SetField(ref _processControlChanges, value); }

        private bool _processPitchBend = true;
        public bool ProcessPitchBend { get => _processPitchBend; set => SetField(ref _processPitchBend, value); }

        private bool _processProgramChanges = true;
        public bool ProcessProgramChanges { get => _processProgramChanges; set => SetField(ref _processProgramChanges, value); }

        private ObservableCollection<int> _excludedChannels = new();
        public ObservableCollection<int> ExcludedChannels { get => _excludedChannels; set => SetField(ref _excludedChannels, value); }

        private ObservableCollection<int> _excludedTracks = new();
        public ObservableCollection<int> ExcludedTracks { get => _excludedTracks; set => SetField(ref _excludedTracks, value); }

        public void CopyFrom(MidiSettings source)
        {
            DefaultTempo = source.DefaultTempo;
            PitchBendRange = source.PitchBendRange;
            MinVelocity = source.MinVelocity;
            ProcessControlChanges = source.ProcessControlChanges;
            ProcessPitchBend = source.ProcessPitchBend;
            ProcessProgramChanges = source.ProcessProgramChanges;
            ExcludedChannels = new ObservableCollection<int>(source.ExcludedChannels);
            ExcludedTracks = new ObservableCollection<int>(source.ExcludedTracks);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            return true;
        }
    }
}
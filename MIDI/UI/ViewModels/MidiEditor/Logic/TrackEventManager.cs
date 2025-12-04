using System.ComponentModel;
using System.Globalization;
using System.Windows.Data;

namespace MIDI.UI.ViewModels.MidiEditor.Logic
{
    public class TrackEventManager
    {
        private readonly MidiEditorViewModel _vm;

        public ICollectionView FilteredControlEvents { get; }
        public ICollectionView FilteredTempoEvents { get; }

        public TrackEventManager(MidiEditorViewModel vm)
        {
            _vm = vm;

            FilteredControlEvents = CollectionViewSource.GetDefaultView(_vm.ControlChangeEvents);
            FilteredControlEvents.Filter = FilterCcEvents;

            FilteredTempoEvents = CollectionViewSource.GetDefaultView(_vm.TempoEvents);
            FilteredTempoEvents.Filter = FilterTempoEvents;
        }

        private bool FilterCcEvents(object item)
        {
            if (string.IsNullOrEmpty(_vm.CcSearchText))
            {
                (item as ControlChangeEventViewModel)!.IsMatch = false;
                return true;
            }

            if (item is ControlChangeEventViewModel vm)
            {
                var searchText = _vm.CcSearchText.ToLower();
                bool isMatch = vm.AbsoluteTime.ToString().Contains(searchText) ||
                               vm.Channel.ToString().Contains(searchText) ||
                               vm.Controller.ToString().ToLower().Contains(searchText) ||
                               vm.ControllerValue.ToString().Contains(searchText);
                vm.IsMatch = isMatch;
                return isMatch;
            }
            return false;
        }

        private bool FilterTempoEvents(object item)
        {
            if (string.IsNullOrEmpty(_vm.TempoSearchText))
            {
                (item as TempoEventViewModel)!.IsMatch = false;
                return true;
            }

            if (item is TempoEventViewModel vm)
            {
                var searchText = _vm.TempoSearchText.ToLower();
                bool isMatch = false;
                if (decimal.TryParse(searchText, out var searchDecimal))
                {
                    var culture = CultureInfo.InvariantCulture;
                    int decimalPlaces = (searchText.Contains('.')) ? searchText.Length - searchText.IndexOf('.') - 1 : 0;
                    isMatch = Math.Round(vm.Bpm, decimalPlaces).ToString(culture).Contains(searchDecimal.ToString(culture));
                }
                else
                {
                    isMatch = vm.AbsoluteTime.ToString().Contains(searchText);
                }
                vm.IsMatch = isMatch;
                return isMatch;
            }
            return false;
        }

        public void AddTempoEvent()
        {
            if (_vm.MidiFile == null) return;
            var newTempo = new NAudio.Midi.TempoEvent(500000, _vm.ViewManager.TimeToTicks(_vm.CurrentTime));
            if (_vm.TempoEvents.Any())
            {
                newTempo.MicrosecondsPerQuarterNote = (int)(60000000 / _vm.TempoEvents.Average(t => t.Bpm));
            }
            var vm = new TempoEventViewModel(newTempo);
            vm.PlaybackPropertyChanged += _vm.OnPlaybackPropertyChanged;
            _vm.TempoEvents.Add(vm);
            _vm.MidiFile.Events[0].Add(newTempo);
            SortAndRefreshTempoTrack();
        }

        public void RemoveTempoEvent(TempoEventViewModel? vm)
        {
            if (vm == null || _vm.MidiFile == null) return;
            vm.PlaybackPropertyChanged -= _vm.OnPlaybackPropertyChanged;
            _vm.TempoEvents.Remove(vm);
            _vm.MidiFile.Events[0].Remove(vm.TempoEvent);
            SortAndRefreshTempoTrack();
        }

        public void AddControlChangeEvent()
        {
            if (_vm.MidiFile == null) return;
            var newCC = new NAudio.Midi.ControlChangeEvent(_vm.ViewManager.TimeToTicks(_vm.CurrentTime), 1, NAudio.Midi.MidiController.MainVolume, 100);
            var vm = new ControlChangeEventViewModel(newCC);
            vm.PlaybackPropertyChanged += _vm.OnPlaybackPropertyChanged;
            _vm.ControlChangeEvents.Add(vm);

            var track = _vm.MidiFile.Events.Tracks > 1 ? 1 : 0;
            _vm.MidiFile.Events[track].Add(newCC);

            SortAndRefreshCCTrack();
        }

        public void RemoveControlChangeEvent(ControlChangeEventViewModel? vm)
        {
            if (vm == null || _vm.MidiFile == null) return;
            vm.PlaybackPropertyChanged -= _vm.OnPlaybackPropertyChanged;
            _vm.ControlChangeEvents.Remove(vm);
            foreach (var track in _vm.MidiFile.Events)
            {
                track.Remove(vm.ControlChangeEvent);
            }
            SortAndRefreshCCTrack();
        }

        private void SortAndRefreshTempoTrack()
        {
            var sorted = _vm.TempoEvents.OrderBy(e => e.AbsoluteTime).ToList();
            _vm.TempoEvents.Clear();
            foreach (var item in sorted) _vm.TempoEvents.Add(item);
            UpdateNotePositionsAndDurations();
            _vm.UpdatePlaybackMidiData();
        }

        private void UpdateNotePositionsAndDurations()
        {
            if (_vm.MidiFile == null) return;
            var newTempoMap = MidiProcessor.ExtractTempoMap(_vm.MidiFile, MidiConfiguration.Default);
            foreach (var note in _vm.AllNotes)
            {
                note.RecalculateTimes(newTempoMap);
            }
            _vm.ViewManager.UpdateTimeRuler();
            _vm.RequestRedraw(true);
        }

        private void SortAndRefreshCCTrack()
        {
            var sorted = _vm.ControlChangeEvents.OrderBy(e => e.AbsoluteTime).ToList();
            _vm.ControlChangeEvents.Clear();
            foreach (var item in sorted) _vm.ControlChangeEvents.Add(item);
            _vm.UpdatePlaybackMidiData();
        }

        public void RefreshFiltering()
        {
            FilteredControlEvents.Refresh();
            FilteredTempoEvents.Refresh();
        }
    }
}
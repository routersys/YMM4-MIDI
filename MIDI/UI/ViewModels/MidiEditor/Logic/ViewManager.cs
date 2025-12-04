using MIDI.UI.Views.MidiEditor.Modals;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MIDI.UI.ViewModels.MidiEditor.Logic
{
    public class ViewManager
    {
        private readonly MidiEditorViewModel _vm;

        public ViewManager(MidiEditorViewModel vm)
        {
            _vm = vm;
        }

        public void UpdatePianoRollSize()
        {
            if (_vm.MidiFile == null)
            {
                _vm.PianoRollWidth = 3000;
            }
            else
            {
                _vm.PianoRollWidth = Math.Max(3000, _vm.MaxTime.TotalSeconds * _vm.HorizontalZoom);
            }
            _vm.PianoRollHeight = (_vm.MaxNoteNumber + 1) * 20.0 * _vm.VerticalZoom / _vm.KeyYScale;

            _vm.OnPropertyChanged(nameof(_vm.PianoRollWidth));
            _vm.OnPropertyChanged(nameof(_vm.PianoRollHeight));

            _vm.PianoRollRenderer.InitializeBitmap();
        }

        public void UpdateTimeRuler()
        {
            _vm.TimeRuler.Clear();
            if (_vm.MidiFile == null) return;

            var tempoMap = MidiProcessor.ExtractTempoMap(_vm.MidiFile, MidiConfiguration.Default);
            var totalTicks = _vm.MidiFile.Events.SelectMany(t => t).Any() ? _vm.MidiFile.Events.SelectMany(t => t).Max(e => e.AbsoluteTime) : _vm.TicksPerBar * 4;
            var minTotalTicks = _vm.TicksPerBar * _vm.LengthInBars;
            totalTicks = Math.Max(totalTicks, minTotalTicks);

            var totalDuration = MidiProcessor.TicksToTimeSpan(totalTicks, _vm.MidiFile.DeltaTicksPerQuarterNote, tempoMap);
            var pianoRollWidth = Math.Max(3000, totalDuration.TotalSeconds * _vm.HorizontalZoom);

            double intervalSeconds = _vm.TimeRulerInterval;
            if (intervalSeconds <= 0) intervalSeconds = 1.0;

            while (intervalSeconds * _vm.HorizontalZoom < 50)
            {
                intervalSeconds += (_vm.TimeRulerInterval > 0 ? _vm.TimeRulerInterval : 1.0);
            }

            for (double seconds = 0; ; seconds += intervalSeconds)
            {
                var time = TimeSpan.FromSeconds(seconds);
                var ticks = TimeToTicks(time);
                var x = MidiProcessor.TicksToTimeSpan(ticks, _vm.MidiFile.DeltaTicksPerQuarterNote, tempoMap).TotalSeconds * _vm.HorizontalZoom;
                var nextTime = TimeSpan.FromSeconds(seconds + intervalSeconds);
                var nextTicks = TimeToTicks(nextTime);
                var nextX = MidiProcessor.TicksToTimeSpan(nextTicks, _vm.MidiFile.DeltaTicksPerQuarterNote, tempoMap).TotalSeconds * _vm.HorizontalZoom;

                var width = nextX - x;
                _vm.TimeRuler.Add(new TimeRulerViewModel(time, width, x));

                if (time > totalDuration.Add(TimeSpan.FromSeconds(intervalSeconds)) || x > pianoRollWidth + 200) break;
            }
            _vm.OnPropertyChanged(nameof(_vm.PianoRollWidth));
        }

        public async void RenderThumbnail()
        {
            if (!_vm.ShowThumbnail || _vm.MidiFile == null)
            {
                _vm.ThumbnailBitmap = null;
                return;
            }

            int width = 1024;
            int height = 60;
            var notesToRender = _vm.AllNotes.ToList();
            var maxTime = _vm.MaxTime;

            if (!notesToRender.Any())
            {
                var emptyBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
                emptyBitmap.Freeze();
                _vm.ThumbnailBitmap = emptyBitmap;
                return;
            }

            byte[]? pixels = await Task.Run(() =>
            {
                try
                {
                    var pixelData = new byte[width * height * 4];
                    var totalDuration = maxTime.TotalSeconds;
                    if (totalDuration <= 0) return pixelData;

                    int minNote = notesToRender.Min(n => n.NoteNumber);
                    int maxNote = notesToRender.Max(n => n.NoteNumber);
                    int noteRange = Math.Max(1, maxNote - minNote);

                    foreach (var note in notesToRender)
                    {
                        var xStart = (int)((note.StartTime.TotalSeconds / totalDuration) * width);
                        var xEnd = (int)(((note.StartTime + note.Duration).TotalSeconds / totalDuration) * width);
                        var y = height - 1 - (int)(((double)(note.NoteNumber - minNote) / noteRange) * (height - 1));

                        int noteHeight = 1;
                        for (int currentY = y; currentY < y + noteHeight && currentY < height; currentY++)
                        {
                            if (currentY < 0) continue;
                            for (int x = xStart; x < xEnd && x < width; x++)
                            {
                                if (x < 0) continue;
                                int index = (currentY * width + x) * 4;
                                var color = note.Color;
                                pixelData[index] = color.B;
                                pixelData[index + 1] = color.G;
                                pixelData[index + 2] = color.R;
                                pixelData[index + 3] = color.A;
                            }
                        }
                    }
                    return pixelData;
                }
                catch { return null; }
            });

            if (pixels != null)
            {
                var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
                bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
                bitmap.Freeze();
                _vm.ThumbnailBitmap = bitmap;
            }
        }

        public void OpenDisplaySettings()
        {
            var displaySettingsWindow = new DisplaySettingsWindow { Owner = Application.Current.MainWindow };
            var vm = displaySettingsWindow.ViewModel;
            vm.SelectedGridOption = _vm.GridQuantizeValue;
            vm.SelectedTimeRulerInterval = _vm.TimeRulerInterval.ToString();

            if (displaySettingsWindow.ShowDialog() == true)
            {
                _vm.GridQuantizeValue = vm.SelectedGridOption;
                _vm.TimeRulerInterval = int.Parse(vm.SelectedTimeRulerInterval);
            }
        }

        public double GetTicksPerGrid()
        {
            if (_vm.MidiFile == null) return 0;
            double beatDurationDenom;
            var parts = _vm.GridQuantizeValue.Split('/');
            if (parts.Length == 2 && double.TryParse(parts[1].Replace("T", ""), out double denom)) beatDurationDenom = denom;
            else beatDurationDenom = 16;
            if (beatDurationDenom == 0) beatDurationDenom = 16;
            bool isTriplet = _vm.GridQuantizeValue.Contains('T');
            double ticksPerGrid = (_vm.MidiFile.DeltaTicksPerQuarterNote * 4) / beatDurationDenom;
            if (isTriplet) ticksPerGrid *= 2.0 / 3.0;
            return ticksPerGrid;
        }

        public TimeSpan PositionToTime(double x)
        {
            if (_vm.MidiFile == null || _vm.HorizontalZoom == 0) return TimeSpan.Zero;
            var timeInSeconds = x / _vm.HorizontalZoom;
            return TimeSpan.FromSeconds(timeInSeconds);
        }

        public long TimeToTicks(TimeSpan time)
        {
            if (_vm.MidiFile == null) return 0;
            var tempoMap = MidiProcessor.ExtractTempoMap(_vm.MidiFile, MidiConfiguration.Default);
            if (!tempoMap.Any())
            {
                double secondsPerTick = 0.5 / _vm.MidiFile.DeltaTicksPerQuarterNote;
                return (long)(time.TotalSeconds / secondsPerTick);
            }

            double accumulatedSeconds = 0;
            long lastTicks = 0;
            double currentTempo = tempoMap[0].MicrosecondsPerQuarterNote;
            double lastTempo = currentTempo;

            foreach (var tempoEvent in tempoMap)
            {
                if (tempoEvent.AbsoluteTime > 0)
                {
                    long deltaTicks = tempoEvent.AbsoluteTime - lastTicks;
                    if (deltaTicks > 0)
                    {
                        double deltaSeconds = (deltaTicks / (double)_vm.MidiFile.DeltaTicksPerQuarterNote) * (lastTempo / 1000000.0);
                        if (accumulatedSeconds + deltaSeconds >= time.TotalSeconds)
                        {
                            double secondsIntoSegment = time.TotalSeconds - accumulatedSeconds;
                            long ticksIntoSegment = (long)(secondsIntoSegment * (1000000.0 / lastTempo) * _vm.MidiFile.DeltaTicksPerQuarterNote);
                            return lastTicks + ticksIntoSegment;
                        }
                        accumulatedSeconds += deltaSeconds;
                    }
                }
                lastTicks = tempoEvent.AbsoluteTime;
                lastTempo = tempoEvent.MicrosecondsPerQuarterNote;
            }
            double remainingSeconds = time.TotalSeconds - accumulatedSeconds;
            long remainingTicks = (long)(remainingSeconds * (1000000.0 / lastTempo) * _vm.MidiFile.DeltaTicksPerQuarterNote);
            return lastTicks + remainingTicks;
        }

        public TimeSpan TicksToTime(long ticks)
        {
            if (_vm.MidiFile == null) return TimeSpan.Zero;
            var tempoMap = MidiProcessor.ExtractTempoMap(_vm.MidiFile, MidiConfiguration.Default);
            return MidiProcessor.TicksToTimeSpan(ticks, _vm.MidiFile.DeltaTicksPerQuarterNote, tempoMap);
        }

        public void ZoomToSelection()
        {
            if (!_vm.SelectedNotes.Any()) return;
            var minTick = _vm.SelectedNotes.Min(n => n.StartTicks);
            var maxTick = _vm.SelectedNotes.Max(n => n.StartTicks + n.DurationTicks);
            var minNote = _vm.SelectedNotes.Min(n => n.NoteNumber);
            var maxNote = _vm.SelectedNotes.Max(n => n.NoteNumber);

            var startTime = TicksToTime(minTick);
            var endTime = TicksToTime(maxTick);
            var duration = endTime - startTime;

            if (duration.TotalSeconds > 0) _vm.HorizontalZoom = _vm.ViewportWidth / duration.TotalSeconds;

            var noteHeight = 20.0 / _vm.KeyYScale;
            var requiredHeight = (maxNote - minNote + 1) * noteHeight * _vm.VerticalZoom;
            if (requiredHeight > 0) _vm.VerticalZoom *= _vm.ViewportHeight / requiredHeight;
        }
    }
}
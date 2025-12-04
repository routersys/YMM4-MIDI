using System.Windows.Threading;

namespace MIDI.UI.ViewModels.MidiEditor.Logic
{
    public class PlaybackControlManager
    {
        private readonly MidiEditorViewModel _vm;
        private readonly DispatcherTimer _uiUpdateTimer;
        private readonly DispatcherTimer _seekDelayTimer;

        public PlaybackControlManager(MidiEditorViewModel vm)
        {
            _vm = vm;

            _uiUpdateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            _uiUpdateTimer.Tick += UiUpdateTimer_Tick;

            _seekDelayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _seekDelayTimer.Tick += (s, e) => {
                _seekDelayTimer.Stop();
                _vm.PlaybackService.Seek(_vm.PlaybackService.CurrentTime);
            };
        }

        private void UiUpdateTimer_Tick(object? sender, EventArgs e)
        {
            _vm.OnPropertyChanged(nameof(_vm.CurrentTime));
            _vm.OnPropertyChanged(nameof(_vm.PlaybackCursorPosition));

            if (_vm.EditorSettings.View.LightUpKeyboardDuringPlayback)
            {
                var activeNotes = _vm.AllNotes.AsParallel()
                    .Where(n => n.StartTime <= _vm.CurrentTime && n.StartTime + n.Duration > _vm.CurrentTime)
                    .Select(n => n.NoteNumber)
                    .ToList();
                _vm.OnPlayingNotesChanged(activeNotes);
            }
        }

        public void PlayPause()
        {
            _vm.PlaybackService.PlayPause();
            if (_vm.PlaybackService.IsPlaying)
            {
                _uiUpdateTimer.Start();
                _vm.StatusText = "再生中";
            }
            else
            {
                _uiUpdateTimer.Stop();
                _vm.OnPlayingNotesChanged(Enumerable.Empty<int>());
                _vm.StatusText = "一時停止";
            }
        }

        public void Stop()
        {
            _vm.PlaybackService.Stop();
            _uiUpdateTimer.Stop();
            _vm.OnPropertyChanged(nameof(_vm.CurrentTime));
            _vm.OnPropertyChanged(nameof(_vm.PlaybackCursorPosition));
            _vm.OnPlayingNotesChanged(Enumerable.Empty<int>());
            _vm.StatusText = "停止";
        }

        public void Rewind()
        {
            _vm.CurrentTime = TimeSpan.Zero;
        }

        public void SetCurrentTimeFromArrowKey(TimeSpan time)
        {
            _vm.PlaybackService.SuppressSeek = true;
            _vm.CurrentTime = time;
            _vm.PlaybackService.SuppressSeek = false;
            _seekDelayTimer.Stop();
            _seekDelayTimer.Start();
        }

        public void Cleanup()
        {
            _uiUpdateTimer.Tick -= UiUpdateTimer_Tick;
            _uiUpdateTimer.Stop();
            _seekDelayTimer.Stop();
        }
    }
}
using MIDI.UI.ViewModels.MidiEditor;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;

namespace MIDI.UI.ViewModels
{
    public partial class MidiEditorViewModel : IDisposable
    {
        private bool _isDisposed;
        private DispatcherTimer _zoomTimer = new DispatcherTimer();
        private DispatcherTimer _backupTimer = new DispatcherTimer();

        private void InitializeInfrastructure()
        {
            _zoomTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _zoomTimer.Tick += (s, e) => {
                _zoomTimer.Stop();
                OnPropertyChanged(nameof(HorizontalZoom));
                OnPropertyChanged(nameof(VerticalZoom));
                ViewManager.UpdatePianoRollSize();
                OnPropertyChanged(nameof(PlaybackCursorPosition));
                ViewManager.UpdateTimeRuler();
            };

            _backupTimer = new DispatcherTimer();
            _backupTimer.Tick += (s, e) => { if (EditorSettings.Backup.EnableAutoBackup) _ = MidiFileIOService.SaveProjectAsync(null, true); };
            UpdateBackupTimer();

            PlaybackService.AudioChunkRendered += OnAudioChunkRendered;
            PlaybackService.ParentViewModel.Metronome.PropertyChanged += PlaybackService.Metronome_PropertyChanged;

            EditorSettings.Note.PropertyChanged += Editor_Note_PropertyChanged;
            EditorSettings.Flag.PropertyChanged += Editor_Flag_PropertyChanged;
        }

        public void CheckForBackup()
        {
            var abnormalShutdownFlag = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "backup", "editor", ".abnormal_shutdown");
            if (!File.Exists(abnormalShutdownFlag)) return;

            File.Delete(abnormalShutdownFlag);

            var backupDir = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "backup", "editor");
            if (!Directory.Exists(backupDir)) return;

            var latestBackup = new DirectoryInfo(backupDir)
                .GetFiles("*.ymidi")
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();

            if (latestBackup != null)
            {
                var result = MessageBox.Show($"前回のセッションは異常終了したようです。バックアップを読み込みますか？\nバックアップ時刻: {latestBackup.LastWriteTime}", "バックアップの復元", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.Yes)
                {
                    _ = MidiFileIOService.LoadMidiDataAsync(latestBackup.FullName);
                }
            }
        }

        public void OnScrollChanged(double horizontalOffset, double viewportWidth, double verticalOffset, double viewportHeight)
        {
            HorizontalOffset = horizontalOffset;
            ViewportWidth = viewportWidth;
            VerticalOffset = verticalOffset;
            ViewportHeight = viewportHeight;
            VerticalScrollOffset = verticalOffset;
            OnPropertyChanged(nameof(VerticalScrollOffset));
            PianoRollRenderer.UpdateVisibleRect();
            PianoRollRenderer.RequestRedraw(true);
        }

        public void HandleKeyDown(System.Windows.Input.Key key) => InputEventManager.HandleKeyDown(key);
        public void HandleKeyUp(System.Windows.Input.Key key) => InputEventManager.HandleKeyUp(key);
        public void OnPianoRollMouseDown(Point position, System.Windows.Input.MouseButtonEventArgs e) => MouseHandler.OnPianoRollMouseDown(position, e);
        public void OnPianoRollMouseUp(System.Windows.Input.MouseButtonEventArgs e) => MouseHandler.OnPianoRollMouseUp(e);
        public void OnPianoRollMouseMove(Point position, System.Windows.Input.MouseEventArgs e) => MouseHandler.OnPianoRollMouseMove(position, e);
        public void OnTimeBarMouseDown(Point position) => MouseHandler.OnTimeBarMouseDown(position);
        public void OnTimeBarMouseMove(Point position) => MouseHandler.OnTimeBarMouseMove(position);
        public void OnTimeBarMouseUp() => MouseHandler.OnTimeBarMouseUp();

        public void UpdateBackupTimer()
        {
            if (EditorSettings.Backup.EnableAutoBackup)
            {
                _backupTimer.Interval = TimeSpan.FromMinutes(EditorSettings.Backup.BackupIntervalMinutes);
                _backupTimer.Start();
            }
            else _backupTimer.Stop();
        }

        public void OnPlaybackPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (MidiFile == null) return;
            var newTempoMap = MidiProcessor.ExtractTempoMap(MidiFile, MidiConfiguration.Default);
            foreach (var note in AllNotes) note.RecalculateTimes(newTempoMap);
            ViewManager.UpdateTimeRuler();
            PianoRollRenderer.RequestRedraw(true);
            UpdatePlaybackMidiData();
        }

        private void OnAudioChunkRendered(ReadOnlySpan<float> audioBuffer)
        {
            var bufferCopy = new float[audioBuffer.Length];
            audioBuffer.CopyTo(bufferCopy);
            Task.Run(() => {
                _audioMeter.Process(bufferCopy);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    SoundPeakViewModel.UpdateValues(
                        _audioMeter.PeakLeft, _audioMeter.PeakRight,
                        _audioMeter.RmsLeft, _audioMeter.RmsRight,
                        _audioMeter.VuLeft, _audioMeter.VuRight,
                        _audioMeter.MomentaryLoudness, _audioMeter.ShortTermLoudness, _audioMeter.IntegratedLoudness
                    );
                });
            });
        }

        private void Editor_Note_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(EditorSettings.Note.NoteColor)) ResetNoteColorCommand.Execute(null);
            if (e.PropertyName == nameof(EditorSettings.Note.SelectedNoteColor))
            {
                foreach (var note in SelectedNotes) PianoRollRenderer.RequestNoteRedraw(note);
            }
        }
        private void Editor_Flag_PropertyChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(EditorSettings));

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            EditorSettings.Note.PropertyChanged -= Editor_Note_PropertyChanged;
            EditorSettings.Flag.PropertyChanged -= Editor_Flag_PropertyChanged;

            _zoomTimer.Stop();
            _backupTimer.Stop();
            PlaybackControlManager.Cleanup();

            PlaybackService.AudioChunkRendered -= OnAudioChunkRendered;
            PlaybackService.Dispose();
            MidiInputService.Dispose();
            PianoRollRenderer.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}
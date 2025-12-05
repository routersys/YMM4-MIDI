using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using MeltySynth;
using NAudio.Wave;
using System.Linq;
using System.Collections.Generic;
using NAudioMidi = NAudio.Midi;
using System.Windows.Threading;
using System.ComponentModel;
using MIDI.Configuration.Models;
using MIDI;

namespace MIDI.UI.ViewModels.MidiEditor
{
    public class PlaybackService : ViewModelBase, IDisposable
    {
        private MeltySynth.MidiFile? _meltyMidiFile;
        private IWavePlayer? _waveOut;
        private Synthesizer? _sequencerSynthesizer;
        private Synthesizer? _keyboardSynthesizer;
        private bool _isDisposed;
        private Task? _playbackTask;
        private CancellationTokenSource? _playbackCts;
        private SynthesizerWaveProvider? _waveProvider;
        private bool _isScrubbing = false;
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private TimeSpan _timeAtLastUpdate;
        private double _nextBeatTime = 0;
        public event Action<int>? Beat;
        public event Action<ReadOnlySpan<float>>? AudioChunkRendered;
        private readonly object _sequencerLock = new object();
        private bool _isSeeking = false;
        private ManualResetEventSlim _audioStartedEvent = new ManualResetEventSlim(false);

        private List<PlaybackStateSnapshot> _checkpoints = new List<PlaybackStateSnapshot>();
        private List<TimedMidiEvent> _sortedNaudioEvents = new List<TimedMidiEvent>();
        private static readonly TimeSpan CheckpointInterval = TimeSpan.FromSeconds(5);

        private class PlaybackStateSnapshot
        {
            public TimeSpan Time { get; }
            public ChannelStateSnapshot[] ChannelStates { get; } = new ChannelStateSnapshot[16];
            public HashSet<(int Channel, int NoteNumber)> ActiveNotes { get; } = new HashSet<(int Channel, int NoteNumber)>();
            public Dictionary<(int Channel, int NoteNumber), TimeSpan> ActiveNoteStartTimes { get; } = new Dictionary<(int Channel, int NoteNumber), TimeSpan>();

            public PlaybackStateSnapshot(TimeSpan time)
            {
                Time = time;
                for (int i = 0; i < 16; i++)
                {
                    ChannelStates[i] = new ChannelStateSnapshot();
                }
            }
        }

        private class ChannelStateSnapshot
        {
            public int Program { get; set; }
            public int BankSelectMsb { get; set; }
            public int BankSelectLsb { get; set; }
            public int Volume { get; set; } = 100;
            public int Pan { get; set; } = 64;
            public int Expression { get; set; } = 127;
            public int PitchBend { get; set; } = 8192;
            public bool Sustain { get; set; }
        }

        private class TimedMidiEvent
        {
            public TimeSpan Time { get; }
            public NAudioMidi.MidiEvent Event { get; }
            public TimedMidiEvent(TimeSpan time, NAudioMidi.MidiEvent midiEvent)
            {
                Time = time;
                Event = midiEvent;
            }
        }

        private TimeSpan _currentTime;
        public TimeSpan CurrentTime
        {
            get => _currentTime;
            set
            {
                if (SetField(ref _currentTime, value))
                {
                    if (_sequencerSynthesizer != null && !IsPlaying && !_isScrubbing && !SuppressSeek)
                    {
                        Seek(value);
                    }
                    if (IsLooping && (value < LoopStart || value >= LoopEnd))
                    {
                        SetLoop(false, TimeSpan.Zero, TimeSpan.Zero);
                    }
                }
            }
        }

        public bool SuppressSeek { get; set; } = false;

        public TimeSpan GetInterpolatedTime()
        {
            if (IsPlaying)
                return _timeAtLastUpdate + _stopwatch.Elapsed;
            return _currentTime;
        }

        private bool _isPlaying;
        public bool IsPlaying { get => _isPlaying; internal set => SetField(ref _isPlaying, value); }
        public Synthesizer? Synthesizer => _keyboardSynthesizer;
        public MidiEditorViewModel ParentViewModel { get; }
        public double MasterVolume
        {
            get => _sequencerSynthesizer?.MasterVolume ?? 1.0;
            set
            {
                if (_sequencerSynthesizer != null)
                {
                    _sequencerSynthesizer.MasterVolume = (float)value;
                }
                if (_keyboardSynthesizer != null)
                {
                    _keyboardSynthesizer.MasterVolume = (float)value;
                }
                OnPropertyChanged();
            }
        }

        public bool IsLooping { get; private set; }
        public TimeSpan LoopStart { get; private set; }
        public TimeSpan LoopEnd { get; private set; }

        public PlaybackService(MidiEditorViewModel parentViewModel)
        {
            ParentViewModel = parentViewModel;
        }

        public void Metronome_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MetronomeViewModel.Tempo) && IsPlaying)
            {
                var secondsPerBeat = 60.0 / ParentViewModel.Metronome.Tempo;
                if (secondsPerBeat > 0)
                {
                    _nextBeatTime = (Math.Floor(_currentTime.TotalSeconds / secondsPerBeat) + 1) * secondsPerBeat;
                }
            }
        }

        public void LoadMidiFile(string filePath)
        {
            if (!File.Exists(filePath)) return;
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                LoadMidiData(stream);
            }
        }

        public void LoadMidiData(Stream stream, TimeSpan? startTime = null)
        {
            var wasPlaying = IsPlaying;

            if (wasPlaying)
            {
                _playbackCts?.Cancel();
                _waveOut?.Pause();
                IsPlaying = false;
                _stopwatch.Stop();
            }

            MeltySynth.MidiFile? newMeltyMidiFile = null;
            NAudioMidi.MidiFile? newNaudioMidiFile = null;

            try
            {
                stream.Position = 0;
                var midiBytes = new byte[stream.Length];
                int bytesRead = 0;
                int totalBytesRead = 0;
                while (totalBytesRead < midiBytes.Length && (bytesRead = stream.Read(midiBytes, totalBytesRead, midiBytes.Length - totalBytesRead)) > 0)
                {
                    totalBytesRead += bytesRead;
                }

                try
                {
                    newMeltyMidiFile = new MeltySynth.MidiFile(new MemoryStream(midiBytes));
                }
                catch
                {
                    using var memoryStream = new MemoryStream();
                    var writer = new BinaryWriter(memoryStream);
                    writer.Write(new byte[] { 0x4d, 0x54, 0x68, 0x64 });
                    writer.Write(BitConverter.GetBytes(6).Reverse().ToArray());
                    writer.Write(BitConverter.GetBytes((ushort)0).Reverse().ToArray());
                    writer.Write(BitConverter.GetBytes((ushort)1).Reverse().ToArray());
                    writer.Write(BitConverter.GetBytes((ushort)480).Reverse().ToArray());
                    writer.Write(new byte[] { 0x4d, 0x54, 0x72, 0x6b });
                    writer.Write(BitConverter.GetBytes(4).Reverse().ToArray());
                    writer.Write(new byte[] { 0x00, 0xFF, 0x2F, 0x00 });
                    memoryStream.Position = 0;
                    newMeltyMidiFile = new MeltySynth.MidiFile(memoryStream);
                }

                try
                {
                    newNaudioMidiFile = new NAudioMidi.MidiFile(new MemoryStream(midiBytes), false);
                }
                catch
                {
                    newNaudioMidiFile = null;
                }
            }
            catch (Exception)
            {
                using var memoryStream = new MemoryStream();
                var writer = new BinaryWriter(memoryStream);
                writer.Write(new byte[] { 0x4d, 0x54, 0x68, 0x64 });
                writer.Write(BitConverter.GetBytes(6).Reverse().ToArray());
                writer.Write(BitConverter.GetBytes((ushort)0).Reverse().ToArray());
                writer.Write(BitConverter.GetBytes((ushort)1).Reverse().ToArray());
                writer.Write(BitConverter.GetBytes((ushort)480).Reverse().ToArray());
                writer.Write(new byte[] { 0x4d, 0x54, 0x72, 0x6b });
                writer.Write(BitConverter.GetBytes(4).Reverse().ToArray());
                writer.Write(new byte[] { 0x00, 0xFF, 0x2F, 0x00 });
                memoryStream.Position = 0;
                newMeltyMidiFile = new MeltySynth.MidiFile(memoryStream);
            }

            lock (_sequencerLock)
            {
                _meltyMidiFile = newMeltyMidiFile;
                if (newNaudioMidiFile != null)
                {
                    GenerateCheckpoints(newNaudioMidiFile);
                }
                else
                {
                    _checkpoints.Clear();
                    _sortedNaudioEvents.Clear();
                    _checkpoints.Add(new PlaybackStateSnapshot(TimeSpan.Zero));
                }
            }

            var targetTime = startTime ?? TimeSpan.Zero;

            if (_sequencerSynthesizer != null)
            {
                lock (_sequencerLock)
                {
                    _sequencerSynthesizer.Reset();
                }
                Seek(targetTime);
            }

            _currentTime = targetTime;
            ParentViewModel.CurrentTime = targetTime;

            if (wasPlaying)
            {
                PlayPause();
            }
        }

        public void InitializePlayback(string? soundFontName = null)
        {
            if (_sequencerSynthesizer != null && string.IsNullOrEmpty(soundFontName) && _waveOut != null) return;

            try
            {
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var sfDir = Path.Combine(assemblyLocation ?? "", MidiConfiguration.Default.SoundFont.DefaultSoundFontDirectory);

                var effectiveSoundFontName = soundFontName ?? MidiConfiguration.Default.SoundFont.PreferredSoundFont;
                if (string.IsNullOrEmpty(effectiveSoundFontName))
                {
                    effectiveSoundFontName = "GeneralUser-GS.sf2";
                }

                var soundfontPath = Path.Combine(sfDir, effectiveSoundFontName);

                if (!File.Exists(soundfontPath))
                {
                    var defaultPath = Path.Combine(assemblyLocation ?? "", "GeneralUser-GS.sf2");
                    if (File.Exists(defaultPath))
                    {
                        soundfontPath = defaultPath;
                    }
                    else
                    {
                        MessageBox.Show($"再生用のSoundFont '{effectiveSoundFontName}' が見つかりません。");
                        return;
                    }
                }

                var masterVolume = MasterVolume;
                bool wasPlaying = IsPlaying;
                if (wasPlaying) _waveOut?.Pause();

                lock (_sequencerLock)
                {
                    _sequencerSynthesizer = new Synthesizer(soundfontPath, 44100);
                    _keyboardSynthesizer = new Synthesizer(soundfontPath, 44100);
                    MasterVolume = masterVolume;
                }

                if (_meltyMidiFile != null)
                {
                    Seek(CurrentTime);
                }

                if (_waveOut == null)
                {
                    _waveProvider = new SynthesizerWaveProvider(this);
                    _waveOut = new DirectSoundOut(50);
                    _waveOut.Init(_waveProvider);
                    _waveOut.PlaybackStopped += (s, e) =>
                    {
                        if (IsPlaying)
                        {
                            Stop();
                        }
                    };
                }

                if (wasPlaying) _waveOut.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"再生の初期化に失敗しました: {ex.Message}");
            }
        }

        public void PlayPause()
        {
            InitializePlayback();
            if (_waveOut == null || _sequencerSynthesizer == null || _meltyMidiFile == null) return;

            if (IsPlaying)
            {
                _playbackCts?.Cancel();
                _waveOut.Pause();
                IsPlaying = false;
                _stopwatch.Stop();
            }
            else
            {
                IsPlaying = true;
                TimeSpan startTime = CurrentTime;
                if (IsLooping && (startTime < LoopStart || startTime >= LoopEnd))
                {
                    startTime = LoopStart;
                }

                Seek(startTime);
                _currentTime = startTime;
                _timeAtLastUpdate = _currentTime;

                _waveProvider?.SetSamplePosition((long)(_currentTime.TotalSeconds * _sequencerSynthesizer.SampleRate));
                _audioStartedEvent.Reset();

                _waveOut.Play();

                _audioStartedEvent.Wait(TimeSpan.FromMilliseconds(100));

                _stopwatch.Restart();
                var secondsPerBeat = 60.0 / ParentViewModel.Metronome.Tempo;
                if (secondsPerBeat > 0)
                {
                    _nextBeatTime = (Math.Floor(_currentTime.TotalSeconds / secondsPerBeat) + 1) * secondsPerBeat;
                }
                _playbackCts = new CancellationTokenSource();
                _playbackTask = Task.Run(async () => await PlaybackLoopAsync(_playbackCts.Token), _playbackCts.Token);
            }
        }

        private async Task PlaybackLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (_meltyMidiFile == null || _sequencerSynthesizer == null || _waveProvider == null) break;

                var newTime = _timeAtLastUpdate + _stopwatch.Elapsed;

                if (ParentViewModel.Metronome.AutoTempoChange)
                {
                    var currentTicks = ParentViewModel.TimeToTicks(newTime);
                    var currentTempoEvent = ParentViewModel.TempoEvents
                        .Where(t => t.AbsoluteTime <= currentTicks)
                        .OrderByDescending(t => t.AbsoluteTime)
                        .FirstOrDefault();

                    if (currentTempoEvent != null)
                    {
                        double targetBpm = currentTempoEvent.Bpm;
                        if (ParentViewModel.Metronome.Tempo != targetBpm)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() =>
                            {
                                ParentViewModel.Metronome.Tempo = targetBpm;
                            });
                        }
                    }
                }

                if (IsLooping && newTime >= LoopEnd && !_isSeeking)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Seek(LoopStart);
                        _currentTime = LoopStart;
                        _timeAtLastUpdate = LoopStart;
                        _stopwatch.Restart();

                        var secondsPerBeat = 60.0 / ParentViewModel.Metronome.Tempo;
                        if (secondsPerBeat > 0)
                        {
                            _nextBeatTime = (Math.Floor(_currentTime.TotalSeconds / secondsPerBeat) + 1) * secondsPerBeat;
                        }

                    }, DispatcherPriority.Normal, token);
                    continue;
                }

                if (!IsLooping && newTime >= _meltyMidiFile.Length)
                {
                    Application.Current.Dispatcher.Invoke(Stop);
                    break;
                }

                _currentTime = newTime;

                if (MidiEditorSettings.Default.Metronome.MetronomeEnabled)
                {
                    if (_currentTime.TotalSeconds >= _nextBeatTime)
                    {
                        var secondsPerBeat = 60.0 / ParentViewModel.Metronome.Tempo;
                        if (secondsPerBeat > 0)
                        {
                            var currentBeat = (int)Math.Round(_nextBeatTime / secondsPerBeat);
                            var beatIndex = currentBeat % ParentViewModel.Metronome.BeatsPerMeasure;
                            Beat?.Invoke(beatIndex);

                            _keyboardSynthesizer?.NoteOn(9, beatIndex == 0 ? 37 : 76, (int)(100 * MidiEditorSettings.Default.Metronome.MetronomeVolume));

                            _nextBeatTime += secondsPerBeat;
                        }
                        else
                        {
                            _nextBeatTime = double.MaxValue;
                        }
                    }
                }

                try
                {
                    await Task.Delay(5, token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void Stop()
        {
            _playbackCts?.Cancel();
            _waveOut?.Stop();
            lock (_sequencerLock)
            {
                _sequencerSynthesizer?.Reset();
            }
            CurrentTime = TimeSpan.Zero;
            _timeAtLastUpdate = TimeSpan.Zero;
            _stopwatch.Reset();
            if (_waveProvider != null) _waveProvider.SetSamplePosition(0);
            IsPlaying = false;
            _nextBeatTime = 0;
        }

        public void Seek(TimeSpan time)
        {
            if (_isSeeking) return;
            _isSeeking = true;

            try
            {
                lock (_sequencerLock)
                {
                    if (_sequencerSynthesizer is null || _meltyMidiFile is null || ParentViewModel.MidiFile is null) return;

                    _sequencerSynthesizer.Reset();

                    for (int i = 0; i < 16; i++)
                    {
                        _sequencerSynthesizer.ProcessMidiMessage(i, 0xB0, 7, 100);
                        _sequencerSynthesizer.ProcessMidiMessage(i, 0xB0, 11, 127);
                        _sequencerSynthesizer.ProcessMidiMessage(i, 0xB0, 10, 64);
                        _sequencerSynthesizer.ProcessMidiMessage(i, 0xE0, 0, 64);
                        _sequencerSynthesizer.ProcessMidiMessage(i, 0xB0, 64, 0);
                    }

                    var checkpoint = _checkpoints.LastOrDefault(c => c.Time <= time);
                    if (checkpoint == null)
                    {
                        checkpoint = new PlaybackStateSnapshot(TimeSpan.Zero);
                    }

                    foreach (var channelState in checkpoint.ChannelStates.Select((state, index) => new { state, index }))
                    {
                        var ch = channelState.index;
                        var state = channelState.state;
                        _sequencerSynthesizer.ProcessMidiMessage(ch, 0xB0, 0, state.BankSelectMsb);
                        _sequencerSynthesizer.ProcessMidiMessage(ch, 0xB0, 32, state.BankSelectLsb);
                        _sequencerSynthesizer.ProcessMidiMessage(ch, 0xC0, state.Program, 0);
                        _sequencerSynthesizer.ProcessMidiMessage(ch, 0xB0, 7, state.Volume);
                        _sequencerSynthesizer.ProcessMidiMessage(ch, 0xB0, 10, state.Pan);
                        _sequencerSynthesizer.ProcessMidiMessage(ch, 0xB0, 11, state.Expression);
                        _sequencerSynthesizer.ProcessMidiMessage(ch, 0xB0, 64, state.Sustain ? 127 : 0);
                        _sequencerSynthesizer.ProcessMidiMessage(ch, 0xE0, state.PitchBend & 0x7F, state.PitchBend >> 7);
                    }

                    int firstEventIndex = _sortedNaudioEvents.FindIndex(m => m.Time >= checkpoint.Time);
                    if (firstEventIndex == -1) firstEventIndex = 0;

                    var activeNotesAtSeekTime = new HashSet<(int Channel, int NoteNumber)>(checkpoint.ActiveNotes);
                    var activeNoteStartTimesAtSeekTime = new Dictionary<(int Channel, int NoteNumber), TimeSpan>(checkpoint.ActiveNoteStartTimes);


                    for (int i = firstEventIndex; i < _sortedNaudioEvents.Count; i++)
                    {
                        var timedEvent = _sortedNaudioEvents[i];
                        if (timedEvent.Time >= time) break;

                        ProcessNaudioEventOnSynthesizer(timedEvent.Event);

                        var message = timedEvent.Event;
                        if (message.Channel >= 1 && message.Channel <= 16)
                        {
                            int ch = message.Channel - 1;
                            if (message is NAudioMidi.NoteOnEvent noteOn && noteOn.Velocity > 0)
                            {
                                var noteKey = (ch, noteOn.NoteNumber);
                                activeNotesAtSeekTime.Add(noteKey);
                                activeNoteStartTimesAtSeekTime[noteKey] = timedEvent.Time;
                            }
                            else if (message is NAudioMidi.NoteEvent noteOff &&
                                     (message.CommandCode == NAudioMidi.MidiCommandCode.NoteOff || (noteOff is NAudioMidi.NoteOnEvent noe && noe.Velocity == 0)))
                            {
                                var noteKey = (ch, noteOff.NoteNumber);
                                activeNotesAtSeekTime.Remove(noteKey);
                                activeNoteStartTimesAtSeekTime.Remove(noteKey);
                            }
                        }
                    }

                    var currentTempoMap = MidiProcessor.ExtractTempoMap(ParentViewModel.MidiFile, MidiConfiguration.Default);
                    foreach (var noteKey in activeNotesAtSeekTime)
                    {
                        TimeSpan noteStartTime;
                        if (activeNoteStartTimesAtSeekTime.TryGetValue(noteKey, out noteStartTime))
                        {
                            var noteOnEvent = FindNoteOnEvent(_sortedNaudioEvents, noteKey.Channel + 1, noteKey.NoteNumber, noteStartTime);
                            if (noteOnEvent != null && noteOnEvent.OffEvent != null)
                            {
                                var noteOffTime = MidiProcessor.TicksToTimeSpan(noteOnEvent.OffEvent.AbsoluteTime, ParentViewModel.MidiFile.DeltaTicksPerQuarterNote, currentTempoMap);

                                if (noteOffTime >= time)
                                {
                                    _sequencerSynthesizer.NoteOn(noteKey.Channel, noteKey.NoteNumber, noteOnEvent.Velocity);
                                }
                            }
                        }
                    }


                    if (_waveProvider != null)
                    {
                        _waveProvider.SetSamplePosition((long)(time.TotalSeconds * _sequencerSynthesizer.SampleRate));
                    }

                    _timeAtLastUpdate = time;
                    if (IsPlaying) _stopwatch.Restart(); else _stopwatch.Reset();

                    var secondsPerBeat = 60.0 / ParentViewModel.Metronome.Tempo;
                    if (secondsPerBeat > 0)
                    {
                        _nextBeatTime = (Math.Floor(time.TotalSeconds / secondsPerBeat) + 1) * secondsPerBeat;
                    }
                    _currentTime = time;
                    OnPropertyChanged(nameof(CurrentTime));
                    ParentViewModel.OnPropertyChanged(nameof(ParentViewModel.PlaybackCursorPosition));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Seek failed: {ex.Message}");
            }
            finally
            {
                _isSeeking = false;
            }
        }

        private NAudioMidi.NoteOnEvent? FindNoteOnEvent(List<TimedMidiEvent> events, int channel, int noteNumber, TimeSpan startTime)
        {
            var matchedEvent = events.FirstOrDefault(te =>
                te.Time == startTime &&
                te.Event is NAudioMidi.NoteOnEvent noe &&
                noe.Channel == channel &&
                noe.NoteNumber == noteNumber &&
                noe.Velocity > 0);

            return matchedEvent?.Event as NAudioMidi.NoteOnEvent;
        }


        private void ProcessNaudioEventOnSynthesizer(NAudioMidi.MidiEvent midiEvent)
        {
            if (_sequencerSynthesizer == null || midiEvent.Channel < 1 || midiEvent.Channel > 16) return;
            int ch = midiEvent.Channel - 1;

            switch (midiEvent.CommandCode)
            {
                case NAudioMidi.MidiCommandCode.NoteOn:
                    var noteOn = (NAudioMidi.NoteOnEvent)midiEvent;
                    if (noteOn.Velocity > 0)
                    {
                        if (!_isSeeking) _sequencerSynthesizer.NoteOn(ch, noteOn.NoteNumber, noteOn.Velocity);
                    }
                    else
                    {
                        if (!_isSeeking) _sequencerSynthesizer.NoteOff(ch, noteOn.NoteNumber);
                    }
                    break;
                case NAudioMidi.MidiCommandCode.NoteOff:
                    var noteOff = (NAudioMidi.NoteEvent)midiEvent;
                    if (!_isSeeking) _sequencerSynthesizer.NoteOff(ch, noteOff.NoteNumber);
                    break;
                case NAudioMidi.MidiCommandCode.ControlChange:
                    var cc = (NAudioMidi.ControlChangeEvent)midiEvent;
                    _sequencerSynthesizer.ProcessMidiMessage(ch, 0xB0, (int)cc.Controller, cc.ControllerValue);
                    break;
                case NAudioMidi.MidiCommandCode.PatchChange:
                    var pc = (NAudioMidi.PatchChangeEvent)midiEvent;
                    _sequencerSynthesizer.ProcessMidiMessage(ch, 0xC0, pc.Patch, 0);
                    break;
                case NAudioMidi.MidiCommandCode.PitchWheelChange:
                    var pw = (NAudioMidi.PitchWheelChangeEvent)midiEvent;
                    _sequencerSynthesizer.ProcessMidiMessage(ch, 0xE0, pw.Pitch & 0x7F, pw.Pitch >> 7);
                    break;
            }
        }

        public void GenerateCheckpoints(NAudioMidi.MidiFile naudioMidiFile)
        {
            _checkpoints.Clear();
            _sortedNaudioEvents.Clear();
            if (naudioMidiFile == null || naudioMidiFile.Events == null)
            {
                _checkpoints.Add(new PlaybackStateSnapshot(TimeSpan.Zero));
                return;
            }

            var tempoMap = MidiProcessor.ExtractTempoMap(naudioMidiFile, MidiConfiguration.Default);
            var ticksPerQuarterNote = naudioMidiFile.DeltaTicksPerQuarterNote;

            var allEvents = new List<TimedMidiEvent>();
            for (int track = 0; track < naudioMidiFile.Tracks; track++)
            {
                try
                {
                    var noteOns = new Dictionary<int, NAudioMidi.NoteOnEvent>();
                    var trackEvents = naudioMidiFile.Events[track].OrderBy(ev => ev.AbsoluteTime).ToList();

                    foreach (var midiEvent in trackEvents)
                    {
                        if (midiEvent == null) continue;

                        if (midiEvent is NAudioMidi.NoteOnEvent noteOn && noteOn.Velocity > 0)
                        {
                            noteOns[noteOn.NoteNumber] = noteOn;
                        }
                        else if (midiEvent is NAudioMidi.NoteEvent noteOff &&
                                 (midiEvent.CommandCode == NAudioMidi.MidiCommandCode.NoteOff || (noteOff is NAudioMidi.NoteOnEvent noe && noe.Velocity == 0)))
                        {
                            if (noteOns.TryGetValue(noteOff.NoteNumber, out var correspondingNoteOn))
                            {
                                if (correspondingNoteOn.OffEvent == null || correspondingNoteOn.OffEvent.AbsoluteTime > noteOff.AbsoluteTime)
                                {
                                    correspondingNoteOn.OffEvent = noteOff;
                                }
                            }
                        }
                        var time = MidiProcessor.TicksToTimeSpan(midiEvent.AbsoluteTime, ticksPerQuarterNote, tempoMap);
                        allEvents.Add(new TimedMidiEvent(time, midiEvent));
                    }
                }
                catch (Exception)
                {
                    continue;
                }
            }


            _sortedNaudioEvents = allEvents.OrderBy(e => e.Time).ThenBy(e => e.Event.AbsoluteTime).ToList();

            _checkpoints.Add(new PlaybackStateSnapshot(TimeSpan.Zero));
            if (!_sortedNaudioEvents.Any()) return;

            TimeSpan nextCheckpointTime = CheckpointInterval;
            var currentState = new PlaybackStateSnapshot(TimeSpan.Zero);
            var activeNotes = new HashSet<(int Channel, int NoteNumber)>();
            var activeNoteStartTimes = new Dictionary<(int Channel, int NoteNumber), TimeSpan>();


            foreach (var timedEvent in _sortedNaudioEvents)
            {
                var time = timedEvent.Time;
                var message = timedEvent.Event;

                while (time >= nextCheckpointTime)
                {
                    var newCheckpoint = new PlaybackStateSnapshot(nextCheckpointTime);
                    for (int i = 0; i < 16; i++)
                    {
                        newCheckpoint.ChannelStates[i] = new ChannelStateSnapshot
                        {
                            Program = currentState.ChannelStates[i].Program,
                            BankSelectMsb = currentState.ChannelStates[i].BankSelectMsb,
                            BankSelectLsb = currentState.ChannelStates[i].BankSelectLsb,
                            Volume = currentState.ChannelStates[i].Volume,
                            Pan = currentState.ChannelStates[i].Pan,
                            Expression = currentState.ChannelStates[i].Expression,
                            PitchBend = currentState.ChannelStates[i].PitchBend,
                            Sustain = currentState.ChannelStates[i].Sustain
                        };
                    }
                    foreach (var note in activeNotes)
                    {
                        newCheckpoint.ActiveNotes.Add(note);
                        if (activeNoteStartTimes.TryGetValue(note, out var startTime))
                        {
                            newCheckpoint.ActiveNoteStartTimes[note] = startTime;
                        }
                    }
                    _checkpoints.Add(newCheckpoint);
                    nextCheckpointTime += CheckpointInterval;
                }

                var channel = message.Channel;
                if (channel >= 1 && channel <= 16)
                {
                    int ch = channel - 1;
                    switch (message.CommandCode)
                    {
                        case NAudioMidi.MidiCommandCode.NoteOn:
                            var noteOn = (NAudioMidi.NoteOnEvent)message;
                            var noteKeyOn = (ch, noteOn.NoteNumber);
                            if (noteOn.Velocity > 0)
                            {
                                activeNotes.Add(noteKeyOn);
                                activeNoteStartTimes[noteKeyOn] = time;
                            }
                            else
                            {
                                activeNotes.Remove(noteKeyOn);
                                activeNoteStartTimes.Remove(noteKeyOn);
                            }
                            break;
                        case NAudioMidi.MidiCommandCode.NoteOff:
                            var noteOff = (NAudioMidi.NoteEvent)message;
                            var noteKeyOff = (ch, noteOff.NoteNumber);
                            activeNotes.Remove(noteKeyOff);
                            activeNoteStartTimes.Remove(noteKeyOff);
                            break;
                        case NAudioMidi.MidiCommandCode.ControlChange:
                            var cc = (NAudioMidi.ControlChangeEvent)message;
                            switch (cc.Controller)
                            {
                                case NAudioMidi.MidiController.BankSelect: currentState.ChannelStates[ch].BankSelectMsb = cc.ControllerValue; break;
                                case NAudioMidi.MidiController.BankSelectLsb: currentState.ChannelStates[ch].BankSelectLsb = cc.ControllerValue; break;
                                case NAudioMidi.MidiController.MainVolume: currentState.ChannelStates[ch].Volume = cc.ControllerValue; break;
                                case NAudioMidi.MidiController.Pan: currentState.ChannelStates[ch].Pan = cc.ControllerValue; break;
                                case NAudioMidi.MidiController.Expression: currentState.ChannelStates[ch].Expression = cc.ControllerValue; break;
                                case NAudioMidi.MidiController.Sustain: currentState.ChannelStates[ch].Sustain = cc.ControllerValue >= 64; break;
                            }
                            break;
                        case NAudioMidi.MidiCommandCode.PatchChange:
                            currentState.ChannelStates[ch].Program = ((NAudioMidi.PatchChangeEvent)message).Patch;
                            break;
                        case NAudioMidi.MidiCommandCode.PitchWheelChange:
                            currentState.ChannelStates[ch].PitchBend = ((NAudioMidi.PitchWheelChangeEvent)message).Pitch;
                            break;
                    }
                }
            }
        }

        public void SetLoop(bool loop, TimeSpan start, TimeSpan end)
        {
            if (IsLooping != loop || LoopStart != start || LoopEnd != end)
            {
                IsLooping = loop;
                LoopStart = start;
                LoopEnd = end;
                ParentViewModel.OnPropertyChanged(nameof(ParentViewModel.IsLooping));
                ParentViewModel.OnPropertyChanged(nameof(ParentViewModel.LoopRangeText));
                ParentViewModel.OnPropertyChanged(nameof(ParentViewModel.LoopStartX));
                ParentViewModel.OnPropertyChanged(nameof(ParentViewModel.LoopDurationWidth));
            }
        }

        public void BeginScrub()
        {
            _isScrubbing = true;
        }

        public void EndScrub()
        {
            _isScrubbing = false;
            if (IsLooping && (CurrentTime < LoopStart || CurrentTime >= LoopEnd))
            {
                SetLoop(false, TimeSpan.Zero, TimeSpan.Zero);
            }
            else
            {
                Seek(CurrentTime);
            }
        }

        public void PlayPianoKey(int noteNumber)
        {
            if (_keyboardSynthesizer == null || _waveOut == null)
            {
                InitializePlayback();
            }

            if (_keyboardSynthesizer == null || _waveOut == null) return;

            if (IsPlaying) return;
            if (_waveOut.PlaybackState != PlaybackState.Playing)
            {
                _waveOut.Play();
            }

            _keyboardSynthesizer.NoteOn(0, noteNumber, 100);
        }

        public void StopPianoKey(int noteNumber)
        {
            _keyboardSynthesizer?.NoteOff(0, noteNumber);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (ParentViewModel.Metronome != null)
            {
                ParentViewModel.Metronome.PropertyChanged -= Metronome_PropertyChanged;
            }

            _playbackCts?.Cancel();

            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;

            _sequencerSynthesizer = null;
            _keyboardSynthesizer = null;
            _audioStartedEvent?.Dispose();
        }

        private class SynthesizerWaveProvider : ISampleProvider
        {
            private readonly PlaybackService _owner;
            private long _playedSamples;
            private int _currentEventIndex;
            private const int SubBlockSize = 64;
            private float[] _subBlockLeft = new float[SubBlockSize];
            private float[] _subBlockRight = new float[SubBlockSize];


            public SynthesizerWaveProvider(PlaybackService owner)
            {
                _owner = owner;
                if (_owner._sequencerSynthesizer == null)
                {
                    throw new InvalidOperationException("Synthesizer is not initialized.");
                }
                WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(_owner._sequencerSynthesizer.SampleRate, 2);
                _playedSamples = 0;
                SetSamplePosition(_playedSamples);
            }

            public void SetSamplePosition(long position)
            {
                _playedSamples = position;
                var targetTime = TimeSpan.FromSeconds((double)position / WaveFormat.SampleRate);
                _currentEventIndex = _owner._sortedNaudioEvents.FindIndex(e => e.Time >= targetTime);
                if (_currentEventIndex == -1)
                    _currentEventIndex = _owner._sortedNaudioEvents.Count;
            }

            public WaveFormat WaveFormat { get; }

            public int Read(float[] buffer, int offset, int count)
            {
                _owner._audioStartedEvent.Set();

                if (_owner._isDisposed)
                {
                    Array.Clear(buffer, offset, count);
                    return count;
                }

                var synthesizer = _owner._sequencerSynthesizer;
                if (synthesizer == null)
                {
                    Array.Clear(buffer, offset, count);
                    return count;
                }

                var outputSpan = buffer.AsSpan(offset, count);
                outputSpan.Clear();

                int samplesRequested = count / 2;
                int samplesRendered = 0;

                while (samplesRendered < samplesRequested)
                {
                    int samplesToRenderInSubBlock = Math.Min(SubBlockSize, samplesRequested - samplesRendered);
                    long currentSubBlockStartSample = _playedSamples;
                    long currentSubBlockEndSample = currentSubBlockStartSample + samplesToRenderInSubBlock;


                    lock (_owner._sequencerLock)
                    {
                        if (_owner.IsPlaying)
                        {
                            while (_currentEventIndex < _owner._sortedNaudioEvents.Count)
                            {
                                var timedEvent = _owner._sortedNaudioEvents[_currentEventIndex];
                                var eventSampleTime = (long)(timedEvent.Time.TotalSeconds * WaveFormat.SampleRate);

                                if (eventSampleTime >= currentSubBlockEndSample) break;

                                _owner.ProcessNaudioEventOnSynthesizer(timedEvent.Event);
                                _currentEventIndex++;
                            }
                        }
                    }

                    var subBlockLeftSpan = _subBlockLeft.AsSpan(0, samplesToRenderInSubBlock);
                    var subBlockRightSpan = _subBlockRight.AsSpan(0, samplesToRenderInSubBlock);

                    synthesizer.Render(subBlockLeftSpan, subBlockRightSpan);

                    if (_owner._keyboardSynthesizer != null)
                    {
                        var keyboardBuffer = ArrayPool<float>.Shared.Rent(samplesToRenderInSubBlock * 2);
                        try
                        {
                            var keyboardSpanInterleaved = keyboardBuffer.AsSpan(0, samplesToRenderInSubBlock * 2);
                            _owner._keyboardSynthesizer.RenderInterleaved(keyboardSpanInterleaved);

                            for (int i = 0; i < samplesToRenderInSubBlock; i++)
                            {
                                subBlockLeftSpan[i] += keyboardSpanInterleaved[i * 2];
                                subBlockRightSpan[i] += keyboardSpanInterleaved[i * 2 + 1];
                            }
                        }
                        finally
                        {
                            ArrayPool<float>.Shared.Return(keyboardBuffer);
                        }
                    }

                    int outputIndex = offset + samplesRendered * 2;
                    for (int i = 0; i < samplesToRenderInSubBlock; i++)
                    {
                        buffer[outputIndex++] = subBlockLeftSpan[i];
                        buffer[outputIndex++] = subBlockRightSpan[i];
                    }


                    _playedSamples += samplesToRenderInSubBlock;
                    samplesRendered += samplesToRenderInSubBlock;
                }

                _owner.AudioChunkRendered?.Invoke(outputSpan);

                return count;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Midi;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Plugin.FileWriter;
using YukkuriMovieMaker.Project;
using MIDI.Utils;

namespace MIDI.FileWriter
{
    public class MidiFileWriter : IVideoFileWriter, IVideoFileWriter2
    {
        public VideoFileWriterSupportedStreams SupportedStreams => VideoFileWriterSupportedStreams.Audio;

        private readonly string _filePath;
        private readonly VideoInfo _videoInfo;
        private readonly MidiFileWriterConfigViewModel _viewModel;
        private readonly AudioToMidiConverter _converter;
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly object _lock = new object();
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private List<float> _audioBuffer;
        private Task? _conversionTask;
        private bool _disposed = false;
        private long _totalSamplesWritten = 0;
        private const int BUFFER_WARNING_THRESHOLD = 100000000;

        public MidiFileWriter(string path, VideoInfo videoInfo, MidiFileWriterConfigViewModel viewModel)
        {
            _filePath = path ?? throw new ArgumentNullException(nameof(path));
            _videoInfo = videoInfo ?? throw new ArgumentNullException(nameof(videoInfo));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            _sampleRate = videoInfo.Hz;
            _channels = 2;
            _converter = new AudioToMidiConverter(_sampleRate, viewModel);
            _audioBuffer = new List<float>(1048576);

            Logger.Info($"MidiFileWriter initialized for: {_filePath}, SampleRate: {_sampleRate}, Channels: {_channels}", 4);
        }

        public void WriteAudio(float[] samples)
        {
            if (samples == null || samples.Length == 0)
                return;

            lock (_lock)
            {
                if (_disposed)
                {
                    Logger.Warn("Attempted to write audio after disposal.", 3);
                    return;
                }

                try
                {
                    _audioBuffer.AddRange(samples);
                    _totalSamplesWritten += samples.Length;

                    if (_audioBuffer.Count > BUFFER_WARNING_THRESHOLD)
                    {
                        Logger.Warn($"Audio buffer size exceeds threshold: {_audioBuffer.Count} samples", 2);
                    }

                    if (_totalSamplesWritten % (_sampleRate * 10 * _channels) == 0)
                    {
                        Logger.Info($"Audio data received: {samples.Length} samples (Total {_totalSamplesWritten})", 5);
                    }
                }
                catch (OutOfMemoryException ex)
                {
                    Logger.Error("Out of memory while buffering audio", ex);
                    throw;
                }
                catch (Exception ex)
                {
                    Logger.Error("Error buffering audio chunk", ex);
                    throw;
                }
            }
        }

        public void WriteVideo(byte[] frame)
        {
        }

        public void WriteVideo(ID2D1Bitmap1 frame)
        {
        }

        private void FinishBufferingAndStartConversion()
        {
            float[]? finalAudioDataArray = null;

            lock (_lock)
            {
                if (_audioBuffer != null && _audioBuffer.Count > 0)
                {
                    try
                    {
                        finalAudioDataArray = _audioBuffer.ToArray();
                        Logger.Info($"Finished buffering audio. Total Samples: {_totalSamplesWritten} ({finalAudioDataArray.Length}). Starting conversion task.", 4);

                        _audioBuffer.Clear();
                        _audioBuffer = null!;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("Error creating final audio array", ex);
                        return;
                    }
                }
                else
                {
                    Logger.Warn("No audio data was buffered. Skipping MIDI conversion.", 2);
                    return;
                }
            }

            if (finalAudioDataArray != null && finalAudioDataArray.Length > 0)
            {
                try
                {
                    _conversionTask = Task.Run(() => ConvertAndWriteMidi(finalAudioDataArray), _cts.Token);
                }
                catch (Exception ex)
                {
                    Logger.Error("Error starting conversion task", ex);
                }
            }
        }

        private void ConvertAndWriteMidi(float[] audioDataArray)
        {
            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                if (_cts.Token.IsCancellationRequested)
                {
                    Logger.Info("Conversion cancelled before start.", 3);
                    return;
                }

                if (audioDataArray == null || audioDataArray.Length == 0)
                {
                    Logger.Warn("Audio data array is null or empty. Skipping conversion.", 2);
                    return;
                }

                Logger.Info($"Starting audio processing. Input samples: {audioDataArray.Length}, Channels: {_channels}", 4);

                float[] monoAudioData = ConvertToMono(audioDataArray);

                if (monoAudioData == null || monoAudioData.Length == 0)
                {
                    Logger.Warn("Mono audio data is empty after conversion. Skipping MIDI generation.", 2);
                    return;
                }

                Logger.Info($"Audio data preparation complete. Sample count: {monoAudioData.Length}, Elapsed time: {sw.ElapsedMilliseconds}ms", 4);

                if (_cts.Token.IsCancellationRequested)
                {
                    Logger.Info("Conversion cancelled after mono conversion.", 3);
                    return;
                }

                List<MidiEvent>? midiEvents = _converter.ConvertToMidiEvents(monoAudioData);

                if (_cts.Token.IsCancellationRequested)
                {
                    Logger.Info("Conversion cancelled after MIDI event generation.", 3);
                    return;
                }

                Logger.Info($"MIDI event conversion complete. Event count: {midiEvents?.Count ?? 0}, Elapsed time: {sw.ElapsedMilliseconds}ms", 4);

                WriteMidiFile(midiEvents);

                Logger.Info($"MIDI file export complete. Total elapsed time: {sw.ElapsedMilliseconds}ms", 4);
            }
            catch (OperationCanceledException)
            {
                Logger.Info("MIDI conversion task was cancelled.", 3);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error during MIDI conversion and writing process", ex);
            }
            finally
            {
                sw.Stop();
                Logger.Info($"ConvertAndWriteMidi process finished. Total elapsed time: {sw.ElapsedMilliseconds}ms", 4);
            }
        }

        private float[] ConvertToMono(float[] audioDataArray)
        {
            try
            {
                if (_channels == 2)
                {
                    if (audioDataArray.Length % 2 != 0)
                    {
                        Logger.Warn($"Stereo audio data length is not even: {audioDataArray.Length}. Truncating.", 3);
                    }

                    int monoLength = audioDataArray.Length / 2;
                    float[] monoAudioData = new float[monoLength];

                    for (int i = 0; i < monoLength; i++)
                    {
                        monoAudioData[i] = (audioDataArray[i * 2] + audioDataArray[i * 2 + 1]) * 0.5f;
                    }

                    Logger.Info($"Converted stereo audio to mono. Mono samples: {monoAudioData.Length}", 5);
                    return monoAudioData;
                }
                else if (_channels == 1)
                {
                    Logger.Info("Audio is already mono.", 5);
                    return audioDataArray;
                }
                else
                {
                    Logger.Warn($"Unexpected channel count: {_channels}. Treating as mono.", 3);
                    return audioDataArray;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error converting to mono", ex);
                throw;
            }
        }

        private void WriteMidiFile(List<MidiEvent>? midiEvents)
        {
            try
            {
                if (midiEvents == null || !midiEvents.Any(ev => ev is NoteOnEvent))
                {
                    var noteEventsCount = midiEvents?.Count(ev => ev is NoteOnEvent) ?? 0;
                    Logger.Warn($"No MIDI NoteOn events were generated (Count: {noteEventsCount}). MIDI file not saved.", 3);
                    return;
                }

                ValidateAndFixMidiEvents(midiEvents);

                var midiFile = new MidiEventCollection(1, _converter.TicksPerQuarterNote);
                midiFile.AddTrack(midiEvents);

                try
                {
                    MidiFile.Export(_filePath, midiFile);
                    Logger.Info($"MIDI file exported successfully: {_filePath}", 4);
                }
                catch (UnauthorizedAccessException ex)
                {
                    Logger.Error($"Access denied writing MIDI file '{_filePath}'", ex);
                    throw;
                }
                catch (System.IO.IOException ex)
                {
                    Logger.Error($"IO error writing MIDI file '{_filePath}'", ex);
                    throw;
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"Failed to write MIDI file '{_filePath}'", ex);
                throw;
            }
        }

        private void ValidateAndFixMidiEvents(List<MidiEvent> midiEvents)
        {
            try
            {
                bool hasTempoEvent = midiEvents.Any(e => e is TempoEvent);
                bool hasEndTrackEvent = midiEvents.Any(e => e is MetaEvent me && me.MetaEventType == MetaEventType.EndTrack);

                if (!hasTempoEvent)
                {
                    midiEvents.Insert(0, new TempoEvent(500000, 0));
                    Logger.Info("Added missing TempoEvent (120BPM).", 5);
                }

                if (!hasEndTrackEvent)
                {
                    long lastTick = midiEvents.Count > 0 ? midiEvents.Max(e => e.AbsoluteTime) : 0;
                    midiEvents.Add(new MetaEvent(MetaEventType.EndTrack, 0, lastTick + _converter.TicksPerQuarterNote));
                    Logger.Info($"Added missing EndTrack event at tick {lastTick + _converter.TicksPerQuarterNote}.", 5);
                }

                midiEvents.RemoveAll(e => e.AbsoluteTime < 0);

                var sortedEvents = midiEvents.OrderBy(e => e.AbsoluteTime).ToList();
                midiEvents.Clear();
                midiEvents.AddRange(sortedEvents);

                Logger.Info("MIDI events validated and fixed.", 5);
            }
            catch (Exception ex)
            {
                Logger.Error("Error validating MIDI events", ex);
                throw;
            }
        }

        public void Dispose()
        {
            bool wasDisposed;
            lock (_lock)
            {
                wasDisposed = _disposed;
                if (!_disposed)
                {
                    _disposed = true;
                    Logger.Info("MidiFileWriter Dispose initiated", 4);
                }
            }

            if (wasDisposed)
            {
                Logger.Info("MidiFileWriter already disposed.", 5);
                return;
            }

            try
            {
                FinishBufferingAndStartConversion();

                Task? taskToWait = null;
                lock (_lock)
                {
                    taskToWait = _conversionTask;
                }

                if (taskToWait != null)
                {
                    WaitForConversionTask(taskToWait);
                }
                else
                {
                    Logger.Info("No conversion task was started, skipping wait.", 5);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error during MidiFileWriter disposal", ex);
            }
            finally
            {
                try
                {
                    _cts?.Cancel();
                    _cts?.Dispose();
                }
                catch (Exception ex)
                {
                    Logger.Error("Error disposing cancellation token source", ex);
                }

                lock (_lock)
                {
                    _audioBuffer?.Clear();
                    _audioBuffer = null!;
                }

                Logger.Info("MidiFileWriter Dispose completed", 4);
                GC.SuppressFinalize(this);
            }
        }

        private void WaitForConversionTask(Task taskToWait)
        {
            try
            {
                Logger.Info("Waiting for MIDI conversion task to complete...", 5);

                bool completed = taskToWait.Wait(TimeSpan.FromSeconds(120));

                if (!completed)
                {
                    Logger.Warn("MIDI conversion task timed out (120s). Attempting cancellation.", 2);
                    _cts.Cancel();

                    try
                    {
                        taskToWait.Wait(TimeSpan.FromSeconds(5));
                    }
                    catch (AggregateException)
                    {
                    }
                }

                Logger.Info($"MIDI conversion task final status: {taskToWait.Status}", 5);

                if (taskToWait.Exception != null)
                {
                    foreach (var ex in taskToWait.Exception.InnerExceptions)
                    {
                        if (!(ex is OperationCanceledException))
                        {
                            Logger.Error("Exception in conversion task", ex);
                        }
                    }
                }
            }
            catch (AggregateException ae)
            {
                bool hadNonCancelledExceptions = false;
                foreach (var ex in ae.InnerExceptions)
                {
                    if (!(ex is OperationCanceledException))
                    {
                        Logger.Error("Error occurred while waiting for conversion task", ex);
                        hadNonCancelledExceptions = true;
                    }
                }

                if (!hadNonCancelledExceptions)
                {
                    Logger.Info("Conversion task was cancelled during wait.", 3);
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("Conversion task was cancelled during wait.", 3);
            }
            catch (Exception ex)
            {
                Logger.Error("Unexpected error occurred while waiting for conversion task", ex);
            }
        }
    }
}
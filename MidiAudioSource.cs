using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.FileSource;
using MeltySynth;
using NAudio.Midi;
using System.Threading;

namespace MIDI
{
    public class MidiAudioSource : IAudioFileSource
    {
        private static readonly ConcurrentDictionary<string, (float[] audioBuffer, TimeSpan duration, int sampleRate)> audioCache = new();

        public static void ClearCache() => audioCache.Clear();

        public TimeSpan Duration { get; private set; }
        public int Hz => sampleRate;

        private readonly int sampleRate;
        private long position = 0;
        private float[]? audioBuffer;
        private readonly Task loadingTask;
        private Dictionary<int, InstrumentSettings>? instrumentSettings;
        private readonly MidiConfiguration config;
        private readonly SynthesisEngine synthesisEngine;
        private readonly AudioRenderer audioRenderer;
        private readonly EffectsProcessor effectsProcessor;
        private readonly SfzProcessor sfzProcessor;

        public MidiAudioSource(string filePath, MidiConfiguration? configuration = null)
        {
            config = configuration ?? MidiConfiguration.Default;
            sampleRate = config.Audio.SampleRate;
            synthesisEngine = new SynthesisEngine(config, sampleRate);
            audioRenderer = new AudioRenderer(config, sampleRate);
            effectsProcessor = new EffectsProcessor(config, sampleRate);
            sfzProcessor = new SfzProcessor(config, sampleRate);

            instrumentSettings = synthesisEngine.InitializeInstrumentSettings();

            if (audioCache.TryGetValue(filePath, out var cachedData) && cachedData.sampleRate == this.sampleRate)
            {
                this.audioBuffer = cachedData.audioBuffer;
                this.Duration = cachedData.duration;
                this.loadingTask = Task.CompletedTask;
                return;
            }

            try
            {
                var midiFile = new MeltySynth.MidiFile(filePath);
                this.Duration = midiFile.Length.Add(TimeSpan.FromSeconds(2.0));
            }
            catch (Exception ex)
            {
                LogError($"MIDIのDuration読み込み中にエラーが発生しました: {ex.Message}", ex);
                this.Duration = TimeSpan.Zero;
                this.loadingTask = Task.FromException(ex);
                return;
            }

            var totalSamples = (long)(this.Duration.TotalSeconds * sampleRate) * 2;
            if (totalSamples <= 0)
            {
                this.audioBuffer = Array.Empty<float>();
                this.loadingTask = Task.CompletedTask;
                return;
            }
            this.audioBuffer = new float[totalSamples];

            try
            {
                RenderInitialChunk(filePath);
            }
            catch (Exception ex)
            {
                LogError($"MIDIの初期チャンク描画中にエラーが発生しました: {ex.Message}", ex);
            }

            this.loadingTask = Task.Run(() => LoadFullAudioAsync(filePath));
        }

        private void RenderInitialChunk(string filePath)
        {
            var initialDuration = TimeSpan.FromSeconds(config.Performance.InitialSyncDurationSeconds);
            var chunkBuffer = RenderAudio(filePath, initialDuration);
            if (this.audioBuffer != null)
            {
                Array.Copy(chunkBuffer, this.audioBuffer, Math.Min(chunkBuffer.Length, this.audioBuffer.Length));
            }
        }

        private void LoadFullAudioAsync(string filePath)
        {
            try
            {
                var fullBuffer = RenderAudio(filePath, null);
                Volatile.Write(ref this.audioBuffer, fullBuffer);

                if (this.audioBuffer != null)
                {
                    var newItem = (this.audioBuffer, this.Duration, this.sampleRate);
                    audioCache.AddOrUpdate(filePath, newItem, (key, existingVal) => newItem);
                }
            }
            catch (Exception ex)
            {
                LogError($"MIDIデータの非同期ロード中にエラーが発生しました: {ex.Message}", ex);
                throw;
            }
        }

        private float[] RenderAudio(string filePath, TimeSpan? durationLimit)
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                                 throw new DirectoryNotFoundException();

            if (config.SFZ.EnableSfz && config.SFZ.ProgramMaps.Any())
            {
                try
                {
                    return sfzProcessor.ProcessWithSfz(filePath, durationLimit);
                }
                catch (Exception ex)
                {
                    LogError($"SFZのレンダリングに失敗しました。: {ex.Message}", ex);
                }
            }

            if (config.SoundFont.EnableSoundFont)
            {
                var sf2Path = FindSoundFont(filePath, assemblyLocation);
                if (sf2Path != null && File.Exists(sf2Path))
                {
                    return ProcessWithSoundFont(filePath, sf2Path, durationLimit);
                }
            }

            return ProcessWithSynthesis(filePath, durationLimit);
        }

        private string? FindSoundFont(string midiFilePath, string assemblyLocation)
        {
            var defaultSf2Path = Path.Combine(assemblyLocation, "GeneralUser-GS.sf2");
            var userSf2Directory = Path.Combine(assemblyLocation, config.SoundFont.DefaultSoundFontDirectory);

            if (!Directory.Exists(userSf2Directory))
            {
                Directory.CreateDirectory(userSf2Directory);
            }
            var allUserSf2Files = Directory.GetFiles(userSf2Directory, "*.sf2", SearchOption.AllDirectories);

            var meltyMidiFile = new MeltySynth.MidiFile(midiFilePath);
            var durationSeconds = meltyMidiFile.Length.TotalSeconds;

            var naudioMidiFile = new NAudio.Midi.MidiFile(midiFilePath, false);
            var trackCount = naudioMidiFile.Events.Tracks;
            var usedPrograms = naudioMidiFile.Events
                .SelectMany(track => track)
                .OfType<PatchChangeEvent>()
                .Select(p => p.Patch)
                .Distinct()
                .ToHashSet();

            foreach (var rule in config.SoundFont.Rules)
            {
                bool durationMatch =
                    (!rule.MinDurationSeconds.HasValue || durationSeconds >= rule.MinDurationSeconds.Value) &&
                    (!rule.MaxDurationSeconds.HasValue || durationSeconds <= rule.MaxDurationSeconds.Value);

                bool trackCountMatch =
                    (!rule.MinTrackCount.HasValue || trackCount >= rule.MinTrackCount.Value) &&
                    (!rule.MaxTrackCount.HasValue || trackCount <= rule.MaxTrackCount.Value);

                bool programsMatch = !rule.RequiredPrograms.Any() || rule.RequiredPrograms.All(usedPrograms.Contains);

                if (durationMatch && trackCountMatch && programsMatch)
                {
                    var matchedFontPath = allUserSf2Files.FirstOrDefault(f =>
                        Path.GetFileName(f).Equals(rule.SoundFontFile, StringComparison.OrdinalIgnoreCase));
                    if (matchedFontPath != null) return matchedFontPath;
                }
            }

            if (!string.IsNullOrEmpty(config.SoundFont.PreferredSoundFont))
            {
                var preferred = allUserSf2Files.FirstOrDefault(f =>
                    Path.GetFileName(f).Equals(config.SoundFont.PreferredSoundFont, StringComparison.OrdinalIgnoreCase));
                if (preferred != null) return preferred;
            }

            if (config.SoundFont.UseDefaultSoundFont && File.Exists(defaultSf2Path))
            {
                return defaultSf2Path;
            }

            if (allUserSf2Files.Any())
            {
                return allUserSf2Files.OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
            }

            return null;
        }

        private float[] ProcessWithSoundFont(string filePath, string sf2Path, TimeSpan? durationLimit)
        {
            var midiFile = new MeltySynth.MidiFile(filePath);
            var synthesizer = new Synthesizer(sf2Path, sampleRate);
            var sequencer = new MidiFileSequencer(synthesizer);
            sequencer.Play(midiFile, false);

            var renderDuration = durationLimit ?? midiFile.Length.Add(TimeSpan.FromSeconds(2.0));
            var totalSamplesToRender = (long)(renderDuration.TotalSeconds * sampleRate) * 2;
            var audioDataList = new List<float>((int)Math.Min(totalSamplesToRender, int.MaxValue));

            var bufferSize = config.Performance.BufferSize;
            var leftBuffer = new float[bufferSize];
            var rightBuffer = new float[bufferSize];

            long renderedSamples = 0;
            while (renderedSamples < totalSamplesToRender && !sequencer.EndOfSequence)
            {
                sequencer.Render(leftBuffer, rightBuffer);

                for (int i = 0; i < leftBuffer.Length; i++)
                {
                    if (renderedSamples + (i * 2) >= totalSamplesToRender) break;
                    audioDataList.Add(leftBuffer[i] * config.Audio.MasterVolume);
                    audioDataList.Add(rightBuffer[i] * config.Audio.MasterVolume);
                }
                renderedSamples += bufferSize * 2;
            }

            var buffer = audioDataList.ToArray();

            if (config.Effects.EnableEffects)
            {
                effectsProcessor.ApplyAudioEnhancements(buffer);
            }
            return buffer;
        }

        private float[] ProcessWithSynthesis(string filePath, TimeSpan? durationLimit)
        {
            var midiFile = new NAudio.Midi.MidiFile(filePath, false);
            var ticksPerQuarterNote = midiFile.DeltaTicksPerQuarterNote;

            var tempoMap = MidiProcessor.ExtractTempoMap(midiFile, config);
            var noteEvents = MidiProcessor.ExtractNoteEvents(midiFile, ticksPerQuarterNote, tempoMap, config);
            var controlEvents = MidiProcessor.ExtractControlEvents(midiFile, ticksPerQuarterNote, tempoMap, config);

            long totalTicks = 0;
            if (noteEvents.Any()) totalTicks = Math.Max(totalTicks, noteEvents.Max(e => e.EndTicks));
            if (controlEvents.Any()) totalTicks = Math.Max(totalTicks, controlEvents.Max(e => e.Ticks));

            var internalDuration = totalTicks > 0 ? MidiProcessor.TicksToTimeSpan(totalTicks, ticksPerQuarterNote, tempoMap) : TimeSpan.FromSeconds(1);
            var renderDuration = durationLimit ?? internalDuration.Add(TimeSpan.FromSeconds(2.0));
            var totalSamples = (long)(renderDuration.TotalSeconds * sampleRate);
            var buffer = new float[totalSamples * 2];

            var channelStates = new Dictionary<int, ChannelState>();
            for (int i = 0; i < 16; i++)
            {
                channelStates[i] = new ChannelState();
            }
            MidiProcessor.ApplyControlEvents(controlEvents, channelStates, config);

            var currentInstrumentSettings = instrumentSettings ?? synthesisEngine.InitializeInstrumentSettings();

            if (config.Performance.EnableParallelProcessing)
            {
                audioRenderer.RenderAudioHighQuality(noteEvents, buffer, channelStates, currentInstrumentSettings);
            }
            else
            {
                audioRenderer.RenderAudioStandard(noteEvents, buffer, channelStates);
            }

            if (config.Audio.EnableNormalization)
            {
                effectsProcessor.NormalizeAudio(buffer);
            }
            return buffer;
        }

        private void LogError(string message, Exception? ex = null)
        {
            if (!config.Debug.EnableLogging) return;

            try
            {
                var logPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                    config.Debug.LogFilePath
                );

                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                if (ex != null) logEntry += $"\n{ex}";
                logEntry += "\n\n";

                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
            }
        }

        public int Read(float[] destBuffer, int offset, int count)
        {
            var currentBuffer = Volatile.Read(ref audioBuffer);

            if (currentBuffer == null)
            {
                Array.Clear(destBuffer, offset, count);
                return count;
            }

            var currentPosition = Interlocked.Read(ref position);
            var maxCount = (int)Math.Max(0, currentBuffer.Length - currentPosition);
            count = Math.Min(count, maxCount);

            if (count > 0)
            {
                Array.Copy(currentBuffer, currentPosition, destBuffer, offset, count);
            }

            Interlocked.Add(ref position, count);
            return count;
        }

        public void Seek(TimeSpan time)
        {
            var currentBuffer = Volatile.Read(ref audioBuffer);
            var bufferLength = currentBuffer?.Length ?? 0;
            var newPosition = Math.Min((long)(sampleRate * time.TotalSeconds) * 2, bufferLength);
            Interlocked.Exchange(ref position, newPosition);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
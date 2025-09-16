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
using System.Buffers;

namespace MIDI
{
    public class MidiAudioSource : IAudioFileSource
    {
        private static readonly ConcurrentDictionary<string, (float[] audioBuffer, TimeSpan duration, int sampleRate)> audioCache = new();
        private static bool hasNotifiedGpuError = false;

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
            effectsProcessor = new EffectsProcessor(config, sampleRate);
            audioRenderer = new AudioRenderer(config, sampleRate);
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
                var sourceSpan = chunkBuffer.AsSpan();
                var destSpan = this.audioBuffer.AsSpan();
                sourceSpan.Slice(0, Math.Min(sourceSpan.Length, destSpan.Length)).CopyTo(destSpan);
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
            if (config.SFZ.EnableSfz)
            {
                try
                {
                    var sfzAudio = sfzProcessor.ProcessWithSfz(filePath, durationLimit);
                    if (sfzAudio.Length > 0)
                    {
                        return sfzAudio;
                    }
                    LogError("SFZレンダリングが空のバッファを返しました。フォールバックします。");
                }
                catch (Exception ex)
                {
                    LogError($"SFZのレンダリングに失敗しました。SoundFontまたは内蔵シンセにフォールバックします。: {ex.Message}", ex);
                }
            }

            if (config.SoundFont.EnableSoundFont)
            {
                var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new DirectoryNotFoundException();
                var sf2Path = FindSoundFont(filePath, assemblyLocation);
                if (sf2Path != null && File.Exists(sf2Path))
                {
                    try
                    {
                        return ProcessWithSoundFont(filePath, sf2Path, durationLimit);
                    }
                    catch (Exception ex)
                    {
                        LogError($"SoundFontの処理中にエラーが発生しました。内蔵シンセにフォールバックします。: {ex.Message}", ex);
                    }
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

            var bufferSize = config.Performance.BufferSize;
            var leftBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            var rightBuffer = ArrayPool<float>.Shared.Rent(bufferSize);

            var audioData = new float[totalSamplesToRender];
            var audioDataSpan = audioData.AsSpan();
            long renderedSamples = 0;

            try
            {
                while (renderedSamples < totalSamplesToRender && !sequencer.EndOfSequence)
                {
                    sequencer.Render(leftBuffer, rightBuffer);
                    var samplesToCopy = (int)Math.Min(bufferSize * 2, totalSamplesToRender - renderedSamples);

                    for (int i = 0; i < samplesToCopy / 2; i++)
                    {
                        audioDataSpan[(int)renderedSamples + i * 2] = leftBuffer[i] * config.Audio.MasterVolume;
                        audioDataSpan[(int)renderedSamples + i * 2 + 1] = rightBuffer[i] * config.Audio.MasterVolume;
                    }
                    renderedSamples += samplesToCopy;
                }

                if (config.Effects.EnableEffects)
                {
                    if (!effectsProcessor.ApplyAudioEnhancements(audioDataSpan))
                    {
                        NotifyGpuFallbackToCpu();
                    }
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(leftBuffer);
                ArrayPool<float>.Shared.Return(rightBuffer);
            }

            return audioData;
        }

        private float[] ProcessWithSynthesis(string filePath, TimeSpan? durationLimit)
        {
            var midiFile = new NAudio.Midi.MidiFile(filePath, false);
            var ticksPerQuarterNote = midiFile.DeltaTicksPerQuarterNote;

            var tempoMap = MidiProcessor.ExtractTempoMap(midiFile, config);
            var noteEvents = MidiProcessor.ExtractNoteEvents(midiFile, ticksPerQuarterNote, tempoMap, config, sampleRate);
            var controlEvents = MidiProcessor.ExtractControlEvents(midiFile, ticksPerQuarterNote, tempoMap, config);

            long totalTicks = 0;
            if (noteEvents.Any()) totalTicks = Math.Max(totalTicks, noteEvents.Max(e => e.EndTicks));
            if (controlEvents.Any()) totalTicks = Math.Max(totalTicks, controlEvents.Max(e => e.Ticks));

            var internalDuration = totalTicks > 0 ? MidiProcessor.TicksToTimeSpan(totalTicks, ticksPerQuarterNote, tempoMap) : TimeSpan.FromSeconds(1);
            var renderDuration = durationLimit ?? internalDuration.Add(TimeSpan.FromSeconds(2.0));
            var totalSamples = (long)(renderDuration.TotalSeconds * sampleRate);
            var buffer = new float[totalSamples * 2];
            var bufferSpan = buffer.AsSpan();

            var channelStates = new Dictionary<int, ChannelState>();
            for (int i = 0; i < 16; i++)
            {
                channelStates[i] = new ChannelState();
            }
            MidiProcessor.ApplyControlEvents(controlEvents, channelStates, config);

            var currentInstrumentSettings = instrumentSettings ?? synthesisEngine.InitializeInstrumentSettings();

            bool gpuSucceeded = true;
            if (config.Performance.GPU.EnableGpuSynthesis && ComputeSharp.GraphicsDevice.GetDefault() != null)
            {
                if (!audioRenderer.RenderAudioGpu(bufferSpan, noteEvents, channelStates, currentInstrumentSettings))
                {
                    gpuSucceeded = false;
                    audioRenderer.RenderAudioHighQuality(buffer, noteEvents, channelStates, currentInstrumentSettings);
                }
            }
            else if (config.Performance.EnableParallelProcessing)
            {
                audioRenderer.RenderAudioHighQuality(buffer, noteEvents, channelStates, currentInstrumentSettings);
            }
            else
            {
                audioRenderer.RenderAudioStandard(bufferSpan, noteEvents, channelStates);
            }

            if (config.Synthesis.EnableEnvelopeSmoothing)
            {
                var attackSamples = (int)(config.Synthesis.SmoothingAttackSeconds * sampleRate) * 2;
                var releaseSamples = (int)(config.Synthesis.SmoothingReleaseSeconds * sampleRate) * 2;

                foreach (var note in noteEvents)
                {
                    var startSample = (int)note.StartSample * 2;
                    var endSample = (int)note.EndSample * 2;

                    for (int i = 0; i < attackSamples; i++)
                    {
                        if (startSample + i >= buffer.Length) break;
                        var fade = (float)i / attackSamples;
                        buffer[startSample + i] *= fade;
                    }

                    for (int i = 0; i < releaseSamples; i++)
                    {
                        var index = endSample - releaseSamples + i;
                        if (index < 0) continue;
                        if (index >= buffer.Length) break;
                        var fade = 1.0f - (float)i / releaseSamples;
                        buffer[index] *= fade;
                    }
                }
            }

            if (config.Synthesis.EnableAntiPop)
            {
                var attackSamples = (int)(config.Synthesis.AntiPopAttackSeconds * sampleRate);
                var releaseSamples = (int)(config.Synthesis.AntiPopReleaseSeconds * sampleRate);

                foreach (var note in noteEvents)
                {
                    var startSample = note.StartSample * 2;
                    var endSample = note.EndSample * 2;
                    var noteSamples = endSample - startSample;

                    var actualAttackSamples = Math.Min(attackSamples * 2, noteSamples);
                    for (long i = 0; i < actualAttackSamples; i += 2)
                    {
                        var index = startSample + i;
                        if (index + 1 >= buffer.Length) break;
                        var fade = (float)i / actualAttackSamples;
                        buffer[index] *= fade;
                        buffer[index + 1] *= fade;
                    }

                    var actualReleaseSamples = Math.Min(releaseSamples * 2, noteSamples);
                    for (long i = 0; i < actualReleaseSamples; i += 2)
                    {
                        var index = endSample - actualReleaseSamples + i;
                        if (index < 0 || index + 1 >= buffer.Length) continue;
                        var fade = 1.0f - ((float)i / actualReleaseSamples);
                        buffer[index] *= fade;
                        buffer[index + 1] *= fade;
                    }
                }
            }

            if (config.Effects.EnableEffects)
            {
                if (!effectsProcessor.ApplyAudioEnhancements(bufferSpan))
                {
                    gpuSucceeded = false;
                }
            }

            if (!gpuSucceeded)
            {
                NotifyGpuFallbackToCpu();
            }

            if (config.Audio.EnableGlobalFadeOut)
            {
                var fadeSamples = (int)(config.Audio.GlobalFadeOutSeconds * sampleRate) * 2;
                fadeSamples = Math.Min(fadeSamples, buffer.Length);
                for (int i = 0; i < fadeSamples; i++)
                {
                    var index = buffer.Length - fadeSamples + i;
                    var fade = 1.0f - (float)i / fadeSamples;
                    buffer[index] *= fade;
                }
            }

            if (config.Audio.EnableNormalization)
            {
                effectsProcessor.NormalizeAudio(bufferSpan);
            }
            return buffer;
        }

        private void NotifyGpuFallbackToCpu()
        {
            if (hasNotifiedGpuError) return;
            hasNotifiedGpuError = true;
            string message = "GPUでの音声処理中にエラーが発生しました。処理は自動的にCPUに切り替えられました。\n" +
                             "パフォーマンスが低下する可能性があります。\n\n" +
                             "このメッセージはアプリケーションの実行ごとに一度だけ表示されます。\n" +
                             "エラーが続く場合は、設定 > パフォーマンス > GPUアクセラレーションを無効にすることをお勧めします。";
            LogError(message);
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
                var sourceSpan = new ReadOnlySpan<float>(currentBuffer, (int)currentPosition, count);
                var destSpan = new Span<float>(destBuffer, offset, count);
                sourceSpan.CopyTo(destSpan);
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
            effectsProcessor?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
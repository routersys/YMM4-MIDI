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
using ComputeSharp;
using System.Numerics;

namespace MIDI
{
    public class MidiAudioSource : IAudioFileSource
    {
        private enum RenderMethod { Synthesis, SoundFont, Sfz }

        private static readonly ConcurrentDictionary<string, (float[] audioBuffer, TimeSpan duration, int sampleRate)> audioCache = new();
        private static bool hasNotifiedGpuError = false;

        public static void ClearCache() => audioCache.Clear();

        public TimeSpan Duration { get; private set; }
        public int Hz => sampleRate;

        private readonly int sampleRate;
        private long position = 0;
        private float[]? audioBuffer;
        private readonly Task loadingTask;
        private readonly MidiConfiguration config;
        private readonly SynthesisEngine synthesisEngine;
        private readonly AudioRenderer audioRenderer;
        private readonly EffectsProcessor effectsProcessor;
        private readonly SfzProcessor sfzProcessor;
        private readonly string midiFilePath;

        private readonly RenderMethod _renderMethod;
        private List<EnhancedNoteEvent>? allNoteEvents;
        private List<ControlEvent>? allControlEvents;

        private readonly SfzRealtimeState? _sfzState;

        private List<Synthesizer>? _soundFontSynthesizers;
        private List<MidiFileSequencer>? _soundFontSequencers;
        private readonly MeltySynth.MidiFile? _soundFontMidiFile;

        private readonly GraphicsDevice? gpuDevice;


        public MidiAudioSource(string filePath, MidiConfiguration? configuration = null)
        {
            this.midiFilePath = filePath;
            config = configuration ?? MidiConfiguration.Default;
            sampleRate = config.Audio.SampleRate;
            synthesisEngine = new SynthesisEngine(config, sampleRate);

            sfzProcessor = new SfzProcessor(config, sampleRate);

            if (audioCache.TryGetValue(filePath, out var cachedData) && cachedData.sampleRate == this.sampleRate)
            {
                this.audioBuffer = cachedData.audioBuffer;
                this.Duration = cachedData.duration;
                this.loadingTask = Task.CompletedTask;
                effectsProcessor = new EffectsProcessor(config, sampleRate);
                audioRenderer = new AudioRenderer(config, sampleRate);
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
                effectsProcessor = new EffectsProcessor(config, sampleRate);
                audioRenderer = new AudioRenderer(config, sampleRate);
                return;
            }

            _renderMethod = DetermineRenderMethod();

            if (config.Performance.RenderingMode == RenderingMode.HighQualityGPU || config.Performance.RenderingMode == RenderingMode.RealtimeGPU)
            {
                try
                {
                    gpuDevice = GraphicsDevice.GetDefault();
                }
                catch (Exception ex)
                {
                    LogError($"GPUデバイスの取得に失敗しました: {ex.Message}", ex);
                }
            }

            effectsProcessor = new EffectsProcessor(config, sampleRate, gpuDevice);
            audioRenderer = new AudioRenderer(config, sampleRate);

            bool isRealtime = config.Performance.RenderingMode == RenderingMode.RealtimeCPU || config.Performance.RenderingMode == RenderingMode.RealtimeGPU;

            if (isRealtime)
            {
                PrepareRealtimeRendering(out _sfzState, out _soundFontSynthesizers, out _soundFontMidiFile, out _soundFontSequencers);
                this.loadingTask = Task.CompletedTask;
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

        private RenderMethod DetermineRenderMethod()
        {
            if (config.SFZ.EnableSfz)
            {
                return RenderMethod.Sfz;
            }
            if (config.SoundFont.EnableSoundFont && FindActiveSoundFonts(midiFilePath, GetAssemblyLocation()).Any())
            {
                return RenderMethod.SoundFont;
            }
            return RenderMethod.Synthesis;
        }

        private void PrepareRealtimeRendering(out SfzRealtimeState? sfzState, out List<Synthesizer>? sfSynthesizers, out MeltySynth.MidiFile? sfMidiFile, out List<MidiFileSequencer>? sfSequencers)
        {
            sfzState = null;
            sfSynthesizers = null;
            sfMidiFile = null;
            sfSequencers = null;

            try
            {
                var naudioMidiFile = new NAudio.Midi.MidiFile(midiFilePath, false);
                var tempoMap = MidiProcessor.ExtractTempoMap(naudioMidiFile, config);
                allNoteEvents = MidiProcessor.ExtractNoteEvents(naudioMidiFile, naudioMidiFile.DeltaTicksPerQuarterNote, tempoMap, config, sampleRate);
                allControlEvents = MidiProcessor.ExtractControlEvents(naudioMidiFile, naudioMidiFile.DeltaTicksPerQuarterNote, tempoMap, config);

                switch (_renderMethod)
                {
                    case RenderMethod.Sfz:
                        var allUniversalMidiEvents = MidiProcessor.ExtractAllMidiEvents(naudioMidiFile, naudioMidiFile.DeltaTicksPerQuarterNote, tempoMap, sampleRate);
                        sfzState = sfzProcessor.Initialize(allUniversalMidiEvents);
                        break;
                    case RenderMethod.SoundFont:
                        var sf2Paths = FindActiveSoundFonts(midiFilePath, GetAssemblyLocation());
                        if (sf2Paths.Any())
                        {
                            sfSynthesizers = new List<Synthesizer>();
                            sfSequencers = new List<MidiFileSequencer>();
                            sfMidiFile = new MeltySynth.MidiFile(midiFilePath);

                            foreach (var sf2Path in sf2Paths)
                            {
                                var synthesizer = new Synthesizer(sf2Path, sampleRate);
                                var sequencer = new MidiFileSequencer(synthesizer);
                                sequencer.Play(sfMidiFile, false);
                                sfSynthesizers.Add(synthesizer);
                                sfSequencers.Add(sequencer);
                            }
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                LogError($"リアルタイムレンダリングの準備中にエラーが発生しました: {ex.Message}", ex);
            }
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
            switch (_renderMethod)
            {
                case RenderMethod.Sfz:
                    try
                    {
                        var sfzAudio = sfzProcessor.ProcessWithSfz(filePath, durationLimit);
                        if (sfzAudio.Length > 0) return sfzAudio;
                        LogError("SFZレンダリングが空のバッファを返しました。フォールバックします。");
                    }
                    catch (Exception ex)
                    {
                        LogError($"SFZのレンダリングに失敗しました。SoundFontまたは内蔵シンセにフォールバックします。: {ex.Message}", ex);
                    }
                    break;
                case RenderMethod.SoundFont:
                    var sf2Paths = FindActiveSoundFonts(filePath, GetAssemblyLocation());
                    if (sf2Paths.Any())
                    {
                        try
                        {
                            return ProcessWithSoundFont(filePath, sf2Paths, durationLimit);
                        }
                        catch (Exception ex)
                        {
                            LogError($"SoundFontの処理中にエラーが発生しました。内蔵シンセにフォールバックします。: {ex.Message}", ex);
                        }
                    }
                    break;
            }
            return ProcessWithSynthesis(filePath, durationLimit);
        }

        private static string GetAssemblyLocation() => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? throw new DirectoryNotFoundException();

        private List<string> FindActiveSoundFonts(string midiFilePath, string assemblyLocation)
        {
            var activeFonts = new List<string>();
            var userSf2Directory = Path.Combine(assemblyLocation, config.SoundFont.DefaultSoundFontDirectory);

            if (!Directory.Exists(userSf2Directory))
            {
                try { Directory.CreateDirectory(userSf2Directory); } catch { }
            }

            var allUserSf2Files = Directory.Exists(userSf2Directory)
                ? Directory.GetFiles(userSf2Directory, "*.sf2", SearchOption.AllDirectories)
                : Array.Empty<string>();

            foreach (var layer in config.SoundFont.Layers)
            {
                var fontPath = allUserSf2Files.FirstOrDefault(f =>
                    Path.GetFileName(f).Equals(layer.SoundFontFile, StringComparison.OrdinalIgnoreCase));
                if (fontPath != null && !activeFonts.Contains(fontPath))
                {
                    activeFonts.Add(fontPath);
                }
            }

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
                    if (matchedFontPath != null && !activeFonts.Contains(matchedFontPath))
                    {
                        activeFonts.Add(matchedFontPath);
                    }
                }
            }

            if (!string.IsNullOrEmpty(config.SoundFont.PreferredSoundFont))
            {
                var preferred = allUserSf2Files.FirstOrDefault(f =>
                    Path.GetFileName(f).Equals(config.SoundFont.PreferredSoundFont, StringComparison.OrdinalIgnoreCase));
                if (preferred != null && !activeFonts.Contains(preferred))
                {
                    activeFonts.Add(preferred);
                }
            }

            var defaultSf2Path = Path.Combine(assemblyLocation, "GeneralUser-GS.sf2");
            if (config.SoundFont.UseDefaultSoundFont && File.Exists(defaultSf2Path) && !activeFonts.Contains(defaultSf2Path))
            {
                activeFonts.Add(defaultSf2Path);
            }

            if (!activeFonts.Any() && allUserSf2Files.Any())
            {
                var largestSf2 = allUserSf2Files.OrderByDescending(f => new FileInfo(f).Length).FirstOrDefault();
                if (largestSf2 != null) activeFonts.Add(largestSf2);
            }

            return activeFonts;
        }

        private float[] ProcessWithSoundFont(string filePath, List<string> sf2Paths, TimeSpan? durationLimit)
        {
            var midiFile = new MeltySynth.MidiFile(filePath);
            var renderDuration = durationLimit ?? midiFile.Length.Add(TimeSpan.FromSeconds(2.0));
            var totalSamplesToRender = (long)(renderDuration.TotalSeconds * sampleRate) * 2;
            if (totalSamplesToRender <= 0) return Array.Empty<float>();

            var finalBuffer = new float[totalSamplesToRender];
            var finalBufferSpan = finalBuffer.AsSpan();

            var bufferSize = config.Performance.BufferSize;
            var leftBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            var rightBuffer = ArrayPool<float>.Shared.Rent(bufferSize);

            try
            {
                foreach (var sf2Path in sf2Paths)
                {
                    var synthesizer = new Synthesizer(sf2Path, sampleRate);
                    var sequencer = new MidiFileSequencer(synthesizer);
                    sequencer.Play(midiFile, false);

                    long renderedSamples = 0;
                    while (renderedSamples < totalSamplesToRender && !sequencer.EndOfSequence)
                    {
                        sequencer.Render(leftBuffer, rightBuffer);
                        var samplesToCopy = (int)Math.Min(bufferSize * 2, totalSamplesToRender - renderedSamples);
                        var samplesToCopyPerChannel = samplesToCopy / 2;

                        for (int i = 0; i < samplesToCopyPerChannel; i++)
                        {
                            finalBufferSpan[(int)renderedSamples + i * 2] += leftBuffer[i] * config.Audio.MasterVolume;
                            finalBufferSpan[(int)renderedSamples + i * 2 + 1] += rightBuffer[i] * config.Audio.MasterVolume;
                        }
                        renderedSamples += samplesToCopy;
                    }
                }

                if (config.Effects.EnableEffects)
                {
                    if (!effectsProcessor.ApplyAudioEnhancements(finalBufferSpan))
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

            return finalBuffer;
        }

        private float[] ProcessWithSynthesis(string filePath, TimeSpan? durationLimit)
        {
            var naudioMidiFile = new NAudio.Midi.MidiFile(filePath, false);
            var ticksPerQuarterNote = naudioMidiFile.DeltaTicksPerQuarterNote;

            var tempoMap = MidiProcessor.ExtractTempoMap(naudioMidiFile, config);
            var noteEvents = MidiProcessor.ExtractNoteEvents(naudioMidiFile, ticksPerQuarterNote, tempoMap, config, sampleRate);
            var controlEvents = MidiProcessor.ExtractControlEvents(naudioMidiFile, ticksPerQuarterNote, tempoMap, config);

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

            var currentInstrumentSettings = synthesisEngine.InitializeInstrumentSettings();

            bool gpuSucceeded = true;
            if (config.Performance.RenderingMode == RenderingMode.HighQualityGPU && gpuDevice != null)
            {
                if (!audioRenderer.RenderAudioGpu(gpuDevice, bufferSpan, noteEvents, channelStates, currentInstrumentSettings))
                {
                    gpuSucceeded = false;
                    audioRenderer.RenderAudioHighQuality(buffer, noteEvents, channelStates, currentInstrumentSettings);
                }
            }
            else if (config.Performance.RenderingMode == RenderingMode.HighQualityCPU || config.Performance.EnableParallelProcessing)
            {
                audioRenderer.RenderAudioHighQuality(buffer, noteEvents, channelStates, currentInstrumentSettings);
            }
            else
            {
                audioRenderer.RenderAudioStandard(bufferSpan, noteEvents, channelStates);
            }

            if (!gpuSucceeded)
            {
                NotifyGpuFallbackToCpu();
            }

            if (config.Effects.EnableEffects)
            {
                if (!effectsProcessor.ApplyAudioEnhancements(bufferSpan))
                {
                    NotifyGpuFallbackToCpu();
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
                var logPath = Path.Combine(GetAssemblyLocation(), config.Debug.LogFilePath);
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
                if (ex != null) logEntry += $"\n{ex}";
                logEntry += "\n\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch { }
        }

        private Dictionary<int, ChannelState> GetChannelStatesAtSample(long sample)
        {
            var states = new Dictionary<int, ChannelState>();
            for (int i = 0; i < 16; i++)
            {
                states[i] = new ChannelState();
            }

            if (allControlEvents != null)
            {
                var eventsToApply = allControlEvents.Where(e => (e.Time.TotalSeconds * sampleRate) < sample);
                MidiProcessor.ApplyControlEvents(eventsToApply.ToList(), states, config);
            }

            return states;
        }

        private int RenderRealtimeChunk(Span<float> destBuffer)
        {
            var startSample = position / 2;
            var numSamples = destBuffer.Length / 2;
            var endSample = startSample + numSamples;

            destBuffer.Clear();

            switch (_renderMethod)
            {
                case RenderMethod.Sfz:
                    if (_sfzState != null)
                    {
                        sfzProcessor.RenderRealtimeChunk(_sfzState, destBuffer, startSample);
                    }
                    break;
                case RenderMethod.SoundFont:
                    if (_soundFontSynthesizers != null && _soundFontSequencers != null)
                    {
                        var left = ArrayPool<float>.Shared.Rent(numSamples);
                        var right = ArrayPool<float>.Shared.Rent(numSamples);
                        var tempLeft = ArrayPool<float>.Shared.Rent(numSamples);
                        var tempRight = ArrayPool<float>.Shared.Rent(numSamples);
                        try
                        {
                            var leftSpan = left.AsSpan(0, numSamples);
                            var rightSpan = right.AsSpan(0, numSamples);
                            leftSpan.Clear();
                            rightSpan.Clear();

                            for (int i = 0; i < _soundFontSequencers.Count; i++)
                            {
                                var tempLeftSpan = tempLeft.AsSpan(0, numSamples);
                                var tempRightSpan = tempRight.AsSpan(0, numSamples);

                                _soundFontSequencers[i].Render(tempLeftSpan, tempRightSpan);

                                for (int j = 0; j < numSamples; j++)
                                {
                                    leftSpan[j] += tempLeftSpan[j];
                                    rightSpan[j] += tempRightSpan[j];
                                }
                            }

                            for (int i = 0; i < numSamples; i++)
                            {
                                destBuffer[i * 2] = left[i] * config.Audio.MasterVolume;
                                destBuffer[i * 2 + 1] = right[i] * config.Audio.MasterVolume;
                            }
                        }
                        finally
                        {
                            ArrayPool<float>.Shared.Return(left);
                            ArrayPool<float>.Shared.Return(right);
                            ArrayPool<float>.Shared.Return(tempLeft);
                            ArrayPool<float>.Shared.Return(tempRight);
                        }
                    }
                    break;
                case RenderMethod.Synthesis:
                    if (allNoteEvents == null || allControlEvents == null)
                    {
                        return destBuffer.Length;
                    }

                    var channelStates = GetChannelStatesAtSample(startSample);
                    var notesInChunk = allNoteEvents.Where(n => n.StartSample < endSample && n.EndSample > startSample).ToList();

                    if (config.Performance.RenderingMode == RenderingMode.RealtimeGPU && gpuDevice != null)
                    {
                        if (!audioRenderer.RenderAudioGpuRealtime(gpuDevice, destBuffer, notesInChunk, channelStates, synthesisEngine.InitializeInstrumentSettings(), startSample))
                        {
                            NotifyGpuFallbackToCpu();
                            audioRenderer.RenderAudioStandard(destBuffer, notesInChunk, channelStates, startSample);
                        }
                    }
                    else
                    {
                        audioRenderer.RenderAudioStandard(destBuffer, notesInChunk, channelStates, startSample);
                    }
                    break;
            }

            if (config.Effects.EnableEffects)
            {
                if (!effectsProcessor.ApplyAudioEnhancements(destBuffer))
                {
                    NotifyGpuFallbackToCpu();
                }
            }

            if (config.Audio.EnableNormalization)
            {
                effectsProcessor.NormalizeAudio(destBuffer);
            }

            return destBuffer.Length;
        }


        public int Read(float[] destBuffer, int offset, int count)
        {
            bool isRealtime = config.Performance.RenderingMode == RenderingMode.RealtimeCPU || config.Performance.RenderingMode == RenderingMode.RealtimeGPU;

            if (isRealtime)
            {
                var destSpan = new Span<float>(destBuffer, offset, count);
                var renderedCount = RenderRealtimeChunk(destSpan);
                Interlocked.Add(ref position, renderedCount);
                return renderedCount;
            }

            var currentBuffer = Volatile.Read(ref audioBuffer);
            if (currentBuffer == null)
            {
                loadingTask.Wait();
                currentBuffer = Volatile.Read(ref audioBuffer);
            }

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

        private void SeekRealtime(long newPosition)
        {
            var samplesToSeek = newPosition / 2;

            if (_renderMethod == RenderMethod.Sfz && _sfzState != null)
            {
                sfzProcessor.Seek(_sfzState, samplesToSeek);
            }
            else if (_renderMethod == RenderMethod.SoundFont && _soundFontSynthesizers != null && _soundFontSequencers != null && _soundFontMidiFile != null)
            {
                _soundFontSequencers.Clear();
                for (int i = 0; i < _soundFontSynthesizers.Count; i++)
                {
                    var sequencer = new MidiFileSequencer(_soundFontSynthesizers[i]);
                    sequencer.Play(_soundFontMidiFile, false);

                    var samplesToRender = samplesToSeek;
                    var bufferSize = config.Performance.BufferSize;
                    var left = ArrayPool<float>.Shared.Rent(bufferSize);
                    var right = ArrayPool<float>.Shared.Rent(bufferSize);
                    try
                    {
                        while (samplesToRender > 0)
                        {
                            var count = (int)Math.Min(samplesToRender, bufferSize);
                            sequencer.Render(left.AsSpan(0, count), right.AsSpan(0, count));
                            samplesToRender -= count;
                        }
                    }
                    finally
                    {
                        ArrayPool<float>.Shared.Return(left);
                        ArrayPool<float>.Shared.Return(right);
                    }
                    _soundFontSequencers.Add(sequencer);
                }
            }
            else if (_renderMethod == RenderMethod.Synthesis)
            {
            }
        }

        public void Seek(TimeSpan time)
        {
            var newPosition = (long)(sampleRate * time.TotalSeconds) * 2;

            bool isRealtime = config.Performance.RenderingMode == RenderingMode.RealtimeCPU || config.Performance.RenderingMode == RenderingMode.RealtimeGPU;
            if (isRealtime)
            {
                SeekRealtime(newPosition);
            }

            Interlocked.Exchange(ref position, newPosition);
        }

        public void Dispose()
        {
            (gpuDevice as IDisposable)?.Dispose();
            _sfzState?.Dispose();
            effectsProcessor?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
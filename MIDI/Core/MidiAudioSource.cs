using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.FileSource;
using System.Threading;
using MIDI.Renderers;
using MIDI.Configuration.Models;
using MIDI.Utils;
using MIDI.Core;
using System.Text.Json;
using NAudio.Midi;

namespace MIDI
{
    public class MidiAudioSource : IAudioFileSource
    {
        private enum RenderMethod { Synthesis, SoundFont, Sfz }

        private static readonly ConcurrentDictionary<string, (float[] audioBuffer, TimeSpan duration, int sampleRate)> audioCache = new();

        public static void ClearCache()
        {
            audioCache.Clear();
        }

        public TimeSpan Duration { get; private set; }
        public int Hz => sampleRate;

        private readonly int sampleRate;
        private long position = 0;
        private float[]? audioBuffer;
        private readonly Task initializationTask;
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly MidiConfiguration config;
        private readonly MidiEditorSettings editorSettings;
        private string midiFilePath;
        private readonly string originalMidiFilePath;
        private readonly bool isFromYmidi;
        private RenderMethod renderMethod;
        private IAudioRenderer? renderer;
        private readonly EffectsProcessor effectsProcessor;
        private bool isHighQualityMode;
        private readonly object readLock = new object();
        private bool disposed = false;
        private bool isStopping = false;
        private int fadeOutSamplesRemaining = 0;
        private const int FadeDurationMs = 10;

        public MidiAudioSource(string filePath, MidiConfiguration? configuration = null)
        {
            this.originalMidiFilePath = filePath;
            this.midiFilePath = filePath;
            config = configuration ?? MidiConfiguration.Default;
            editorSettings = MidiEditorSettings.Default;
            sampleRate = config.Audio.SampleRate;

            cancellationTokenSource = new CancellationTokenSource();
            effectsProcessor = new EffectsProcessor(config, sampleRate);

            if (Path.GetExtension(filePath)?.ToLower() == ".ymidi")
            {
                isFromYmidi = true;
                this.initializationTask = InitializeFromYmidiAsync(filePath);
            }
            else
            {
                isFromYmidi = false;
                try
                {
                    var midiFile = new MeltySynth.MidiFile(filePath);
                    this.Duration = midiFile.Length.Add(TimeSpan.FromSeconds(2.0));
                }
                catch (Exception ex)
                {
                    Logger.Error(LogMessages.MidiDurationReadError, ex, ex.Message);
                    this.Duration = TimeSpan.Zero;
                    this.initializationTask = Task.FromException(ex);
                    return;
                }

                this.initializationTask = InitializeAsync();
            }
        }

        private async Task InitializeFromYmidiAsync(string ymidiPath)
        {
            try
            {
                var project = await ProjectService.LoadProjectAsync(ymidiPath);
                var baseMidiPath = project.MidiFilePath;
                if (!File.Exists(baseMidiPath))
                {
                    throw new FileNotFoundException("プロジェクトに関連付けられたMIDIファイルが見つかりません。", baseMidiPath);
                }

                var tempMidiPath = Path.GetTempFileName();
                var originalMidiFile = new NAudio.Midi.MidiFile(baseMidiPath, false);
                ProjectService.ApplyProjectToMidi(originalMidiFile, project);
                NAudio.Midi.MidiFile.Export(tempMidiPath, originalMidiFile.Events);

                this.midiFilePath = tempMidiPath;

                var midiFile = new MeltySynth.MidiFile(this.midiFilePath);
                this.Duration = midiFile.Length.Add(TimeSpan.FromSeconds(2.0));

                await InitializeAsync();
            }
            catch (Exception ex)
            {
                Logger.Error("YMIDIプロジェクトからの初期化に失敗しました。", ex);
                throw;
            }
        }

        private async Task InitializeAsync()
        {
            var token = cancellationTokenSource.Token;
            var configHash = config.GetConfigurationHash();
            var cacheKey = $"{midiFilePath}_{sampleRate}_{configHash}";

            if (audioCache.TryGetValue(cacheKey, out var cachedData))
            {
                this.audioBuffer = cachedData.audioBuffer;
                this.isHighQualityMode = !(config.Performance.RenderingMode == RenderingMode.RealtimeCPU || config.Performance.RenderingMode == RenderingMode.RealtimeGPU);
                return;
            }

            await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                renderMethod = DetermineRenderMethod();
                string pathForRenderer = (renderMethod == RenderMethod.Synthesis) ? midiFilePath : ApplyMicrotonalPitchBend(midiFilePath);
                renderer = CreateRenderer(renderMethod, pathForRenderer);
            }, token).ConfigureAwait(false);


            isHighQualityMode = !(config.Performance.RenderingMode == RenderingMode.RealtimeCPU || config.Performance.RenderingMode == RenderingMode.RealtimeGPU);

            if (isHighQualityMode)
            {
                await LoadFullAudioAsync(midiFilePath, cacheKey, token);
            }
        }

        private string ApplyMicrotonalPitchBend(string originalFilePath)
        {
            if (editorSettings.TuningSystem != TuningSystemType.Microtonal)
            {
                return originalFilePath;
            }

            var midiFile = new MidiFile(originalFilePath, false);
            var centOffsets = new List<double>();
            var textEvents = midiFile.Events.SelectMany(track => track).OfType<TextEvent>()
                .Where(e => e.Text.StartsWith("CENT_OFFSET:"));

            foreach (var textEvent in textEvents)
            {
                var parts = textEvent.Text.Split(':');
                if (parts.Length == 2)
                {
                    var values = parts[1].Split(',');
                    if (values.Length == 3 && int.TryParse(values[2], out int offset))
                    {
                        centOffsets.Add(offset);
                    }
                }
            }

            if (!centOffsets.Any())
            {
                return originalFilePath;
            }

            var averageCentOffset = centOffsets.Average();
            var pitchBendValue = (int)(8192 + (averageCentOffset / 100.0) * (8192.0 / (config.MIDI.PitchBendRange)));
            pitchBendValue = Math.Clamp(pitchBendValue, 0, 16383);


            var newEvents = new MidiEventCollection(midiFile.FileFormat, midiFile.DeltaTicksPerQuarterNote);
            for (int i = 0; i < midiFile.Tracks; i++)
            {
                newEvents.AddTrack();
                newEvents[i].Add(new PitchWheelChangeEvent(0, 1, pitchBendValue));
                foreach (var ev in midiFile.Events[i])
                {
                    newEvents[i].Add(ev.Clone());
                }
            }

            var tempPath = Path.GetTempFileName();
            MidiFile.Export(tempPath, newEvents);
            return tempPath;
        }

        private Task LoadFullAudioAsync(string filePath, string cacheKey, CancellationToken token)
        {
            return Task.Run(() =>
            {
                try
                {
                    var tempFilePath = (renderMethod == RenderMethod.Synthesis) ? filePath : ApplyMicrotonalPitchBend(filePath);
                    using var backgroundRenderer = CreateRenderer(renderMethod, tempFilePath);
                    float[] fullBuffer;

                    try
                    {
                        fullBuffer = backgroundRenderer.Render(tempFilePath, null);
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn(LogMessages.RendererFailedFallback, renderMethod, ex.Message);
                        if (renderMethod != RenderMethod.Synthesis)
                        {
                            try
                            {
                                using var fallbackRenderer = new SynthesisRenderer(tempFilePath, config, sampleRate);
                                fullBuffer = fallbackRenderer.Render(tempFilePath, null);
                            }
                            catch (Exception fallbackEx)
                            {
                                Logger.Error(LogMessages.RendererFailedFallback, fallbackEx, fallbackEx.Message);
                                fullBuffer = Array.Empty<float>();
                            }
                        }
                        else
                        {
                            fullBuffer = Array.Empty<float>();
                        }
                    }
                    finally
                    {
                        if (tempFilePath != filePath)
                        {
                            File.Delete(tempFilePath);
                        }
                    }

                    token.ThrowIfCancellationRequested();

                    if (fullBuffer.Length > 0)
                    {
                        using var backgroundEffectsProcessor = new EffectsProcessor(config, sampleRate);
                        var bufferSpan = fullBuffer.AsSpan();
                        if (config.Effects.EnableEffects)
                        {
                            backgroundEffectsProcessor.ApplyAudioEnhancements(bufferSpan);
                        }
                        if (config.Audio.EnableNormalization)
                        {
                            backgroundEffectsProcessor.NormalizeAudio(bufferSpan);
                        }
                    }

                    var newAudioBuffer = new float[fullBuffer.Length];
                    fullBuffer.CopyTo(newAudioBuffer, 0);

                    Volatile.Write(ref this.audioBuffer, newAudioBuffer);

                    var newItem = (newAudioBuffer, this.Duration, this.sampleRate);
                    audioCache.AddOrUpdate(cacheKey, newItem, (key, existingVal) => newItem);
                }
                catch (OperationCanceledException)
                {
                    Logger.Info(LogMessages.AsyncLoadCancelled);
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        Logger.Error(LogMessages.MidiAsyncLoadError, ex, ex.Message);
                    }
                }
            }, token);
        }

        private IAudioRenderer CreateRenderer(RenderMethod method, string filePathToUse)
        {
            return method switch
            {
                RenderMethod.Sfz => new SfzRenderer(filePathToUse, config, sampleRate),
                RenderMethod.SoundFont => new SoundFontRenderer(filePathToUse, config, sampleRate, FindActiveSoundFonts(filePathToUse, GetAssemblyLocation())),
                _ => new SynthesisRenderer(filePathToUse, config, sampleRate),
            };
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
                .OfType<NAudio.Midi.PatchChangeEvent>()
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

        public async Task<float[]> ReadAllAsync()
        {
            await initializationTask;

            if (initializationTask.IsFaulted)
            {
                throw initializationTask.Exception ?? new Exception("MIDI Audio Source initialization failed.");
            }

            var buffer = Volatile.Read(ref audioBuffer);
            if (buffer != null)
            {
                var bufferCopy = new float[buffer.Length];
                buffer.CopyTo(bufferCopy, 0);
                return bufferCopy;
            }

            if (renderer != null)
            {
                return renderer.Render(midiFilePath, Duration);
            }

            return Array.Empty<float>();
        }

        public int Read(float[] destBuffer, int offset, int count)
        {
            lock (readLock)
            {
                if (disposed) return 0;

                try
                {
                    initializationTask.Wait(500);
                }
                catch (Exception) { }

                var destSpan = new Span<float>(destBuffer, offset, count);
                var fadeSamples = (int)(sampleRate * (FadeDurationMs / 1000.0)) * 2;

                if (isStopping)
                {
                    int samplesToFade = Math.Min(count, fadeOutSamplesRemaining);
                    if (samplesToFade > 0)
                    {
                        for (int i = 0; i < samplesToFade; i++)
                        {
                            float fade = (float)(fadeOutSamplesRemaining - i) / fadeSamples;
                            destSpan[i] *= fade;
                        }
                    }
                    if (count > samplesToFade)
                    {
                        destSpan.Slice(samplesToFade).Clear();
                    }
                    fadeOutSamplesRemaining -= samplesToFade;
                    if (fadeOutSamplesRemaining <= 0)
                    {
                        isStopping = false;
                    }
                    Interlocked.Add(ref position, count);
                    return count;
                }

                var currentPosition = Interlocked.Read(ref position);

                if (!initializationTask.IsCompleted)
                {
                    destSpan.Clear();
                    Interlocked.Add(ref position, count);
                    return count;
                }

                if (initializationTask.IsFaulted)
                {
                    if (initializationTask.Exception != null)
                    {
                        Logger.Error("MidiAudioSource initialization failed.", initializationTask.Exception.InnerException);
                    }
                    destSpan.Clear();
                    Interlocked.Add(ref position, count);
                    return count;
                }

                var currentBuffer = Volatile.Read(ref audioBuffer);
                int readCount = 0;

                if (currentBuffer != null)
                {
                    var maxCount = (int)Math.Max(0, currentBuffer.Length - currentPosition);
                    readCount = Math.Min(count, maxCount);

                    if (readCount > 0)
                    {
                        new ReadOnlySpan<float>(currentBuffer, (int)currentPosition, readCount)
                            .CopyTo(destSpan.Slice(0, readCount));
                    }
                    if (readCount < count)
                    {
                        destSpan.Slice(readCount).Clear();
                    }
                }
                else
                {
                    if (renderer != null)
                    {
                        readCount = renderer.Read(destSpan, currentPosition);
                        if (readCount < count)
                        {
                            destSpan.Slice(readCount).Clear();
                        }
                        if (readCount > 0)
                        {
                            var renderedSpan = destSpan.Slice(0, readCount);
                            if (config.Effects.EnableEffects) effectsProcessor.ApplyAudioEnhancements(renderedSpan);
                            if (config.Audio.EnableNormalization) effectsProcessor.NormalizeAudio(renderedSpan);
                        }
                    }
                    else
                    {
                        destSpan.Clear();
                        readCount = count;
                    }
                }

                Interlocked.Add(ref position, count);
                return count;
            }
        }

        public void Seek(TimeSpan time)
        {
            var newPosition = (long)(sampleRate * time.TotalSeconds) * 2;
            Interlocked.Exchange(ref position, newPosition);

            if (initializationTask.IsCompletedSuccessfully && renderer != null)
            {
                renderer.Seek(newPosition / 2);
                effectsProcessor.Reset();
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            isStopping = true;
            fadeOutSamplesRemaining = (int)(sampleRate * (FadeDurationMs / 1000.0)) * 2;

            cancellationTokenSource.Cancel();

            try
            {
                initializationTask.Wait(500);
            }
            catch (Exception) { }

            renderer?.Dispose();
            effectsProcessor?.Dispose();
            cancellationTokenSource.Dispose();

            if (isFromYmidi && File.Exists(midiFilePath))
            {
                try
                {
                    File.Delete(midiFilePath);
                }
                catch (Exception ex)
                {
                    Logger.Error("一時MIDIファイルの削除に失敗しました。", ex);
                }
            }

            GC.SuppressFinalize(this);
        }
    }
}
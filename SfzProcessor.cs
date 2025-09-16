using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NAudio.Midi;

namespace MIDI
{
    public class SfzRealtimeState : IDisposable
    {
        public List<IntPtr> AllSynths { get; } = new();
        public Dictionary<int, IntPtr> ProgramToSynth { get; } = new();
        public IntPtr[] ChannelToSynth { get; } = new IntPtr[16];
        public List<UniversalMidiEvent> AllEvents { get; set; } = new();
        public int CurrentEventIndex { get; set; } = 0;
        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                foreach (var synth in AllSynths)
                {
                    SfizzPInvoke.sfizz_free(synth);
                }
                AllSynths.Clear();
                ProgramToSynth.Clear();
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    public class SfzProcessor
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;

        public SfzProcessor(MidiConfiguration config, int sampleRate)
        {
            this.config = config;
            this.sampleRate = sampleRate;
        }

        public float[] ProcessWithSfz(string midiFilePath, TimeSpan? durationLimit)
        {
            var midiFile = new NAudio.Midi.MidiFile(midiFilePath, false);
            var tempoMap = MidiProcessor.ExtractTempoMap(midiFile, config);
            var allEvents = MidiProcessor.ExtractAllMidiEvents(midiFile, midiFile.DeltaTicksPerQuarterNote, tempoMap, sampleRate);

            using var state = Initialize(allEvents);
            if (!state.AllSynths.Any())
            {
                throw new InvalidOperationException("有効なSFZファイルが一つも見つかりませんでした。");
            }

            var renderDuration = durationLimit ??
                                MidiProcessor.TicksToTimeSpan(allEvents.LastOrDefault()?.Ticks ?? 0,
                                                            midiFile.DeltaTicksPerQuarterNote, tempoMap)
                                            .Add(TimeSpan.FromSeconds(2.0));

            var totalSamples = (long)(renderDuration.TotalSeconds * sampleRate);
            var outputBuffer = new float[totalSamples * 2];

            RenderRealtimeChunk(state, outputBuffer, 0);

            if (config.Audio.EnableNormalization)
            {
                var effectsProcessor = new EffectsProcessor(config, sampleRate);
                effectsProcessor.NormalizeAudio(outputBuffer);
            }

            return outputBuffer;
        }

        public SfzRealtimeState Initialize(List<UniversalMidiEvent> allEvents)
        {
            var state = new SfzRealtimeState { AllEvents = allEvents };
            var usedPrograms = allEvents.Where(e => e.Type == UniversalMidiEventType.PatchChange)
                                      .Select(e => e.Data1)
                                      .Distinct()
                                      .Append(0)
                                      .ToHashSet();

            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                                 throw new DirectoryNotFoundException();
            var sfzSearchDir = Path.Combine(assemblyLocation, config.SFZ.SfzSearchPath);

            var sfzPathToSynth = new Dictionary<string, IntPtr>();

            LoadSfzSynths(usedPrograms, sfzSearchDir, state.ProgramToSynth, sfzPathToSynth, state.AllSynths);

            var defaultSynth = state.ProgramToSynth.TryGetValue(0, out var s) ? s : state.AllSynths.FirstOrDefault();
            for (int i = 0; i < 16; i++)
            {
                state.ChannelToSynth[i] = defaultSynth;
            }

            return state;
        }

        public void Seek(SfzRealtimeState state, long targetSample)
        {
            foreach (var synth in state.AllSynths)
            {
                SfizzPInvoke.sfizz_all_sounds_off(synth);
            }

            var defaultSynth = state.ProgramToSynth.TryGetValue(0, out var s) ? s : state.AllSynths.FirstOrDefault();
            for (int i = 0; i < 16; i++)
            {
                state.ChannelToSynth[i] = defaultSynth;
            }

            state.CurrentEventIndex = 0;

            ProcessEventsForBlock(state, 0, (int)targetSample, true);
        }

        public void RenderRealtimeChunk(SfzRealtimeState state, Span<float> outputBuffer, long startSample)
        {
            outputBuffer.Clear();
            if (!state.AllSynths.Any()) return;

            var numSamples = outputBuffer.Length / 2;
            var samplesPerBlock = config.Performance.BufferSize;
            var leftBlock = ArrayPool<float>.Shared.Rent(samplesPerBlock);
            var rightBlock = ArrayPool<float>.Shared.Rent(samplesPerBlock);
            var mixLeftBlock = ArrayPool<float>.Shared.Rent(samplesPerBlock);
            var mixRightBlock = ArrayPool<float>.Shared.Rent(samplesPerBlock);

            var leftHandle = GCHandle.Alloc(leftBlock, GCHandleType.Pinned);
            var rightHandle = GCHandle.Alloc(rightBlock, GCHandleType.Pinned);
            var pointers = new IntPtr[] { leftHandle.AddrOfPinnedObject(), rightHandle.AddrOfPinnedObject() };
            var pointersHandle = GCHandle.Alloc(pointers, GCHandleType.Pinned);
            var pointersPtr = pointersHandle.AddrOfPinnedObject();

            try
            {
                for (long framePos = 0; framePos < numSamples; framePos += samplesPerBlock)
                {
                    long currentAbsoluteSample = startSample + framePos;
                    var currentBlockSize = (int)Math.Min(samplesPerBlock, numSamples - framePos);

                    ProcessEventsForBlock(state, currentAbsoluteSample, currentBlockSize, false);

                    mixLeftBlock.AsSpan(0, currentBlockSize).Clear();
                    mixRightBlock.AsSpan(0, currentBlockSize).Clear();

                    RenderSynthsForBlock(state.AllSynths, currentBlockSize, pointersPtr, mixLeftBlock, mixRightBlock, leftBlock, rightBlock);

                    CopyBlockToOutput(outputBuffer, framePos, numSamples, currentBlockSize, mixLeftBlock, mixRightBlock);
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(leftBlock);
                ArrayPool<float>.Shared.Return(rightBlock);
                ArrayPool<float>.Shared.Return(mixLeftBlock);
                ArrayPool<float>.Shared.Return(mixRightBlock);
                leftHandle.Free();
                rightHandle.Free();
                pointersHandle.Free();
            }
        }

        private void ProcessEventsForBlock(SfzRealtimeState state, long startSample, int numSamples, bool isSeeking)
        {
            long endSample = startSample + numSamples;

            while (state.CurrentEventIndex < state.AllEvents.Count &&
                   state.AllEvents[state.CurrentEventIndex].SampleTime < endSample)
            {
                var evt = state.AllEvents[state.CurrentEventIndex];
                if (evt.Channel < 1 || evt.Channel > 16)
                {
                    state.CurrentEventIndex++;
                    continue;
                }

                int frameDelay = (int)(evt.SampleTime - startSample);
                frameDelay = Math.Max(0, Math.Min(frameDelay, numSamples - 1));

                var targetSynth = state.ChannelToSynth[evt.Channel - 1];

                switch (evt.Type)
                {
                    case UniversalMidiEventType.NoteOn:
                        if (!isSeeking) SfizzPInvoke.sfizz_note_on(targetSynth, frameDelay, evt.Data1, evt.Data2);
                        break;
                    case UniversalMidiEventType.NoteOff:
                        if (!isSeeking) SfizzPInvoke.sfizz_note_off(targetSynth, frameDelay, evt.Data1, evt.Data2);
                        break;
                    case UniversalMidiEventType.ControlChange:
                        SfizzPInvoke.sfizz_cc(targetSynth, frameDelay, evt.Data1, evt.Data2);
                        break;
                    case UniversalMidiEventType.PitchWheel:
                        SfizzPInvoke.sfizz_pitch_wheel(targetSynth, frameDelay, evt.Data1 | (evt.Data2 << 7));
                        break;
                    case UniversalMidiEventType.PatchChange:
                        if (state.ProgramToSynth.TryGetValue(evt.Data1, out var newSynth))
                        {
                            state.ChannelToSynth[evt.Channel - 1] = newSynth;
                        }
                        break;
                }
                state.CurrentEventIndex++;
            }
        }


        private void LoadSfzSynths(HashSet<int> usedPrograms, string sfzSearchDir,
                                 Dictionary<int, IntPtr> programToSynth,
                                 Dictionary<string, IntPtr> sfzPathToSynth,
                                 List<IntPtr> allSynths)
        {
            foreach (var prog in usedPrograms)
            {
                var map = config.SFZ.ProgramMaps.FirstOrDefault(m => m.Program == prog);
                if (map == null || string.IsNullOrEmpty(map.FilePath)) continue;

                var sfzPath = Path.Combine(sfzSearchDir, map.FilePath);
                if (!File.Exists(sfzPath)) continue;

                if (sfzPathToSynth.TryGetValue(sfzPath, out var synth))
                {
                    programToSynth[prog] = synth;
                }
                else
                {
                    var newSynth = SfizzPInvoke.sfizz_new();
                    SfizzPInvoke.sfizz_set_sample_rate(newSynth, sampleRate);
                    SfizzPInvoke.sfizz_set_samples_per_block(newSynth, config.Performance.BufferSize);

                    if (SfizzPInvoke.sfizz_load_sfz_file(newSynth, sfzPath))
                    {
                        sfzPathToSynth[sfzPath] = newSynth;
                        programToSynth[prog] = newSynth;
                        allSynths.Add(newSynth);
                    }
                    else
                    {
                        SfizzPInvoke.sfizz_free(newSynth);
                        LogError($"SFZファイルの読み込みに失敗: {sfzPath}");
                    }
                }
            }
        }

        private void RenderSynthsForBlock(List<IntPtr> allSynths, int samplesPerBlock, IntPtr pointersPtr,
                                        Span<float> mixLeftBlock, Span<float> mixRightBlock,
                                        ReadOnlySpan<float> leftBlock, ReadOnlySpan<float> rightBlock)
        {
            foreach (var synth in allSynths)
            {
                SfizzPInvoke.sfizz_render_block(synth, pointersPtr, 2, samplesPerBlock);
                for (int i = 0; i < samplesPerBlock; i++)
                {
                    mixLeftBlock[i] += leftBlock[i];
                    mixRightBlock[i] += rightBlock[i];
                }
            }
        }

        private void CopyBlockToOutput(Span<float> outputBuffer, long framePos, long totalSamples,
                                     int samplesPerBlock, ReadOnlySpan<float> mixLeftBlock, ReadOnlySpan<float> mixRightBlock)
        {
            var samplesToCopy = (int)Math.Min(samplesPerBlock, totalSamples - framePos);
            for (int i = 0; i < samplesToCopy; i++)
            {
                var outIndex = (int)(framePos + i) * 2;
                if (outIndex + 1 < outputBuffer.Length)
                {
                    outputBuffer[outIndex] = mixLeftBlock[i] * config.Audio.MasterVolume;
                    outputBuffer[outIndex + 1] = mixRightBlock[i] * config.Audio.MasterVolume;
                }
            }
        }

        private void LogError(string message)
        {
            if (!config.Debug.EnableLogging) return;

            try
            {
                var logPath = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "",
                    config.Debug.LogFilePath
                );

                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n\n";
                File.AppendAllText(logPath, logEntry);
            }
            catch
            {
            }
        }
    }
}
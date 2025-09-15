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
            var usedPrograms = allEvents.Where(e => e.Type == UniversalMidiEventType.PatchChange)
                                      .Select(e => e.Data1)
                                      .Distinct()
                                      .Append(0)
                                      .ToHashSet();

            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ??
                                 throw new DirectoryNotFoundException();
            var sfzSearchDir = Path.Combine(assemblyLocation, config.SFZ.SfzSearchPath);

            var programToSynth = new Dictionary<int, IntPtr>();
            var sfzPathToSynth = new Dictionary<string, IntPtr>();
            var allSynths = new List<IntPtr>();

            try
            {
                LoadSfzSynths(usedPrograms, sfzSearchDir, programToSynth, sfzPathToSynth, allSynths);

                if (!allSynths.Any())
                {
                    throw new InvalidOperationException("有効なSFZファイルが一つも見つかりませんでした。");
                }

                return RenderSfzAudio(allEvents, allSynths, programToSynth, durationLimit, midiFile, tempoMap);
            }
            finally
            {
                foreach (var synth in allSynths)
                {
                    SfizzPInvoke.sfizz_free(synth);
                }
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

        private float[] RenderSfzAudio(List<UniversalMidiEvent> allEvents, List<IntPtr> allSynths,
                                     Dictionary<int, IntPtr> programToSynth, TimeSpan? durationLimit,
                                     NAudio.Midi.MidiFile midiFile, List<TempoEvent> tempoMap)
        {
            var renderDuration = durationLimit ??
                                MidiProcessor.TicksToTimeSpan(allEvents.LastOrDefault()?.Ticks ?? 0,
                                                            midiFile.DeltaTicksPerQuarterNote, tempoMap)
                                            .Add(TimeSpan.FromSeconds(2.0));

            var totalSamples = (long)(renderDuration.TotalSeconds * sampleRate);
            var outputBuffer = ArrayPool<float>.Shared.Rent((int)(totalSamples * 2));
            var outputSpan = outputBuffer.AsSpan(0, (int)(totalSamples * 2));
            outputSpan.Clear();

            var channelToSynth = new IntPtr[16];
            var defaultSynth = programToSynth.TryGetValue(0, out var s) ? s : allSynths.First();
            for (int i = 0; i < 16; i++)
                channelToSynth[i] = defaultSynth;

            int currentEventIndex = 0;
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
                ProcessSfzBlocks(allEvents, totalSamples, outputSpan, channelToSynth,
                               programToSynth, allSynths, currentEventIndex, samplesPerBlock,
                               mixLeftBlock, mixRightBlock, pointersPtr, leftBlock, rightBlock);

                if (config.Audio.EnableNormalization)
                {
                    var effectsProcessor = new EffectsProcessor(config, sampleRate);
                    effectsProcessor.NormalizeAudio(outputSpan);
                }

                return outputSpan.ToArray();
            }
            finally
            {
                ArrayPool<float>.Shared.Return(outputBuffer);
                ArrayPool<float>.Shared.Return(leftBlock);
                ArrayPool<float>.Shared.Return(rightBlock);
                ArrayPool<float>.Shared.Return(mixLeftBlock);
                ArrayPool<float>.Shared.Return(mixRightBlock);
                leftHandle.Free();
                rightHandle.Free();
                pointersHandle.Free();
            }
        }

        private void ProcessSfzBlocks(List<UniversalMidiEvent> allEvents, long totalSamples, Span<float> outputBuffer,
                                    IntPtr[] channelToSynth, Dictionary<int, IntPtr> programToSynth,
                                    List<IntPtr> allSynths, int currentEventIndex, int samplesPerBlock,
                                    float[] mixLeftBlock, float[] mixRightBlock, IntPtr pointersPtr,
                                    float[] leftBlock, float[] rightBlock)
        {
            var mixLeftSpan = mixLeftBlock.AsSpan(0, samplesPerBlock);
            var mixRightSpan = mixRightBlock.AsSpan(0, samplesPerBlock);

            for (long framePos = 0; framePos < totalSamples; framePos += samplesPerBlock)
            {
                currentEventIndex = ProcessEventsForBlock(allEvents, framePos, samplesPerBlock,
                                                        currentEventIndex, channelToSynth, programToSynth);

                mixLeftSpan.Clear();
                mixRightSpan.Clear();

                RenderSynthsForBlock(allSynths, samplesPerBlock, pointersPtr, mixLeftBlock, mixRightBlock, leftBlock, rightBlock);

                CopyBlockToOutput(outputBuffer, framePos, totalSamples, samplesPerBlock, mixLeftSpan, mixRightSpan);
            }
        }

        private int ProcessEventsForBlock(List<UniversalMidiEvent> allEvents, long framePos, int samplesPerBlock,
                                        int currentEventIndex, IntPtr[] channelToSynth,
                                        Dictionary<int, IntPtr> programToSynth)
        {
            while (currentEventIndex < allEvents.Count &&
                   allEvents[currentEventIndex].SampleTime < framePos + samplesPerBlock)
            {
                var evt = allEvents[currentEventIndex];
                int frameDelay = (int)(evt.SampleTime - framePos);
                frameDelay = Math.Max(0, Math.Min(frameDelay, samplesPerBlock - 1));

                var targetSynth = channelToSynth[evt.Channel - 1];

                switch (evt.Type)
                {
                    case UniversalMidiEventType.NoteOn:
                        SfizzPInvoke.sfizz_note_on(targetSynth, frameDelay, evt.Data1, evt.Data2);
                        break;
                    case UniversalMidiEventType.NoteOff:
                        SfizzPInvoke.sfizz_note_off(targetSynth, frameDelay, evt.Data1, evt.Data2);
                        break;
                    case UniversalMidiEventType.ControlChange:
                        SfizzPInvoke.sfizz_cc(targetSynth, frameDelay, evt.Data1, evt.Data2);
                        break;
                    case UniversalMidiEventType.PitchWheel:
                        SfizzPInvoke.sfizz_pitch_wheel(targetSynth, frameDelay, evt.Data1 | (evt.Data2 << 7));
                        break;
                    case UniversalMidiEventType.PatchChange:
                        if (programToSynth.TryGetValue(evt.Data1, out var newSynth))
                        {
                            channelToSynth[evt.Channel - 1] = newSynth;
                        }
                        break;
                }
                currentEventIndex++;
            }
            return currentEventIndex;
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
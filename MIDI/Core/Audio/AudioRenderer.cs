using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ComputeSharp;
using MIDI.Configuration.Models;
using MIDI.Utils;

namespace MIDI
{
    public class AudioRenderer
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;
        private readonly ISynthesisEngine synthesisEngine;
        private readonly IFilterProcessor filterProcessor;
        private readonly IEffectsProcessor effectsProcessor;
        private readonly object bufferLock = new object();
        private const int GpuChunkSize = 1 << 18;

        public AudioRenderer(MidiConfiguration config, int sampleRate, ISynthesisEngine synthesisEngine, IFilterProcessor filterProcessor, IEffectsProcessor effectsProcessor)
        {
            this.config = config;
            this.sampleRate = sampleRate;
            this.synthesisEngine = synthesisEngine;
            this.filterProcessor = filterProcessor;
            this.effectsProcessor = effectsProcessor;
        }

        public void RenderAudioHighQuality(float[] buffer, List<EnhancedNoteEvent> noteEvents,
                                         Dictionary<int, ChannelState> channelStates,
                                         Dictionary<int, InstrumentSettings> instrumentSettings)
        {
            var groupedNotes = noteEvents.GroupBy(n => n.Channel).ToDictionary(g => g.Key, g => g.ToList());

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = config.Performance.EnableParallelProcessing ? config.Performance.MaxThreads : 1
            };

            Parallel.ForEach(groupedNotes, parallelOptions, kvp =>
            {
                var channel = kvp.Key;
                if (channel < 1 || channel > 16) return;

                var notes = kvp.Value;
                float[] channelBuffer = ArrayPool<float>.Shared.Rent(buffer.Length);
                var channelSpan = channelBuffer.AsSpan(0, buffer.Length);

                try
                {
                    channelSpan.Clear();
                    var channelState = channelStates[channel - 1];

                    foreach (var note in notes)
                    {
                        RenderNoteWithEffects(note, channelSpan, channelState, instrumentSettings);
                    }

                    lock (bufferLock)
                    {
                        var bufferSpan = buffer.AsSpan();
                        int vectorSize = Vector<float>.Count;
                        int i = 0;
                        for (; i <= buffer.Length - vectorSize; i += vectorSize)
                        {
                            var bufferVec = new Vector<float>(bufferSpan.Slice(i, vectorSize));
                            var channelVec = new Vector<float>(channelSpan.Slice(i, vectorSize));
                            (bufferVec + channelVec).CopyTo(bufferSpan.Slice(i, vectorSize));
                        }
                        for (; i < buffer.Length; i++)
                        {
                            buffer[i] += channelBuffer[i];
                        }
                    }
                }
                finally
                {
                    ArrayPool<float>.Shared.Return(channelBuffer);
                }
            });
        }

        private List<GpuNoteData> CreateGpuNoteDataList(IEnumerable<EnhancedNoteEvent> noteEvents, Dictionary<int, ChannelState> channelStates, Dictionary<int, InstrumentSettings> instrumentSettings, long sampleOffset = 0)
        {
            var noteDataList = new List<GpuNoteData>();
            foreach (var note in noteEvents)
            {
                if (note.Channel < 1 || note.Channel > 16) continue;
                var channelState = channelStates[note.Channel - 1];
                var instrument = synthesisEngine.GetInstrumentSettings(note.Channel, channelState.Program, instrumentSettings);
                var frequency = (float)synthesisEngine.GetFrequency(note.NoteNumber, channelState.PitchBend, note.CentOffset);
                var baseAmplitude = (note.Velocity / 127.0f) * channelState.Volume * channelState.Expression * instrument.VolumeMultiplier * config.Audio.MasterVolume;
                var panAngle = (channelState.Pan + 1) * Math.PI / 4;

                var attack = instrument.Attack * config.Synthesis.EnvelopeScale * channelState.AttackMultiplier;
                var release = instrument.Release * config.Synthesis.EnvelopeScale * channelState.ReleaseMultiplier;

                if (config.Synthesis.EnableAntiPop)
                {
                    attack = Math.Max(attack, config.Synthesis.AntiPopAttackSeconds);
                    release = Math.Max(release, config.Synthesis.AntiPopReleaseSeconds);
                }

                noteDataList.Add(new GpuNoteData
                {
                    NoteNumber = note.NoteNumber,
                    Velocity = note.Velocity,
                    Channel = note.Channel,
                    StartSample = (int)(note.StartSample - sampleOffset),
                    EndSample = (int)(note.EndSample - sampleOffset),
                    Frequency = frequency,
                    BaseAmplitude = baseAmplitude,
                    WaveType = (int)instrument.WaveType,
                    Attack = (float)attack,
                    Decay = (float)(instrument.Decay * config.Synthesis.EnvelopeScale * channelState.DecayMultiplier),
                    Sustain = (float)instrument.Sustain,
                    Release = (float)release,
                    FilterType = (int)instrument.FilterType,
                    FilterCutoff = (float)instrument.FilterCutoff,
                    FilterResonance = (float)instrument.FilterResonance,
                    PanLeft = (float)Math.Cos(panAngle),
                    PanRight = (float)Math.Sin(panAngle)
                });
            }
            return noteDataList;
        }

        public bool RenderAudioGpu(GraphicsDevice device, Span<float> buffer, List<EnhancedNoteEvent> noteEvents, Dictionary<int, ChannelState> channelStates, Dictionary<int, InstrumentSettings> instrumentSettings)
        {
            try
            {
                buffer.Clear();
                using var gpuBuffer = device.AllocateReadWriteBuffer<float>(buffer);

                int numSamples = buffer.Length / 2;
                for (int offset = 0; offset < numSamples; offset += GpuChunkSize)
                {
                    int count = Math.Min(GpuChunkSize, numSamples - offset);
                    if (count <= 0) continue;
                    long chunkEndSample = offset + count;

                    var notesForChunk = noteEvents.Where(n => n.StartSample < chunkEndSample && n.EndSample > offset).ToList();
                    if (!notesForChunk.Any()) continue;

                    var noteDataList = CreateGpuNoteDataList(notesForChunk, channelStates, instrumentSettings);
                    if (!noteDataList.Any()) continue;

                    using var noteBuffer = device.AllocateReadOnlyBuffer(noteDataList.ToArray());

                    var shader = new WaveformGenerationShader(
                        gpuBuffer,
                        noteBuffer,
                        sampleRate,
                        offset,
                        (float)config.Synthesis.FmModulatorFrequency,
                        (float)config.Synthesis.FmModulationIndex,
                        config.Synthesis.EnableBandlimitedSynthesis,
                        config.Synthesis.EnableNoteCrossfade ? (int)(config.Synthesis.NoteCrossfadeDuration * sampleRate) : 0
                    );
                    device.For(count, shader);
                }

                gpuBuffer.CopyTo(buffer);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.GpuRenderError, ex);
                return false;
            }
        }

        public void RenderAudioStandard(Span<float> buffer, List<EnhancedNoteEvent> noteEvents,
                                      Dictionary<int, ChannelState> channelStates, long startSampleOffset = 0)
        {
            foreach (var note in noteEvents)
            {
                if (note.Channel < 1 || note.Channel > 16) continue;
                var channelState = channelStates[note.Channel - 1];
                var frequency = synthesisEngine.GetFrequency(note.NoteNumber, channelState.PitchBend, note.CentOffset);
                var amplitude = note.Velocity / 127.0f * config.Audio.MasterVolume;

                var startSample = note.StartSample;
                var endSample = note.EndSample;

                var attackTime = config.Synthesis.DefaultAttack;
                var releaseTime = config.Synthesis.DefaultRelease;

                if (config.Synthesis.EnableAntiPop)
                {
                    attackTime = Math.Max(attackTime, config.Synthesis.AntiPopAttackSeconds);
                    releaseTime = Math.Max(releaseTime, config.Synthesis.AntiPopReleaseSeconds);
                }

                var attackSamples = (long)(attackTime * sampleRate);
                var releaseSamples = (long)(releaseTime * sampleRate);

                long bufferStartSample = startSample - startSampleOffset;
                long bufferEndSample = endSample - startSampleOffset;

                for (long i = bufferStartSample; i < bufferEndSample; i++)
                {
                    if (i * 2 + 1 >= buffer.Length || i < 0) continue;

                    long currentAbsoluteSample = i + startSampleOffset;
                    var time = (currentAbsoluteSample - startSample) / (double)sampleRate;

                    long samplesIntoNote = currentAbsoluteSample - startSample;
                    long samplesToEnd = endSample - currentAbsoluteSample;

                    double attackFade = 1.0;
                    if (attackSamples > 0 && samplesIntoNote < attackSamples)
                    {
                        attackFade = (double)samplesIntoNote / attackSamples;
                    }

                    double releaseFade = 1.0;
                    if (releaseSamples > 0 && samplesToEnd < releaseSamples)
                    {
                        releaseFade = (double)samplesToEnd / releaseSamples;
                    }

                    double envelope = Math.Min(attackFade, releaseFade);

                    var waveType = config.Synthesis.DefaultWaveform;
                    var sampleValue = amplitude * envelope * synthesisEngine.GenerateBasicWaveform(waveType, frequency, time);
                    buffer[(int)(i * 2)] += (float)sampleValue;
                    buffer[(int)(i * 2 + 1)] += (float)sampleValue;
                }
            }
        }

        public bool RenderAudioGpuRealtime(GraphicsDevice device, Span<float> buffer, List<EnhancedNoteEvent> noteEvents, Dictionary<int, ChannelState> channelStates, Dictionary<int, InstrumentSettings> instrumentSettings, long startSampleOffset)
        {
            if (device == null) return false;

            try
            {
                var noteDataList = CreateGpuNoteDataList(noteEvents, channelStates, instrumentSettings, startSampleOffset);

                if (!noteDataList.Any())
                {
                    buffer.Clear();
                    return true;
                }

                buffer.Clear();
                using var gpuBuffer = device.AllocateReadWriteBuffer<float>(buffer);
                using var noteBuffer = device.AllocateReadOnlyBuffer(noteDataList.ToArray());

                int numSamples = buffer.Length / 2;
                var shader = new WaveformGenerationShader(
                    gpuBuffer,
                    noteBuffer,
                    sampleRate,
                    0,
                    (float)config.Synthesis.FmModulatorFrequency,
                    (float)config.Synthesis.FmModulationIndex,
                    config.Synthesis.EnableBandlimitedSynthesis,
                    config.Synthesis.EnableNoteCrossfade ? (int)(config.Synthesis.NoteCrossfadeDuration * sampleRate) : 0
                );
                device.For(numSamples, shader);

                gpuBuffer.CopyTo(buffer);
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error(LogMessages.GpuRenderError, ex);
                return false;
            }
        }


        private void RenderNoteWithEffects(EnhancedNoteEvent note, Span<float> buffer, ChannelState channelState,
                                         Dictionary<int, InstrumentSettings> instrumentSettings)
        {
            var instrument = synthesisEngine.GetInstrumentSettings(note.Channel, channelState.Program, instrumentSettings);

            var startSample = note.StartSample;
            var endSample = note.EndSample;
            var noteDuration = endSample - startSample;

            object envelope;
            if (instrument.AmplitudeEnvelope != null && instrument.AmplitudeEnvelope.Any())
            {
                envelope = new EnvelopeGenerator(
                    instrument.AmplitudeEnvelope,
                    instrument.Release * config.Synthesis.EnvelopeScale * channelState.ReleaseMultiplier,
                    sampleRate,
                    config);
            }
            else
            {
                envelope = new ADSREnvelope(
                    instrument.Attack * config.Synthesis.EnvelopeScale * channelState.AttackMultiplier,
                    instrument.Decay * config.Synthesis.EnvelopeScale * channelState.DecayMultiplier,
                    instrument.Sustain,
                    instrument.Release * config.Synthesis.EnvelopeScale * channelState.ReleaseMultiplier,
                    noteDuration,
                    sampleRate,
                    config
                );
            }

            var fadeSamples = config.Synthesis.EnableNoteCrossfade ? (long)(config.Synthesis.NoteCrossfadeDuration * sampleRate) : 0;

            for (long i = startSample; i < endSample && i < buffer.Length / 2; i++)
            {
                var time = (i - startSample) / (double)sampleRate;

                var pitchLfoValue = synthesisEngine.GetLfoValue(instrument.PitchLfo, time) * instrument.PitchLfo.Depth;
                var ampLfoValue = 1.0 + synthesisEngine.GetLfoValue(instrument.AmplitudeLfo, time) * instrument.AmplitudeLfo.Depth;

                var frequency = synthesisEngine.GetFrequency(note.NoteNumber, channelState.PitchBend, note.CentOffset, pitchLfoValue);

                var baseAmplitude = (note.Velocity / 127.0f) * channelState.Volume * channelState.Expression *
                                   instrument.VolumeMultiplier * config.Audio.MasterVolume * ampLfoValue;

                double envelopeValue;
                if (envelope is EnvelopeGenerator eg)
                {
                    envelopeValue = eg.GetValue(i - startSample, noteDuration);
                }
                else
                {
                    envelopeValue = ((ADSREnvelope)envelope).GetValue(i - startSample);
                }

                if (channelState.Sustain && envelopeValue > instrument.Sustain)
                {
                    envelopeValue = Math.Max(envelopeValue, instrument.Sustain);
                }

                if (fadeSamples > 0)
                {
                    long samplesIntoNote = i - startSample;
                    long samplesToEnd = endSample - i;
                    double fadeMultiplier = 1.0;

                    if (samplesIntoNote < fadeSamples)
                    {
                        fadeMultiplier = (double)samplesIntoNote / fadeSamples;
                    }
                    else if (samplesToEnd < fadeSamples)
                    {
                        fadeMultiplier = (double)samplesToEnd / fadeSamples;
                    }
                    envelopeValue *= fadeMultiplier;
                }

                var waveValue = synthesisEngine.GenerateWaveform(instrument.WaveType, frequency, time, (float)baseAmplitude, envelopeValue, note.NoteNumber, instrument.UserWavetableFile);
                waveValue = filterProcessor.ApplyFilters(waveValue, instrument, time, channelState);
                waveValue = effectsProcessor.ApplyChannelEffects(waveValue, channelState, time);

                var leftIndex = (int)(i * 2);
                var rightIndex = (int)(i * 2 + 1);

                if (rightIndex < buffer.Length)
                {
                    var panAngle = (channelState.Pan + 1) * Math.PI / 4;
                    var panLeft = (float)Math.Cos(panAngle);
                    var panRight = (float)Math.Sin(panAngle);

                    buffer[leftIndex] += waveValue * panLeft;
                    buffer[rightIndex] += waveValue * panRight;
                }
            }
        }
    }
}
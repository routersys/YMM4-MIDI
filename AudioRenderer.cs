using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using ComputeSharp;

namespace MIDI
{
    public class AudioRenderer
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;
        private readonly SynthesisEngine synthesisEngine;
        private readonly FilterProcessor filterProcessor;
        private readonly EffectsProcessor effectsProcessor;
        private readonly object bufferLock = new object();
        private const int GpuChunkSize = 1 << 18;

        public AudioRenderer(MidiConfiguration config, int sampleRate)
        {
            this.config = config;
            this.sampleRate = sampleRate;
            this.synthesisEngine = new SynthesisEngine(config, sampleRate);
            this.filterProcessor = new FilterProcessor(sampleRate);
            var gpuDevice = (config.Performance.RenderingMode == RenderingMode.HighQualityGPU || config.Performance.RenderingMode == RenderingMode.RealtimeGPU) ? GraphicsDevice.GetDefault() : null;
            this.effectsProcessor = new EffectsProcessor(config, sampleRate, gpuDevice);
        }

        public void RenderAudioHighQuality(float[] buffer, List<EnhancedNoteEvent> noteEvents,
                                         Dictionary<int, ChannelState> channelStates,
                                         Dictionary<int, InstrumentSettings> instrumentSettings)
        {
            var groupedNotes = noteEvents.GroupBy(n => n.Channel).ToDictionary(g => g.Key, g => g.ToList());

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = config.Performance.MaxThreads
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

                    if (config.Synthesis.EnableNoteCrossfade)
                    {
                        ApplyNoteCrossfade(notes, channelSpan);
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

        private void ApplyNoteCrossfade(List<EnhancedNoteEvent> notes, Span<float> buffer)
        {
            var fadeSamples = (long)(config.Synthesis.NoteCrossfadeDuration * sampleRate);
            if (fadeSamples <= 0) return;

            foreach (var note in notes)
            {
                for (long i = 0; i < fadeSamples; i++)
                {
                    var sampleIndex = note.EndSample + i;
                    var bufferIndex = sampleIndex * 2;
                    if (bufferIndex + 1 >= buffer.Length) break;

                    float multiplier = 1.0f - (float)i / fadeSamples;
                    buffer[(int)bufferIndex] *= multiplier;
                    buffer[(int)bufferIndex + 1] *= multiplier;
                }
            }
        }


        public bool RenderAudioGpu(GraphicsDevice device, Span<float> buffer, List<EnhancedNoteEvent> noteEvents, Dictionary<int, ChannelState> channelStates, Dictionary<int, InstrumentSettings> instrumentSettings)
        {
            try
            {
                var noteDataList = new List<GpuNoteData>();
                foreach (var note in noteEvents)
                {
                    if (note.Channel < 1 || note.Channel > 16) continue;
                    var channelState = channelStates[note.Channel - 1];
                    var instrument = synthesisEngine.GetInstrumentSettings(note.Channel, channelState.Program, instrumentSettings);
                    var frequency = (float)synthesisEngine.GetFrequency(note.NoteNumber, channelState.PitchBend);
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
                        StartSample = (int)note.StartSample,
                        EndSample = (int)note.EndSample,
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

                using var gpuBuffer = device.AllocateReadWriteBuffer<float>(buffer);
                using var noteBuffer = device.AllocateReadOnlyBuffer(noteDataList.ToArray());

                int numSamples = buffer.Length / 2;
                for (int offset = 0; offset < numSamples; offset += GpuChunkSize)
                {
                    int count = Math.Min(GpuChunkSize, numSamples - offset);
                    var shader = new WaveformGenerationShader(
                        gpuBuffer,
                        noteBuffer,
                        noteBuffer.Length,
                        sampleRate,
                        offset,
                        (float)config.Synthesis.FmModulatorFrequency,
                        (float)config.Synthesis.FmModulationIndex,
                        config.Synthesis.EnableBandlimitedSynthesis
                    );
                    device.For(count, shader);
                }

                gpuBuffer.CopyTo(buffer);
                return true;
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is ArgumentOutOfRangeException || ex is NotSupportedException || ex is OutOfMemoryException)
            {
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
                var frequency = synthesisEngine.GetFrequency(note.NoteNumber, channelState.PitchBend);
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

                    var time = (i + startSampleOffset - startSample) / (double)sampleRate;
                    double envelope = 1.0;

                    if (i + startSampleOffset - startSample < attackSamples && attackSamples > 0)
                        envelope = (double)(i + startSampleOffset - startSample) / attackSamples;
                    else if (endSample - (i + startSampleOffset) < releaseSamples && releaseSamples > 0)
                        envelope = (double)(endSample - (i + startSampleOffset)) / releaseSamples;

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
                var noteDataList = new List<GpuNoteData>();
                foreach (var note in noteEvents)
                {
                    if (note.Channel < 1 || note.Channel > 16) continue;
                    var channelState = channelStates[note.Channel - 1];
                    var instrument = synthesisEngine.GetInstrumentSettings(note.Channel, channelState.Program, instrumentSettings);
                    var frequency = (float)synthesisEngine.GetFrequency(note.NoteNumber, channelState.PitchBend);
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
                        StartSample = (int)(note.StartSample - startSampleOffset),
                        EndSample = (int)(note.EndSample - startSampleOffset),
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

                using var gpuBuffer = device.AllocateReadWriteBuffer<float>(buffer.Length);
                using var noteBuffer = device.AllocateReadOnlyBuffer(noteDataList.ToArray());

                int numSamples = buffer.Length / 2;
                var shader = new WaveformGenerationShader(
                    gpuBuffer,
                    noteBuffer,
                    noteBuffer.Length,
                    sampleRate,
                    0,
                    (float)config.Synthesis.FmModulatorFrequency,
                    (float)config.Synthesis.FmModulationIndex,
                    config.Synthesis.EnableBandlimitedSynthesis
                );
                device.For(numSamples, shader);

                gpuBuffer.CopyTo(buffer);
                return true;
            }
            catch (Exception ex) when (ex is ObjectDisposedException || ex is ArgumentOutOfRangeException || ex is NotSupportedException || ex is OutOfMemoryException)
            {
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

            for (long i = startSample; i < endSample && i < buffer.Length / 2; i++)
            {
                var time = (i - startSample) / (double)sampleRate;

                var pitchLfoValue = synthesisEngine.GetLfoValue(instrument.PitchLfo, time) * instrument.PitchLfo.Depth;
                var ampLfoValue = 1.0 + synthesisEngine.GetLfoValue(instrument.AmplitudeLfo, time) * instrument.AmplitudeLfo.Depth;

                var frequency = synthesisEngine.GetFrequency(note.NoteNumber, channelState.PitchBend, pitchLfoValue);

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
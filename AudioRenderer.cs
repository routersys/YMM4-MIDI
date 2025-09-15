using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MIDI
{
    public class AudioRenderer
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;
        private readonly SynthesisEngine synthesisEngine;
        private readonly FilterProcessor filterProcessor;
        private readonly EffectsProcessor effectsProcessor;

        public AudioRenderer(MidiConfiguration config, int sampleRate)
        {
            this.config = config;
            this.sampleRate = sampleRate;
            this.synthesisEngine = new SynthesisEngine(config, sampleRate);
            this.filterProcessor = new FilterProcessor(sampleRate);
            this.effectsProcessor = new EffectsProcessor(config, sampleRate);
        }

        public void RenderAudioHighQuality(List<EnhancedNoteEvent> noteEvents, float[] buffer,
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
                var notes = kvp.Value;
                var channelBuffer = new float[buffer.Length];
                var channelState = channelStates[channel];

                foreach (var note in notes)
                {
                    RenderNoteWithEffects(note, channelBuffer, channelState, instrumentSettings);
                }

                lock (buffer)
                {
                    for (int i = 0; i < channelBuffer.Length; i++)
                    {
                        buffer[i] += channelBuffer[i];
                    }
                }
            });
        }

        public void RenderAudioStandard(List<EnhancedNoteEvent> noteEvents, float[] buffer,
                                      Dictionary<int, ChannelState> channelStates)
        {
            foreach (var note in noteEvents)
            {
                var channelState = channelStates[note.Channel];
                var frequency = synthesisEngine.GetFrequency(note.NoteNumber, channelState.PitchBend);
                var amplitude = note.Velocity / 127.0f * config.Audio.MasterVolume;
                var startSample = (long)(note.StartTime.TotalSeconds * sampleRate);
                var endSample = (long)(note.EndTime.TotalSeconds * sampleRate);

                var attackTime = config.Synthesis.DefaultAttack;
                var releaseTime = config.Synthesis.DefaultRelease;
                var attackSamples = (long)(attackTime * sampleRate);
                var releaseSamples = (long)(releaseTime * sampleRate);

                for (long i = startSample; i < endSample && i * 2 + 1 < buffer.Length; i++)
                {
                    var time = (i - startSample) / (double)sampleRate;
                    double envelope = 1.0;

                    if (i - startSample < attackSamples)
                        envelope = (double)(i - startSample) / attackSamples;
                    else if (endSample - i < releaseSamples)
                        envelope = (double)(endSample - i) / releaseSamples;

                    var waveType = config.Synthesis.DefaultWaveform;
                    var sampleValue = amplitude * envelope * synthesisEngine.GenerateBasicWaveform(waveType, frequency, time);
                    buffer[i * 2] += (float)sampleValue;
                    buffer[i * 2 + 1] += (float)sampleValue;
                }
            }
        }

        private void RenderNoteWithEffects(EnhancedNoteEvent note, float[] buffer, ChannelState channelState,
                                         Dictionary<int, InstrumentSettings> instrumentSettings)
        {
            var instrument = synthesisEngine.GetInstrumentSettings(note.Channel, channelState.Program, instrumentSettings);
            var frequency = synthesisEngine.GetFrequency(note.NoteNumber, channelState.PitchBend);
            var baseAmplitude = (note.Velocity / 127.0f) * channelState.Volume * channelState.Expression *
                               instrument.VolumeMultiplier * config.Audio.MasterVolume;

            var startSample = (long)(note.StartTime.TotalSeconds * sampleRate);
            var endSample = (long)(note.EndTime.TotalSeconds * sampleRate);
            var noteDuration = endSample - startSample;

            var envelope = new ADSREnvelope(
                instrument.Attack * config.Synthesis.EnvelopeScale * channelState.AttackMultiplier,
                instrument.Decay * config.Synthesis.EnvelopeScale * channelState.DecayMultiplier,
                instrument.Sustain,
                instrument.Release * config.Synthesis.EnvelopeScale * channelState.ReleaseMultiplier,
                noteDuration,
                sampleRate
            );

            for (long i = startSample; i < endSample && i < buffer.Length / 2; i++)
            {
                var time = (i - startSample) / (double)sampleRate;
                var envelopeValue = (float)envelope.GetValue(i - startSample);

                var amplitude = baseAmplitude;
                if (channelState.Sustain && envelopeValue > instrument.Sustain)
                {
                    envelopeValue = (float)Math.Max(envelopeValue, instrument.Sustain);
                }

                var waveValue = synthesisEngine.GenerateWaveform(instrument.WaveType, frequency, time, amplitude, envelopeValue);
                waveValue = filterProcessor.ApplyFilters(waveValue, instrument, time, channelState);
                waveValue = effectsProcessor.ApplyChannelEffects(waveValue, channelState, time);

                var leftIndex = i * 2;
                var rightIndex = i * 2 + 1;

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
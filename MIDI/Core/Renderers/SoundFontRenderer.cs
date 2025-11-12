using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ComputeSharp;
using MeltySynth;
using MIDI.Configuration.Models;
using MIDI.Core;
using NAudio.Midi;

namespace MIDI.Renderers
{
    public class SoundFontRenderer : IAudioRenderer
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;
        private readonly string midiFilePath;
        private readonly List<string> sf2Paths;
        private readonly List<Synthesizer> synthesizers;
        private List<MidiFileSequencer> sequencers;
        private readonly MeltySynth.MidiFile midiFile;
        private readonly EffectsProcessor effectsProcessor;
        private long internalSamplePosition = 0;

        private GraphicsDevice? GpuDevice => GpuDeviceProvider.GetDevice();

        public SoundFontRenderer(string midiFilePath, MidiConfiguration config, int sampleRate, List<string> sf2Paths)
        {
            this.midiFilePath = midiFilePath;
            this.config = config;
            this.sampleRate = sampleRate;
            this.sf2Paths = sf2Paths;
            this.effectsProcessor = new EffectsProcessor(config, sampleRate, GpuDevice);

            midiFile = new MeltySynth.MidiFile(midiFilePath);

            synthesizers = new List<Synthesizer>();
            foreach (var sf2Path in sf2Paths)
            {
                var synthesizer = new Synthesizer(sf2Path, sampleRate);
                synthesizers.Add(synthesizer);
            }

            sequencers = new List<MidiFileSequencer>();
            ResetSequencers();
            ProcessChannelModeMessages();
        }

        private void ResetSequencers()
        {
            sequencers.Clear();
            foreach (var synthesizer in synthesizers)
            {
                var sequencer = new MidiFileSequencer(synthesizer);
                sequencer.Play(midiFile, false);
                sequencers.Add(sequencer);
            }
        }

        private void ResetSynthesizersAndSequencers()
        {
            foreach (var synthesizer in synthesizers)
            {
                synthesizer.Reset();
            }
            ResetSequencers();
        }

        private void ProcessChannelModeMessages()
        {
            var naudioMidiFile = new NAudio.Midi.MidiFile(midiFilePath, false);
            foreach (var track in naudioMidiFile.Events)
            {
                foreach (var midiEvent in track)
                {
                    if (midiEvent.CommandCode == MidiCommandCode.ControlChange)
                    {
                        var cc = (ControlChangeEvent)midiEvent;
                        foreach (var synthesizer in synthesizers)
                        {
                            switch (cc.Controller)
                            {
                                case (MidiController)120:
                                    synthesizer.NoteOffAll(true);
                                    break;
                                case (MidiController)121:
                                    synthesizer.Reset();
                                    break;
                                case (MidiController)123:
                                    synthesizer.NoteOffAll(false);
                                    break;
                            }
                        }
                    }
                }
            }
        }

        public float[] Render(string midiFilePath, TimeSpan? durationLimit)
        {
            var renderDuration = durationLimit ?? midiFile.Length.Add(TimeSpan.FromSeconds(2.0));
            var totalStereoSamples = (long)(renderDuration.TotalSeconds * sampleRate);
            if (totalStereoSamples <= 0) return Array.Empty<float>();

            var finalBuffer = new float[totalStereoSamples * 2];
            var finalBufferSpan = finalBuffer.AsSpan();

            var bufferSize = config.Performance.BufferSize;
            var leftBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            var rightBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            var mixLeftBuffer = ArrayPool<float>.Shared.Rent(bufferSize);
            var mixRightBuffer = ArrayPool<float>.Shared.Rent(bufferSize);

            try
            {
                ResetSynthesizersAndSequencers();

                long renderedStereoSamples = 0;
                while (renderedStereoSamples < totalStereoSamples)
                {
                    int samplesToRenderInBlock = (int)Math.Min(bufferSize, totalStereoSamples - renderedStereoSamples);
                    if (samplesToRenderInBlock <= 0) break;

                    var mixLeftSpan = mixLeftBuffer.AsSpan(0, samplesToRenderInBlock);
                    var mixRightSpan = mixRightBuffer.AsSpan(0, samplesToRenderInBlock);
                    mixLeftSpan.Clear();
                    mixRightSpan.Clear();

                    bool anySequencerActive = false;
                    foreach (var sequencer in sequencers)
                    {
                        if (sequencer.EndOfSequence)
                        {
                            continue;
                        }
                        anySequencerActive = true;

                        var tempLeftSpan = leftBuffer.AsSpan(0, samplesToRenderInBlock);
                        var tempRightSpan = rightBuffer.AsSpan(0, samplesToRenderInBlock);

                        sequencer.Render(tempLeftSpan, tempRightSpan);

                        for (int i = 0; i < samplesToRenderInBlock; i++)
                        {
                            mixLeftSpan[i] += tempLeftSpan[i];
                            mixRightSpan[i] += tempRightSpan[i];
                        }
                    }

                    if (!anySequencerActive)
                    {
                        break;
                    }

                    for (int i = 0; i < samplesToRenderInBlock; i++)
                    {
                        long bufferIndex = (renderedStereoSamples + i) * 2;
                        finalBufferSpan[(int)bufferIndex] = mixLeftBuffer[i] * config.Audio.MasterVolume;
                        finalBufferSpan[(int)bufferIndex + 1] = mixRightBuffer[i] * config.Audio.MasterVolume;
                    }

                    renderedStereoSamples += samplesToRenderInBlock;
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(leftBuffer);
                ArrayPool<float>.Shared.Return(rightBuffer);
                ArrayPool<float>.Shared.Return(mixLeftBuffer);
                ArrayPool<float>.Shared.Return(mixRightBuffer);
            }

            if (config.Effects.EnableEffects)
            {
                effectsProcessor.ApplyAudioEnhancements(finalBufferSpan);
            }
            if (config.Audio.EnableNormalization)
            {
                effectsProcessor.NormalizeAudio(finalBufferSpan);
            }

            return finalBuffer;
        }

        private void PerformSeek(long samplePosition)
        {
            ResetSynthesizersAndSequencers();

            int seekBufferSize = 262144;
            var left = ArrayPool<float>.Shared.Rent(seekBufferSize);
            var right = ArrayPool<float>.Shared.Rent(seekBufferSize);

            try
            {
                foreach (var sequencer in sequencers)
                {
                    var samplesToRender = samplePosition;
                    if (samplesToRender > 0)
                    {
                        while (samplesToRender > 0)
                        {
                            var count = (int)Math.Min(samplesToRender, seekBufferSize);
                            sequencer.Render(left.AsSpan(0, count), right.AsSpan(0, count));
                            samplesToRender -= count;
                        }
                    }
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(left);
                ArrayPool<float>.Shared.Return(right);
            }

            this.internalSamplePosition = samplePosition;
        }

        public void Seek(long samplePosition)
        {
            PerformSeek(samplePosition);
        }

        public int Read(Span<float> buffer, long position)
        {
            var requestedSamplePosition = position / 2;

            if (requestedSamplePosition != this.internalSamplePosition)
            {
                PerformSeek(requestedSamplePosition);
            }

            var numSamples = buffer.Length / 2;
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

                for (int i = 0; i < sequencers.Count; i++)
                {
                    var tempLeftSpan = tempLeft.AsSpan(0, numSamples);
                    var tempRightSpan = tempRight.AsSpan(0, numSamples);

                    sequencers[i].Render(tempLeftSpan, tempRightSpan);

                    for (int j = 0; j < numSamples; j++)
                    {
                        leftSpan[j] += tempLeftSpan[j];
                        rightSpan[j] += tempRightSpan[j];
                    }
                }

                for (int i = 0; i < numSamples; i++)
                {
                    buffer[i * 2] = left[i] * config.Audio.MasterVolume;
                    buffer[i * 2 + 1] = right[i] * config.Audio.MasterVolume;
                }
            }
            finally
            {
                ArrayPool<float>.Shared.Return(left);
                ArrayPool<float>.Shared.Return(right);
                ArrayPool<float>.Shared.Return(tempLeft);
                ArrayPool<float>.Shared.Return(tempRight);
            }

            this.internalSamplePosition += numSamples;

            return buffer.Length;
        }

        public void Dispose()
        {
            effectsProcessor?.Dispose();
        }
    }
}
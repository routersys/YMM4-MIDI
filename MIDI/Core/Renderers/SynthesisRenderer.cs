using System;
using System.Collections.Generic;
using System.Linq;
using ComputeSharp;
using MIDI.Configuration.Models;
using MIDI.Utils;
using NAudio.Midi;
using MIDI.Core;

namespace MIDI.Renderers
{
    public class SynthesisRenderer : IAudioRenderer
    {
        private readonly MidiConfiguration config;
        private readonly int sampleRate;
        private readonly AudioRenderer audioRenderer;
        private readonly IEffectsProcessor effectsProcessor;
        private readonly ISynthesisEngine synthesisEngine;

        private readonly List<EnhancedNoteEvent> allNoteEvents;
        private readonly List<ControlEvent> allControlEvents;

        private GraphicsDevice? GpuDevice => GpuDeviceProvider.GetDevice();


        public SynthesisRenderer(string midiFilePath, MidiConfiguration config, int sampleRate)
        {
            this.config = config;
            this.sampleRate = sampleRate;

            this.synthesisEngine = new SynthesisEngine(config, sampleRate);
            IFilterProcessor filterProcessor = new FilterProcessor(sampleRate);
            this.effectsProcessor = new EffectsProcessor(config, sampleRate, GpuDevice);
            this.audioRenderer = new AudioRenderer(config, sampleRate, this.synthesisEngine, filterProcessor, this.effectsProcessor);


            var naudioMidiFile = new NAudio.Midi.MidiFile(midiFilePath, false);
            var tempoMap = MidiProcessor.ExtractTempoMap(naudioMidiFile, config);
            allNoteEvents = MidiProcessor.ExtractNoteEvents(naudioMidiFile, naudioMidiFile.DeltaTicksPerQuarterNote, tempoMap, config, sampleRate);
            allControlEvents = MidiProcessor.ExtractControlEvents(naudioMidiFile, naudioMidiFile.DeltaTicksPerQuarterNote, tempoMap, config);
        }

        public float[] Render(string midiFilePath, TimeSpan? durationLimit)
        {
            var naudioMidiFile = new NAudio.Midi.MidiFile(midiFilePath, false);
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

            bool gpuSucceeded = false;
            if (config.Performance.RenderingMode == RenderingMode.HighQualityGPU && GpuDevice != null)
            {
                gpuSucceeded = audioRenderer.RenderAudioGpu(GpuDevice, bufferSpan, noteEvents, channelStates, currentInstrumentSettings);
                if (!gpuSucceeded)
                {
                    Logger.Warn(LogMessages.GpuRenderFallback);
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

            if (!gpuSucceeded && config.Performance.RenderingMode == RenderingMode.HighQualityGPU)
            {
                Logger.Info(LogMessages.GpuToCpuFallback);
            }

            return buffer;
        }

        public void Seek(long samplePosition)
        {
        }

        public int Read(Span<float> buffer, long position)
        {
            var startSample = position / 2;
            var numSamples = buffer.Length / 2;
            var endSample = startSample + numSamples;

            buffer.Clear();

            var channelStates = GetChannelStatesAtSample(startSample);
            var notesInChunk = allNoteEvents.Where(n => n.StartSample < endSample && n.EndSample > startSample).ToList();

            bool useGpu = config.Performance.RenderingMode == RenderingMode.RealtimeGPU && GpuDevice != null;
            bool gpuRenderedSuccessfully = false;

            if (useGpu)
            {
                if (audioRenderer.RenderAudioGpuRealtime(GpuDevice!, buffer, notesInChunk, channelStates, synthesisEngine.InitializeInstrumentSettings(), startSample))
                {
                    gpuRenderedSuccessfully = true;
                }
                else
                {
                    Logger.Warn(LogMessages.RealtimeGpuRenderFallback);
                    audioRenderer.RenderAudioStandard(buffer, notesInChunk, channelStates, startSample);
                }
            }
            else
            {
                audioRenderer.RenderAudioStandard(buffer, notesInChunk, channelStates, startSample);
            }

            if (useGpu && gpuRenderedSuccessfully)
            {
                Logger.Info(LogMessages.RealtimeGpuRenderSuccess);
            }

            return buffer.Length;
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

        public void Dispose()
        {
            effectsProcessor?.Dispose();
        }
    }
}
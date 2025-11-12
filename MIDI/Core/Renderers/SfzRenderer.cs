using System;
using System.Collections.Generic;
using System.Linq;
using NAudio.Midi;

namespace MIDI.Renderers
{
    public class SfzRenderer : IAudioRenderer
    {
        private readonly SfzProcessor sfzProcessor;
        private readonly SfzRealtimeState sfzState;
        private readonly MidiConfiguration config;

        public SfzRenderer(string midiFilePath, MidiConfiguration config, int sampleRate)
        {
            this.config = config;
            sfzProcessor = new SfzProcessor(config, sampleRate);

            var naudioMidiFile = new NAudio.Midi.MidiFile(midiFilePath, false);
            var tempoMap = MidiProcessor.ExtractTempoMap(naudioMidiFile, config);
            var allUniversalMidiEvents = MidiProcessor.ExtractAllMidiEvents(naudioMidiFile, naudioMidiFile.DeltaTicksPerQuarterNote, tempoMap, sampleRate);
            sfzState = sfzProcessor.Initialize(allUniversalMidiEvents);
        }

        public float[] Render(string midiFilePath, TimeSpan? durationLimit)
        {
            return sfzProcessor.ProcessWithSfz(midiFilePath, durationLimit);
        }

        public void Seek(long samplePosition)
        {
            sfzProcessor.Seek(sfzState, samplePosition);
        }

        public int Read(Span<float> buffer, long position)
        {
            var startSample = position / 2;
            sfzProcessor.RenderRealtimeChunk(sfzState, buffer, startSample);

            return buffer.Length;
        }

        public void Dispose()
        {
            sfzState?.Dispose();
        }
    }
}
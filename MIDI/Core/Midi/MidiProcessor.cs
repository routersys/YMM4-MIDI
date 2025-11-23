using System;
using System.Collections.Generic;
using System.Linq;
using MIDI.Core.Midi.Handlers;
using NAudio.Midi;

namespace MIDI
{
    public static class MidiProcessor
    {
        public static TimeSpan TicksToTimeSpan(long ticks, int ticksPerQuarterNote, List<TempoEvent> tempoMap)
        {
            double seconds = 0;
            long lastTicks = 0;

            if (tempoMap == null || !tempoMap.Any())
            {
                tempoMap = new List<TempoEvent> { new TempoEvent(500000, 0) };
            }

            double currentTempo = tempoMap[0].MicrosecondsPerQuarterNote;

            foreach (var tempoEvent in tempoMap)
            {
                if (ticks < tempoEvent.AbsoluteTime)
                {
                    break;
                }

                long deltaTicks = tempoEvent.AbsoluteTime - lastTicks;
                seconds += (deltaTicks / (double)ticksPerQuarterNote) * (currentTempo / 1000000.0);

                currentTempo = tempoEvent.MicrosecondsPerQuarterNote;
                lastTicks = tempoEvent.AbsoluteTime;
            }

            long remainingTicks = ticks - lastTicks;
            seconds += (remainingTicks / (double)ticksPerQuarterNote) * (currentTempo / 1000000.0);

            return TimeSpan.FromSeconds(seconds);
        }

        public static long TimeToTicks(TimeSpan time, int ticksPerQuarterNote, List<TempoEvent> tempoMap)
        {
            if (time <= TimeSpan.Zero) return 0;
            if (tempoMap == null || !tempoMap.Any())
            {
                tempoMap = new List<TempoEvent> { new TempoEvent(500000, 0) };
            }

            double accumulatedSeconds = 0;
            long lastTicks = 0;
            double lastTempo = tempoMap[0].MicrosecondsPerQuarterNote;

            foreach (var tempoEvent in tempoMap)
            {
                if (tempoEvent.AbsoluteTime > 0)
                {
                    long deltaTicks = tempoEvent.AbsoluteTime - lastTicks;
                    if (deltaTicks > 0)
                    {
                        double deltaSeconds = (deltaTicks / (double)ticksPerQuarterNote) * (lastTempo / 1000000.0);
                        if (accumulatedSeconds + deltaSeconds >= time.TotalSeconds)
                        {
                            double secondsIntoSegment = time.TotalSeconds - accumulatedSeconds;
                            long ticksIntoSegment = (long)(secondsIntoSegment * (1000000.0 / lastTempo) * ticksPerQuarterNote);
                            return lastTicks + ticksIntoSegment;
                        }
                        accumulatedSeconds += deltaSeconds;
                    }
                }
                lastTicks = tempoEvent.AbsoluteTime;
                lastTempo = tempoEvent.MicrosecondsPerQuarterNote;
            }

            double remainingSeconds = time.TotalSeconds - accumulatedSeconds;
            long remainingTicks = (long)(remainingSeconds * (1000000.0 / lastTempo) * ticksPerQuarterNote);
            return lastTicks + remainingTicks;
        }

        public static List<TempoEvent> ExtractTempoMap(NAudio.Midi.MidiFile midiFile, MidiConfiguration config)
        {
            var tempoMap = midiFile.Events.SelectMany(track => track)
                                        .OfType<TempoEvent>()
                                        .OrderBy(e => e.AbsoluteTime)
                                        .ToList();

            if (!tempoMap.Any() || tempoMap.First().AbsoluteTime > 0)
            {
                tempoMap.Insert(0, new TempoEvent(config.MIDI.DefaultTempo, 0));
            }
            return tempoMap;
        }

        public static List<EnhancedNoteEvent> ExtractNoteEvents(NAudio.Midi.MidiFile midiFile, int ticksPerQuarterNote, List<TempoEvent> tempoMap, MidiConfiguration config, int sampleRate)
        {
            var noteEvents = new List<EnhancedNoteEvent>();

            var centOffsetEvents = midiFile.Events
                .SelectMany(track => track.OfType<TextEvent>())
                .Where(e => e.Text.StartsWith("CENT_OFFSET:"))
                .ToDictionary(e => (e.AbsoluteTime, e.Channel, int.Parse(e.Text.Split(':')[1].Split(',')[1])), e => int.Parse(e.Text.Split(':')[1].Split(',')[2]));


            for (int trackIndex = 0; trackIndex < midiFile.Events.Tracks; trackIndex++)
            {
                if (config.MIDI.ExcludedTracks.Contains(trackIndex)) continue;

                foreach (var midiEvent in midiFile.Events[trackIndex])
                {
                    if (midiEvent is NoteOnEvent noteOn && noteOn.OffEvent != null)
                    {
                        if (config.MIDI.ExcludedChannels.Contains(noteOn.Channel)) continue;
                        if (noteOn.Velocity < config.MIDI.MinVelocity) continue;

                        centOffsetEvents.TryGetValue((noteOn.AbsoluteTime, noteOn.Channel, noteOn.NoteNumber), out var offset);
                        noteEvents.Add(new EnhancedNoteEvent(noteOn, ticksPerQuarterNote, tempoMap, trackIndex, sampleRate, offset));
                    }
                }
            }
            return noteEvents;
        }

        public static List<ControlEvent> ExtractControlEvents(NAudio.Midi.MidiFile midiFile, int ticksPerQuarterNote, List<TempoEvent> tempoMap, MidiConfiguration config)
        {
            var controlEvents = new List<ControlEvent>();

            for (int trackIndex = 0; trackIndex < midiFile.Events.Tracks; trackIndex++)
            {
                if (config.MIDI.ExcludedTracks.Contains(trackIndex)) continue;

                foreach (var midiEvent in midiFile.Events[trackIndex])
                {
                    if (config.MIDI.ExcludedChannels.Contains(midiEvent.Channel)) continue;

                    switch (midiEvent)
                    {
                        case ControlChangeEvent cc when config.MIDI.ProcessControlChanges:
                            controlEvents.Add(new ControlEvent(cc, ticksPerQuarterNote, tempoMap, trackIndex));
                            break;
                        case PitchWheelChangeEvent pw when config.MIDI.ProcessPitchBend:
                            controlEvents.Add(new ControlEvent(pw, ticksPerQuarterNote, tempoMap, trackIndex));
                            break;
                        case PatchChangeEvent pc when config.MIDI.ProcessProgramChanges:
                            controlEvents.Add(new ControlEvent(pc, ticksPerQuarterNote, tempoMap, trackIndex));
                            break;
                    }
                }
            }
            return controlEvents.OrderBy(e => e.Time).ToList();
        }

        public static List<UniversalMidiEvent> ExtractAllMidiEvents(NAudio.Midi.MidiFile midiFile, int ticksPerQuarterNote, List<TempoEvent> tempoMap, int sampleRate)
        {
            var events = new List<UniversalMidiEvent>();

            for (int trackIndex = 0; trackIndex < midiFile.Events.Tracks; trackIndex++)
            {
                foreach (var midiEvent in midiFile.Events[trackIndex])
                {
                    var time = TicksToTimeSpan(midiEvent.AbsoluteTime, ticksPerQuarterNote, tempoMap);
                    var sampleTime = (long)(time.TotalSeconds * sampleRate);

                    switch (midiEvent)
                    {
                        case NoteOnEvent e:
                            if (e.Velocity > 0 && e.OffEvent != null)
                                events.Add(new UniversalMidiEvent(UniversalMidiEventType.NoteOn, e.Channel, e.AbsoluteTime, sampleTime, e.NoteNumber, e.Velocity));
                            else if (e.Velocity == 0 && e.OffEvent != null)
                                events.Add(new UniversalMidiEvent(UniversalMidiEventType.NoteOff, e.Channel, e.AbsoluteTime, sampleTime, e.NoteNumber, e.Velocity));
                            break;
                        case NoteEvent ne when ne.CommandCode == MidiCommandCode.NoteOff:
                            events.Add(new UniversalMidiEvent(UniversalMidiEventType.NoteOff, ne.Channel, ne.AbsoluteTime, sampleTime, ne.NoteNumber, ne.Velocity));
                            break;
                        case ControlChangeEvent e:
                            events.Add(new UniversalMidiEvent(UniversalMidiEventType.ControlChange, e.Channel, e.AbsoluteTime, sampleTime, (int)e.Controller, e.ControllerValue));
                            break;
                        case PitchWheelChangeEvent e:
                            events.Add(new UniversalMidiEvent(UniversalMidiEventType.PitchWheel, e.Channel, e.AbsoluteTime, sampleTime, e.Pitch & 0x7F, e.Pitch >> 7));
                            break;
                        case PatchChangeEvent e:
                            events.Add(new UniversalMidiEvent(UniversalMidiEventType.PatchChange, e.Channel, e.AbsoluteTime, sampleTime, e.Patch, 0));
                            break;
                    }
                }
            }
            return events.OrderBy(e => e.SampleTime).ToList();
        }

        public static void ApplyControlEvents(List<ControlEvent> controlEvents, Dictionary<int, ChannelState> channelStates, MidiConfiguration config)
        {
            foreach (var controlEvent in controlEvents)
            {
                if (controlEvent.Channel < 1 || controlEvent.Channel > 16) continue;
                var state = channelStates[controlEvent.Channel - 1];

                switch (controlEvent.Type)
                {
                    case ControlEventType.ControlChange:
                        ControlChangeHandler.ApplyControlChange(controlEvent, state);
                        break;
                    case ControlEventType.PitchWheel:
                        state.PitchBend = (float)(controlEvent.Value - 8192) / 8192.0f * (float)config.MIDI.PitchBendRange;
                        break;
                    case ControlEventType.ProgramChange:
                        state.Program = controlEvent.Value;
                        break;
                }
            }
        }
    }
}
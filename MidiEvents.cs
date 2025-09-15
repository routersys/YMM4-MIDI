using System;
using NAudio.Midi;
using System.Collections.Generic;

namespace MIDI
{
    public enum WaveformType
    {
        Sine,
        Square,
        Sawtooth,
        Triangle,
        Organ,
        Noise,
        Wavetable,
        Fm,
        KarplusStrong
    }

    public enum FilterType
    {
        None,
        LowPass,
        HighPass,
        BandPass
    }

    public enum ControlEventType
    {
        ControlChange,
        PitchWheel,
        ProgramChange
    }

    public enum UniversalMidiEventType
    {
        NoteOn,
        NoteOff,
        ControlChange,
        PitchWheel,
        PatchChange
    }

    public class EnhancedNoteEvent
    {
        public int NoteNumber { get; set; }
        public int Velocity { get; set; }
        public int Channel { get; set; }
        public int TrackIndex { get; set; }
        public long StartTicks { get; set; }
        public long EndTicks { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public long StartSample { get; set; }
        public long EndSample { get; set; }


        public EnhancedNoteEvent(NoteOnEvent noteOn, int ticksPerQuarterNote, List<TempoEvent> tempoMap, int trackIndex, int sampleRate)
        {
            NoteNumber = noteOn.NoteNumber;
            Velocity = noteOn.Velocity;
            Channel = noteOn.Channel;
            TrackIndex = trackIndex;
            StartTicks = noteOn.AbsoluteTime;
            EndTicks = noteOn.OffEvent.AbsoluteTime;
            StartTime = TicksToTimeSpan(StartTicks, ticksPerQuarterNote, tempoMap);
            EndTime = TicksToTimeSpan(EndTicks, ticksPerQuarterNote, tempoMap);
            StartSample = (long)(StartTime.TotalSeconds * sampleRate);
            EndSample = (long)(EndTime.TotalSeconds * sampleRate);
        }

        private static TimeSpan TicksToTimeSpan(long ticks, int ticksPerQuarterNote, List<TempoEvent> tempoMap)
        {
            double seconds = 0;
            long lastTicks = 0;
            double currentTempo = 500000.0;

            foreach (var tempoEvent in tempoMap)
            {
                if (ticks < tempoEvent.AbsoluteTime) break;
                long deltaTicks = tempoEvent.AbsoluteTime - lastTicks;
                seconds += (deltaTicks / (double)ticksPerQuarterNote) * (currentTempo / 1000000.0);
                currentTempo = tempoEvent.MicrosecondsPerQuarterNote;
                lastTicks = tempoEvent.AbsoluteTime;
            }

            long remainingTicks = ticks - lastTicks;
            seconds += (remainingTicks / (double)ticksPerQuarterNote) * (currentTempo / 1000000.0);
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public class ControlEvent
    {
        public TimeSpan Time { get; set; }
        public long Ticks { get; set; }
        public int Channel { get; set; }
        public int TrackIndex { get; set; }
        public ControlEventType Type { get; set; }
        public int Controller { get; set; }
        public int Value { get; set; }

        public ControlEvent(ControlChangeEvent cc, int ticksPerQuarterNote, List<TempoEvent> tempoMap, int trackIndex)
        {
            Ticks = cc.AbsoluteTime;
            Time = TicksToTimeSpan(Ticks, ticksPerQuarterNote, tempoMap);
            Channel = cc.Channel;
            TrackIndex = trackIndex;
            Type = ControlEventType.ControlChange;
            Controller = (int)cc.Controller;
            Value = cc.ControllerValue;
        }

        public ControlEvent(PitchWheelChangeEvent pw, int ticksPerQuarterNote, List<TempoEvent> tempoMap, int trackIndex)
        {
            Ticks = pw.AbsoluteTime;
            Time = TicksToTimeSpan(Ticks, ticksPerQuarterNote, tempoMap);
            Channel = pw.Channel;
            TrackIndex = trackIndex;
            Type = ControlEventType.PitchWheel;
            Controller = -1;
            Value = pw.Pitch;
        }

        public ControlEvent(PatchChangeEvent pc, int ticksPerQuarterNote, List<TempoEvent> tempoMap, int trackIndex)
        {
            Ticks = pc.AbsoluteTime;
            Time = TicksToTimeSpan(Ticks, ticksPerQuarterNote, tempoMap);
            Channel = pc.Channel;
            TrackIndex = trackIndex;
            Type = ControlEventType.ProgramChange;
            Controller = -1;
            Value = pc.Patch;
        }

        private static TimeSpan TicksToTimeSpan(long ticks, int ticksPerQuarterNote, List<TempoEvent> tempoMap)
        {
            double seconds = 0;
            long lastTicks = 0;
            double currentTempo = 500000.0;

            foreach (var tempoEvent in tempoMap)
            {
                if (ticks < tempoEvent.AbsoluteTime) break;
                long deltaTicks = tempoEvent.AbsoluteTime - lastTicks;
                seconds += (deltaTicks / (double)ticksPerQuarterNote) * (currentTempo / 1000000.0);
                currentTempo = tempoEvent.MicrosecondsPerQuarterNote;
                lastTicks = tempoEvent.AbsoluteTime;
            }

            long remainingTicks = ticks - lastTicks;
            seconds += (remainingTicks / (double)ticksPerQuarterNote) * (currentTempo / 1000000.0);
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public class UniversalMidiEvent
    {
        public UniversalMidiEventType Type { get; }
        public int Channel { get; }
        public long Ticks { get; }
        public long SampleTime { get; }
        public int Data1 { get; }
        public int Data2 { get; }

        public UniversalMidiEvent(UniversalMidiEventType type, int channel, long ticks, long sampleTime, int data1, int data2)
        {
            Type = type;
            Channel = channel;
            Ticks = ticks;
            SampleTime = sampleTime;
            Data1 = data1;
            Data2 = data2;
        }
    }

    public class ChannelState
    {
        public float Volume { get; set; } = 1.0f;
        public float Pan { get; set; } = 0.0f;
        public float PitchBend { get; set; } = 0.0f;
        public float Expression { get; set; } = 1.0f;
        public float Reverb { get; set; } = 0.0f;
        public float Chorus { get; set; } = 0.0f;
        public bool Sustain { get; set; } = false;
        public int Program { get; set; } = 0;
        public double AttackMultiplier { get; set; } = 1.0;
        public double DecayMultiplier { get; set; } = 1.0;
        public double ReleaseMultiplier { get; set; } = 1.0;
        public double FilterCutoffMultiplier { get; set; } = 1.0;
        public double FilterResonanceMultiplier { get; set; } = 1.0;
    }

    public class InstrumentSettings
    {
        public WaveformType WaveType { get; set; } = WaveformType.Sine;
        public double Attack { get; set; } = 0.01;
        public double Decay { get; set; } = 0.2;
        public double Sustain { get; set; } = 0.7;
        public double Release { get; set; } = 0.5;
        public float VolumeMultiplier { get; set; } = 1.0f;
        public FilterType FilterType { get; set; } = FilterType.None;
        public double FilterCutoff { get; set; } = 22050;
        public double FilterResonance { get; set; } = 1.0;
        public double FilterModulation { get; set; } = 0.0;
        public double FilterModulationRate { get; set; } = 5.0;
    }

    public class ADSREnvelope
    {
        private readonly double attack, decay, sustain, release;
        private readonly long attackSamples, decaySamples, releaseSamples;
        private readonly long totalSamples;

        public ADSREnvelope(double attack, double decay, double sustain, double release, long totalSamples, int sampleRate)
        {
            this.attack = attack;
            this.decay = decay;
            this.sustain = sustain;
            this.release = release;
            this.totalSamples = totalSamples;
            this.attackSamples = (long)(attack * sampleRate);
            this.decaySamples = (long)(decay * sampleRate);
            this.releaseSamples = (long)(release * sampleRate);
        }

        public double GetValue(long sample)
        {
            if (sample < 0) return 0;

            if (sample < attackSamples)
            {
                return sample / (double)attackSamples;
            }
            else if (sample < attackSamples + decaySamples)
            {
                var decayProgress = (sample - attackSamples) / (double)decaySamples;
                return 1.0 - (1.0 - sustain) * decayProgress;
            }
            else if (sample < totalSamples - releaseSamples)
            {
                return sustain;
            }
            else if (sample < totalSamples)
            {
                var releaseProgress = (sample - (totalSamples - releaseSamples)) / (double)releaseSamples;
                return sustain * (1.0 - releaseProgress);
            }
            else
            {
                return 0;
            }
        }
    }
}
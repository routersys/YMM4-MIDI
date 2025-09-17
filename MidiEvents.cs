using System;
using NAudio.Midi;
using System.Collections.Generic;
using System.Linq;

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
        UserWavetable,
        Fm,
        KarplusStrong
    }

    public enum FilterType
    {
        None,
        LowPass,
        HighPass,
        BandPass,
        Notch,
        Peak
    }

    public enum LfoWaveformType
    {
        Sine,
        Square,
        Sawtooth,
        Triangle,
        Noise,
        RandomHold
    }

    public enum LfoTarget
    {
        None,
        Pitch,
        Amplitude,
        FilterCutoff
    }

    public enum DistortionType
    {
        None,
        HardClip,
        SoftClip,
        Saturation
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
            StartTime = MidiProcessor.TicksToTimeSpan(StartTicks, ticksPerQuarterNote, tempoMap);
            EndTime = MidiProcessor.TicksToTimeSpan(EndTicks, ticksPerQuarterNote, tempoMap);
            StartSample = (long)(StartTime.TotalSeconds * sampleRate);
            EndSample = (long)(EndTime.TotalSeconds * sampleRate);
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
            Time = MidiProcessor.TicksToTimeSpan(Ticks, ticksPerQuarterNote, tempoMap);
            Channel = cc.Channel;
            TrackIndex = trackIndex;
            Type = ControlEventType.ControlChange;
            Controller = (int)cc.Controller;
            Value = cc.ControllerValue;
        }

        public ControlEvent(PitchWheelChangeEvent pw, int ticksPerQuarterNote, List<TempoEvent> tempoMap, int trackIndex)
        {
            Ticks = pw.AbsoluteTime;
            Time = MidiProcessor.TicksToTimeSpan(Ticks, ticksPerQuarterNote, tempoMap);
            Channel = pw.Channel;
            TrackIndex = trackIndex;
            Type = ControlEventType.PitchWheel;
            Controller = -1;
            Value = pw.Pitch;
        }

        public ControlEvent(PatchChangeEvent pc, int ticksPerQuarterNote, List<TempoEvent> tempoMap, int trackIndex)
        {
            Ticks = pc.AbsoluteTime;
            Time = MidiProcessor.TicksToTimeSpan(Ticks, ticksPerQuarterNote, tempoMap);
            Channel = pc.Channel;
            TrackIndex = trackIndex;
            Type = ControlEventType.ProgramChange;
            Controller = -1;
            Value = pc.Patch;
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
        public string UserWavetableFile { get; set; } = string.Empty;

        public double Attack { get; set; } = 0.01;
        public double Decay { get; set; } = 0.2;
        public double Sustain { get; set; } = 0.7;
        public double Release { get; set; } = 0.5;

        public List<EnvelopePoint> AmplitudeEnvelope { get; set; } = new List<EnvelopePoint>();

        public float VolumeMultiplier { get; set; } = 1.0f;
        public FilterType FilterType { get; set; } = FilterType.None;
        public double FilterCutoff { get; set; } = 22050;
        public double FilterResonance { get; set; } = 1.0;
        public double FilterModulation { get; set; } = 0.0;
        public double FilterModulationRate { get; set; } = 5.0;

        public LfoSettings PitchLfo { get; set; } = new LfoSettings();
        public LfoSettings AmplitudeLfo { get; set; } = new LfoSettings();
        public LfoSettings FilterLfo { get; set; } = new LfoSettings { Waveform = LfoWaveformType.Sine, Rate = 5.0, Depth = 0.0 };

        public LfoWaveformType LfoWaveform { get => FilterLfo.Waveform; set => FilterLfo.Waveform = value; }
        public double LfoRate { get => FilterLfo.Rate; set => FilterLfo.Rate = value; }
        public double LfoDepth { get => FilterLfo.Depth; set => FilterLfo.Depth = value; }
    }

    public class LfoSettings
    {
        public LfoWaveformType Waveform { get; set; } = LfoWaveformType.Sine;
        public double Rate { get; set; } = 5.0;
        public double Depth { get; set; } = 0.0;
    }

    public class EnvelopePoint
    {
        public double Time { get; set; }
        public double Value { get; set; }

        public EnvelopePoint() { }
        public EnvelopePoint(double time, double value)
        {
            Time = time;
            Value = value;
        }
    }

    public class EnvelopeGenerator
    {
        private readonly List<EnvelopePoint> points;
        private readonly double releaseTime;
        private readonly int sampleRate;

        private readonly long[] stageSamples;
        private readonly double[] stageValues;

        public EnvelopeGenerator(List<EnvelopePoint> envelopePoints, double releaseTime, int sampleRate, MidiConfiguration config)
        {
            this.sampleRate = sampleRate;
            this.releaseTime = Math.Max(config.Synthesis.AntiPopReleaseSeconds, releaseTime);

            if (envelopePoints == null || !envelopePoints.Any())
            {
                this.points = new List<EnvelopePoint>
                {
                    new EnvelopePoint(0.0, 0.0),
                    new EnvelopePoint(Math.Max(0.001, config.Synthesis.AntiPopAttackSeconds), 1.0),
                    new EnvelopePoint(0.2, 0.7),
                };
            }
            else
            {
                this.points = envelopePoints.OrderBy(p => p.Time).ToList();
                if (!this.points.Any() || this.points.First().Time > 0)
                {
                    this.points.Insert(0, new EnvelopePoint(0, 0));
                }
                if (this.points.Count > 1)
                {
                    this.points[1].Time = Math.Max(this.points[1].Time, config.Synthesis.AntiPopAttackSeconds);
                }
            }

            this.stageSamples = new long[this.points.Count];
            this.stageValues = new double[this.points.Count];

            for (int i = 0; i < this.points.Count; i++)
            {
                stageSamples[i] = (long)(this.points[i].Time * sampleRate);
                stageValues[i] = this.points[i].Value;
            }
        }

        public double GetValue(long sample, long noteDurationSamples)
        {
            if (sample < 0) return 0;

            long releaseSamples = (long)(releaseTime * sampleRate);
            long releaseStartSample = noteDurationSamples > releaseSamples ? noteDurationSamples - releaseSamples : 0;

            if (sample < releaseStartSample)
            {
                if (stageSamples.Length == 0) return 0.0;
                if (sample >= stageSamples.Last())
                {
                    return stageValues.Last();
                }

                for (int i = 1; i < stageSamples.Length; i++)
                {
                    if (sample < stageSamples[i])
                    {
                        long prevSample = stageSamples[i - 1];
                        double prevValue = stageValues[i - 1];
                        long currentSample = stageSamples[i];
                        double currentValue = stageValues[i];

                        if (currentSample <= prevSample) return currentValue;

                        double progress = (double)(sample - prevSample) / (currentSample - prevSample);
                        return prevValue + (currentValue - prevValue) * progress;
                    }
                }
                return stageValues.Last();
            }
            else
            {
                if (releaseSamples == 0) return 0.0;

                long sampleIntoRelease = sample - releaseStartSample;
                if (sampleIntoRelease < 0) return stageValues.Last();

                double releaseProgress = (double)sampleIntoRelease / releaseSamples;
                return stageValues.Last() * (1.0 - releaseProgress);
            }
        }
    }

    public class ADSREnvelope
    {
        private readonly double attack, decay, sustain, release;
        private readonly long attackSamples, decaySamples, releaseSamples;
        private readonly long totalSamples;

        public ADSREnvelope(double attack, double decay, double sustain, double release, long totalSamples, int sampleRate, MidiConfiguration config)
        {
            if (config != null && config.Synthesis.EnableAntiPop)
            {
                attack = Math.Max(attack, config.Synthesis.AntiPopAttackSeconds);
                release = Math.Max(release, config.Synthesis.AntiPopReleaseSeconds);
            }

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
                if (attackSamples == 0) return 1.0;
                return sample / (double)attackSamples;
            }
            else if (sample < attackSamples + decaySamples)
            {
                if (decaySamples == 0) return sustain;
                var decayProgress = (sample - attackSamples) / (double)decaySamples;
                return 1.0 - (1.0 - sustain) * decayProgress;
            }
            else if (sample < totalSamples - releaseSamples)
            {
                return sustain;
            }
            else if (sample < totalSamples)
            {
                if (releaseSamples == 0) return 0.0;
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
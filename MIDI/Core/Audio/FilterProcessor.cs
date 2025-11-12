using System;
using System.Collections.Concurrent;
using System.Threading;

namespace MIDI
{
    public class FilterProcessor : IFilterProcessor
    {
        private class FilterState
        {
            public double z1, z2;
        }

        private readonly int sampleRate;
        private readonly ConcurrentDictionary<(int, int), FilterState> filterStates = new ConcurrentDictionary<(int, int), FilterState>();
        private static readonly ThreadLocal<Random> threadRandom = new ThreadLocal<Random>(() => new Random(Guid.NewGuid().GetHashCode()));

        public FilterProcessor(int sampleRate)
        {
            this.sampleRate = sampleRate;
        }

        private double GetLfoValue(LfoSettings lfo, double time)
        {
            var phase = lfo.Rate * time;
            switch (lfo.Waveform)
            {
                case LfoWaveformType.Sine:
                    return Math.Sin(2 * Math.PI * phase);
                case LfoWaveformType.Square:
                    return Math.Sign(Math.Sin(2 * Math.PI * phase));
                case LfoWaveformType.Sawtooth:
                    return 2 * (phase - Math.Floor(phase + 0.5));
                case LfoWaveformType.Triangle:
                    return 4 * Math.Abs((phase - Math.Floor(phase + 0.75) + 0.25) % 1 - 0.5) - 1;
                case LfoWaveformType.Noise:
                    return threadRandom.Value!.NextDouble() * 2 - 1;
                case LfoWaveformType.RandomHold:
                    return (int)(2 * phase) % 2 == 0 ? 1 : -1;
                default:
                    return 0;
            }
        }

        public float ApplyFilters(float input, InstrumentSettings instrument, double time, ChannelState channelState)
        {
            if (instrument.FilterType == FilterType.None) return input;

            var lfoValue = GetLfoValue(instrument.FilterLfo, time);
            var lfoModulation = 1.0 + instrument.FilterLfo.Depth * lfoValue;

            var cutoffMod = (1.0 + instrument.FilterModulation * Math.Sin(2 * Math.PI * instrument.FilterModulationRate * time)) * lfoModulation;
            var cutoff = Math.Max(20.0, Math.Min(sampleRate / 2.0 - 1.0, instrument.FilterCutoff * cutoffMod * channelState.FilterCutoffMultiplier));
            var resonance = Math.Max(0.0, Math.Min(1.0, instrument.FilterResonance * channelState.FilterResonanceMultiplier));

            var key = (channelState.Program, instrument.GetHashCode());
            var state = filterStates.GetOrAdd(key, _ => new FilterState());

            double g = Math.Tan(Math.PI * cutoff / sampleRate);
            double k = 2.0 - 2.0 * resonance;
            double a1 = 1.0 / (1.0 + g * (g + k));
            double a2 = g * a1;
            double a3 = g * a2;

            double v0 = input;
            double v1 = a1 * state.z1 + a2 * (v0 - state.z1);
            double v2 = a2 * state.z1 + a3 * (v0 - state.z1) + state.z2;

            state.z1 = v1 + a2 * (v0 - v1);
            state.z2 = v2 + a3 * (v0 - v1);

            float output = 0;
            switch (instrument.FilterType)
            {
                case FilterType.LowPass:
                    output = (float)v2;
                    break;
                case FilterType.HighPass:
                    output = (float)(v0 - v1 * k - v2);
                    break;
                case FilterType.BandPass:
                    output = (float)v1;
                    break;
                case FilterType.Notch:
                    output = (float)(v0 - v1 * k);
                    break;
                case FilterType.Peak:
                    output = (float)(v2 - (v0 - v1 * k));
                    break;
                default:
                    output = input;
                    break;
            }

            return output;
        }

        public float ApplyLowPassFilter(float input, double cutoff, double resonance)
        {
            var alpha = Math.Min(cutoff / sampleRate, 0.49);
            return (float)(input * alpha);
        }

        public float ApplyHighPassFilter(float input, double cutoff, double resonance)
        {
            var alpha = Math.Min(cutoff / sampleRate, 0.49);
            return (float)(input * (1.0 - alpha));
        }

        public float ApplyBandPassFilter(float input, double cutoff, double resonance)
        {
            var alpha = Math.Min(cutoff / sampleRate, 0.49);
            return (float)(input * alpha * (1.0 - alpha));
        }

        public float ApplyNotchFilter(float input, double cutoff, double resonance)
        {
            var alpha = Math.Min(cutoff / sampleRate, 0.49);
            var band = 1.0 - alpha;
            return (float)(input * (1.0 - (1.0 - band * band)));
        }

        public float ApplyPeakFilter(float input, double cutoff, double resonance)
        {
            var alpha = Math.Min(cutoff / sampleRate, 0.49);
            var peak = 1.0 + resonance;
            return (float)(input * (1.0 - alpha + alpha * peak));
        }
    }
}
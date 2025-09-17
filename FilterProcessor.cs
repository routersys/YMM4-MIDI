using System;

namespace MIDI
{
    public class FilterProcessor
    {
        private readonly int sampleRate;
        private Random random = new Random();

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
                    return random.NextDouble() * 2 - 1;
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
            var cutoff = Math.Max(0, Math.Min(sampleRate / 2.0, instrument.FilterCutoff * cutoffMod * channelState.FilterCutoffMultiplier));
            var resonance = Math.Max(0.01, instrument.FilterResonance * channelState.FilterResonanceMultiplier);

            return instrument.FilterType switch
            {
                FilterType.LowPass => ApplyLowPassFilter(input, cutoff, resonance),
                FilterType.HighPass => ApplyHighPassFilter(input, cutoff, resonance),
                FilterType.BandPass => ApplyBandPassFilter(input, cutoff, resonance),
                FilterType.Notch => ApplyNotchFilter(input, cutoff, resonance),
                FilterType.Peak => ApplyPeakFilter(input, cutoff, resonance),
                _ => input
            };
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
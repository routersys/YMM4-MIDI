using System;

namespace MIDI
{
    public class FilterProcessor
    {
        private readonly int sampleRate;

        public FilterProcessor(int sampleRate)
        {
            this.sampleRate = sampleRate;
        }

        public float ApplyFilters(float input, InstrumentSettings instrument, double time, ChannelState channelState)
        {
            if (instrument.FilterType == FilterType.None) return input;

            var cutoffMod = 1.0 + instrument.FilterModulation * Math.Sin(2 * Math.PI * instrument.FilterModulationRate * time);
            var cutoff = instrument.FilterCutoff * cutoffMod * channelState.FilterCutoffMultiplier;
            var resonance = instrument.FilterResonance * channelState.FilterResonanceMultiplier;

            return instrument.FilterType switch
            {
                FilterType.LowPass => ApplyLowPassFilter(input, cutoff, resonance),
                FilterType.HighPass => ApplyHighPassFilter(input, cutoff, resonance),
                FilterType.BandPass => ApplyBandPassFilter(input, cutoff, resonance),
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
    }
}
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;

namespace MIDI.Voice.Engine.Worldline
{
    internal static class WorldlineUtils
    {
        private static float[] ReadSamples(string waveFile)
        {
            using (var reader = new AudioFileReader(waveFile))
            {
                ISampleProvider provider = reader;
                if (provider.WaveFormat.Channels > 1)
                {
                    provider = provider.ToMono();
                }

                var samples = new List<float>((int)(reader.Length / 4) + provider.WaveFormat.SampleRate);
                var buffer = new float[provider.WaveFormat.SampleRate];
                int read;
                while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
                {
                    samples.AddRange(buffer.Take(read));
                }
                return samples.ToArray();
            }
        }

        public static float[] GetSamples(string waveFile)
        {
            return ReadSamples(waveFile);
        }

        public static double[] GetSamplesAsDouble(string waveFile)
        {
            var floats = ReadSamples(waveFile);
            return Array.ConvertAll(floats, x => (double)x);
        }

        public static int GetF0Length(int sampleLength, int fs, double framePeriod)
        {
            return WorldlineApi.GetF0Length(sampleLength, fs, framePeriod);
        }

        public static void Dio(double[] samples, int fs, double framePeriod, double[] timeAxis, double[] f0)
        {
            WorldlineApi.Dio(samples, samples.Length, fs, framePeriod, timeAxis, f0);
        }

        public static double TempoTickToMs(double tick, double tempo, double timebase)
        {
            return tick * 60000.0 / (tempo * timebase);
        }

        public static double TempoMsToTick(double ms, double tempo, double timebase)
        {
            return ms * (tempo * timebase) / 60000.0;
        }

        public static double FreqToTone(double freq)
        {
            return 12 * Math.Log2(freq / 440.0) + 69;
        }

        public static double ToneToFreq(double tone)
        {
            return 440.0 * Math.Pow(2, (tone - 69) / 12.0);
        }
    }
}
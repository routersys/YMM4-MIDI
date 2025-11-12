using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MIDI.AudioEffect.SpatialAudioEffect.Services
{
    public class IrLoaderService
    {
        private readonly int requiredSampleRate;

        public IrLoaderService(int requiredSampleRate)
        {
            this.requiredSampleRate = requiredSampleRate;
        }

        public (float[]? Left, float[]? Right) LoadIrPair(string pathL, string pathR)
        {
            if (requiredSampleRate == 0 || string.IsNullOrEmpty(pathL))
            {
                return (null, null);
            }

            var (irL, irR) = LoadIrStereo(pathL);

            if (irL != null && irR != null)
            {
                return (irL, irR);
            }

            if (irL != null && !string.IsNullOrEmpty(pathR))
            {
                var (irR_mono, _) = LoadIrStereo(pathR);
                if (irR_mono != null)
                {
                    return (irL, irR_mono);
                }
            }

            return (irL, null);
        }

        private (float[]? Left, float[]? Right) LoadIrStereo(string filePath)
        {
            try
            {
                using (var reader = new WaveFileReader(filePath))
                {
                    if (reader.WaveFormat.SampleRate != requiredSampleRate)
                    {
                        return (null, null);
                    }

                    if (reader.WaveFormat.Channels == 1)
                    {
                        var bufferL = new List<float>();
                        float[] frame;
                        while ((frame = reader.ReadNextSampleFrame()) != null)
                        {
                            bufferL.Add(frame.Length > 0 ? frame[0] : 0);
                        }
                        return (bufferL.ToArray(), null);
                    }
                    else if (reader.WaveFormat.Channels == 2)
                    {
                        var bufferL = new List<float>();
                        var bufferR = new List<float>();
                        float[] frame;
                        while ((frame = reader.ReadNextSampleFrame()) != null)
                        {
                            bufferL.Add(frame.Length > 0 ? frame[0] : 0);
                            bufferR.Add(frame.Length > 1 ? frame[1] : 0);
                        }
                        return (bufferL.ToArray(), bufferR.ToArray());
                    }
                    else
                    {
                        return (null, null);
                    }
                }
            }
            catch
            {
                return (null, null);
            }
        }
    }
}
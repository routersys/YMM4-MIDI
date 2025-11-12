using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.IO;
using System.Threading.Tasks;
using MIDI.UI.ViewModels.MidiEditor.Modals;
using NAudio.Lame;

namespace MIDI.Core.Audio
{
    public static class AudioExporter
    {
        public static async Task ExportToWavAsync(string filePath, float[] audioBuffer, int sourceSampleRate, ExportProgressViewModel progressViewModel, int bitDepth, int targetSampleRate, int channels, string normalization, string dithering, double fade, bool clipping, string trim)
        {
            await Task.Run(async () =>
            {
                var sourceWaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sourceSampleRate, 2);
                ISampleProvider sampleProvider = new FloatArraySampleProvider(audioBuffer, sourceWaveFormat);

                if (channels == 1)
                {
                    sampleProvider = new StereoToMonoSampleProvider(sampleProvider);
                }

                if (sourceSampleRate != targetSampleRate)
                {
                    sampleProvider = new WdlResamplingSampleProvider(sampleProvider, targetSampleRate);
                }

                IWaveProvider waveProvider;
                switch (bitDepth)
                {
                    case 16:
                        waveProvider = new SampleToWaveProvider16(sampleProvider);
                        break;
                    case 24:
                        waveProvider = new SampleToWaveProvider24(sampleProvider);
                        break;
                    case 32:
                        waveProvider = new SampleToWaveProvider(sampleProvider);
                        break;
                    default:
                        throw new ArgumentException("Unsupported bit depth");
                }


                using (var writer = new WaveFileWriter(filePath, waveProvider.WaveFormat))
                {
                    long totalBytes = (long)(audioBuffer.Length / 2.0 * sourceSampleRate / targetSampleRate * waveProvider.WaveFormat.BlockAlign);
                    long bytesWritten = 0;
                    byte[] buffer = new byte[waveProvider.WaveFormat.AverageBytesPerSecond];
                    int bytesRead;

                    while ((bytesRead = waveProvider.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        await writer.WriteAsync(buffer, 0, bytesRead);
                        bytesWritten += bytesRead;
                        if (totalBytes > 0)
                        {
                            progressViewModel.Progress = (double)bytesWritten / totalBytes * 100;
                            progressViewModel.StatusMessage = $"書き込み中... {bytesWritten / 1024} KB / {totalBytes / 1024} KB";
                        }
                    }
                }
            });
        }

        public static async Task ExportToMp3Async(string filePath, float[] audioBuffer, int sampleRate, ExportProgressViewModel progressViewModel, int bitRate, string quality, string vbrMode, string title, string artist, string album, string channelMode, string lpf)
        {
            await Task.Run(async () =>
            {
                var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
                var sampleProvider = new FloatArraySampleProvider(audioBuffer, waveFormat);
                var waveProvider = new SampleToWaveProvider16(sampleProvider);

                LAMEPreset lamePreset;
                if (vbrMode == "VBR")
                {
                    lamePreset = quality switch
                    {
                        "Fast" => LAMEPreset.V5,
                        "Standard" => LAMEPreset.V2,
                        "High" => LAMEPreset.V0,
                        _ => LAMEPreset.V2,
                    };
                }
                else if (vbrMode == "ABR")
                {
                    lamePreset = (LAMEPreset)bitRate;
                }
                else
                {
                    lamePreset = (LAMEPreset)bitRate;
                }

                using (var writer = new LameMP3FileWriter(filePath, waveProvider.WaveFormat, lamePreset))
                {
                    long totalBytes = (long)(audioBuffer.Length / 2.0 * waveProvider.WaveFormat.BlockAlign);
                    long bytesWritten = 0;
                    byte[] buffer = new byte[waveProvider.WaveFormat.AverageBytesPerSecond];
                    int bytesRead;

                    while ((bytesRead = waveProvider.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        await writer.WriteAsync(buffer, 0, bytesRead);
                        bytesWritten += bytesRead;
                        if (totalBytes > 0)
                        {
                            progressViewModel.Progress = (double)bytesWritten / totalBytes * 100;
                            progressViewModel.StatusMessage = $"エンコード中... {bytesWritten / 1024} KB / {totalBytes / 1024} KB";
                        }
                    }
                }
            });
        }
    }

    internal class FloatArraySampleProvider : ISampleProvider
    {
        private readonly float[] source;
        private long position;

        public FloatArraySampleProvider(float[] source, WaveFormat waveFormat)
        {
            this.source = source;
            WaveFormat = waveFormat;
            position = 0;
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            long available = source.Length - position;
            int toCopy = (int)Math.Min(available, count);
            if (toCopy > 0)
            {
                Array.Copy(source, position, buffer, offset, toCopy);
                position += toCopy;
            }
            return toCopy;
        }
    }
}
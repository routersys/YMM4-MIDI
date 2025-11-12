using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;

namespace MIDI.Voice.Engine.Worldline
{
    internal interface IFrqFiles { }

    internal class OtoFrq : IFrqFiles
    {
        public byte[] Data { get; private set; } = Array.Empty<byte>();

        public OtoFrq(UtauOto oto, ConcurrentDictionary<string, IFrqFiles> frqFiles)
        {
            string frqFile = Path.ChangeExtension(oto.File, ".frq");
            if (frqFiles.TryGetValue(frqFile, out var frq) && frq is OtoFrq otoFrq)
            {
                this.Data = otoFrq.Data;
            }
            else
            {
                if (File.Exists(frqFile))
                {
                    try
                    {
                        this.Data = File.ReadAllBytes(frqFile);
                    }
                    catch { }
                }

                if (this.Data.Length == 0)
                {
                    try
                    {
                        var f0 = AnalyzeF0(oto.File);
                        this.Data = WriteFrq(f0, 5.0);
                        File.WriteAllBytes(frqFile, this.Data);
                    }
                    catch { }
                }
            }
        }

        private static byte[] WriteFrq(double[] f0, double framePeriod)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(Encoding.ASCII.GetBytes("FREQ0003"));
                writer.Write(BitConverter.GetBytes((int)(framePeriod * 1000)));
                writer.Write(BitConverter.GetBytes(f0.Length));
                writer.Write(BitConverter.GetBytes((double)440.0));
                writer.Write(new byte[12]);
                foreach (double f in f0)
                {
                    writer.Write(BitConverter.GetBytes((float)f));
                    writer.Write(BitConverter.GetBytes(0.0f));
                }
                return stream.ToArray();
            }
        }

        private static double[] AnalyzeF0(string wavFile)
        {
            var samples = WorldlineUtils.GetSamplesAsDouble(wavFile);

            int fs = 44100;
            using (var reader = new NAudio.Wave.AudioFileReader(wavFile))
            {
                fs = reader.WaveFormat.SampleRate;
            }

            double framePeriod = 5.0;
            int f0Length = WorldlineUtils.GetF0Length(samples.Length, fs, framePeriod);
            double[] f0 = new double[f0Length];
            double[] timeAxis = new double[f0Length];

            WorldlineApi.Dio(samples, samples.Length, fs, framePeriod, timeAxis, f0);

            return f0.Select(f => f < 0 ? 0 : f).ToArray();
        }
    }
}
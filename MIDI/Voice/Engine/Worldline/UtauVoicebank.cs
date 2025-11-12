using MIDI.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace MIDI.Voice.Engine.Worldline
{
    internal class UtauVoicebank
    {
        public string BasePath { get; }
        public Dictionary<string, List<UtauOto>> Otos { get; } = new Dictionary<string, List<UtauOto>>();
        public ConcurrentDictionary<string, IFrqFiles> Frqs { get; } = new ConcurrentDictionary<string, IFrqFiles>();
        public ConcurrentDictionary<string, double[]> Samples { get; } = new ConcurrentDictionary<string, double[]>();

        public UtauVoicebank(string basePath)
        {
            BasePath = basePath;
            LoadOtos();
        }

        private void LoadOtos()
        {
            var otoFiles = Directory.GetFiles(BasePath, "oto.ini", SearchOption.AllDirectories);
            foreach (var otoFile in otoFiles)
            {
                try
                {
                    var dir = Path.GetDirectoryName(otoFile) ?? BasePath;
                    var encoding = DetectEncoding(otoFile);
                    var lines = File.ReadAllLines(otoFile, encoding);

                    foreach (var line in lines)
                    {
                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("[")) continue;
                        try
                        {
                            var wavFile = line.Split('=')[0];
                            var wavPath = Path.Combine(dir, wavFile);
                            if (!File.Exists(wavPath))
                            {
                                wavPath = Path.ChangeExtension(wavPath, ".wav");
                                if (!File.Exists(wavPath))
                                {
                                    continue;
                                }
                            }

                            var oto = new UtauOto(line, wavPath);
                            if (!Otos.ContainsKey(oto.Alias))
                            {
                                Otos[oto.Alias] = new List<UtauOto>();
                            }
                            Otos[oto.Alias].Add(oto);
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"Failed to parse oto line: {line} in {otoFile}. {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to read oto.ini: {otoFile}", ex);
                }
            }
        }

        public UtauOto? GetOto(string alias, int noteNum)
        {
            if (Otos.TryGetValue(alias, out var otos))
            {
                return otos.FirstOrDefault();
            }
            return null;
        }

        public double[] GetSamplesAsDouble(string filePath)
        {
            return Samples.GetOrAdd(filePath, (path) => WorldlineUtils.GetSamplesAsDouble(path));
        }

        public OtoFrq GetFrq(UtauOto oto)
        {
            return new OtoFrq(oto, Frqs);
        }

        private Encoding DetectEncoding(string filename)
        {
            try
            {
                Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
                return Encoding.GetEncoding("Shift_JIS");
            }
            catch (Exception ex)
            {
                Logger.Warn($"Failed to get Shift_JIS encoding, falling back to Default. Error: {ex.Message}");
                return Encoding.Default;
            }
        }
    }
}
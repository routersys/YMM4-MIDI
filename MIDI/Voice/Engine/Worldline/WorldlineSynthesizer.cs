using MIDI.Utils;
using MIDI.Voice.SUSL.Core;
using MIDI.Voice.SUSL.Parsing.AST;
using System;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace MIDI.Voice.Engine.Worldline
{
    internal class WorldlineSynthesizer
    {
        private readonly UtauVoicebank _voicebank;
        private const double FrameMs = 10.0;

        private static Process? _hostProcess;
        private static readonly object _ipcLock = new object();
        private static bool _isHostStarting = false;

        public WorldlineSynthesizer(UtauVoicebank voicebank)
        {
            _voicebank = voicebank;
            EnsureHostProcessRunning();
        }

        private static void EnsureHostProcessRunning()
        {
            lock (_ipcLock)
            {
                if (_isHostStarting) return;

                if (_hostProcess != null && !_hostProcess.HasExited)
                {
                    return;
                }

                if (_hostProcess != null)
                {
                    _hostProcess.Dispose();
                    _hostProcess = null;
                }

                _isHostStarting = true;
                try
                {
                    string assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
                    string hostExePath = Path.Combine(assemblyLocation, "WorldlineHost.exe");

                    if (!File.Exists(hostExePath))
                    {
                        hostExePath = Path.Combine(assemblyLocation, "WorldlineHost", "WorldlineHost.exe");
                        if (!File.Exists(hostExePath))
                        {
                            Logger.Error($"WorldlineHost.exe not found.", null);
                            return;
                        }
                    }

                    ProcessStartInfo startInfo = new ProcessStartInfo(hostExePath)
                    {
                        UseShellExecute = false,
                        RedirectStandardInput = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8,
                        StandardInputEncoding = Encoding.UTF8,
                        StandardErrorEncoding = Encoding.UTF8,
                    };

                    _hostProcess = new Process { StartInfo = startInfo };
                    _hostProcess.EnableRaisingEvents = true;

                    _hostProcess.ErrorDataReceived += (sender, args) =>
                    {
                        if (!string.IsNullOrEmpty(args.Data))
                        {
                            Logger.Error($"[WorldlineHost.stderr] {args.Data}", null);
                        }
                    };

                    _hostProcess.Exited += (sender, args) =>
                    {
                        lock (_ipcLock)
                        {
                            Logger.Error("WorldlineHost.exe process has exited.", null);
                            _hostProcess?.Dispose();
                            _hostProcess = null;
                        }
                    };

                    _hostProcess.Start();
                    _hostProcess.BeginErrorReadLine();
                    Logger.Info("WorldlineHost.exe started.");
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to start WorldlineHost.exe.", ex);
                    _hostProcess?.Dispose();
                    _hostProcess = null;
                }
                finally
                {
                    _isHostStarting = false;
                }
            }
        }

        private string SendRequestToHost(string jsonRequest)
        {
            lock (_ipcLock)
            {
                if (_hostProcess == null || _hostProcess.HasExited)
                {
                    Logger.Warn("Host process is not running. Attempting to restart.");
                    EnsureHostProcessRunning();
                    if (_hostProcess == null)
                    {
                        throw new InvalidOperationException("WorldlineHost.exe process is not available and could not be started.");
                    }
                }

                try
                {
                    _hostProcess.StandardInput.WriteLine(jsonRequest);
                    string? responseLine = _hostProcess.StandardOutput.ReadLine();

                    if (responseLine == null)
                    {
                        throw new InvalidOperationException("Received null response from WorldlineHost.exe, it may have crashed.");
                    }
                    return responseLine;
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to communicate with WorldlineHost.exe. It may have crashed.", ex);
                    _hostProcess?.Kill(true);
                    _hostProcess?.Dispose();
                    _hostProcess = null;
                    throw new InvalidOperationException("IPC error with WorldlineHost.exe.", ex);
                }
            }
        }

        public float[] Synthesize(SuslProgram program)
        {
            var (items, context) = SuslEventConverter.Convert(program, _voicebank);
            if (!items.Any())
            {
                return Array.Empty<float>();
            }

            try
            {
                double totalDurationMs = items.Max(i => i.PositionMs + i.DurationMs);
                if (double.IsNaN(totalDurationMs) || double.IsInfinity(totalDurationMs))
                {
                    totalDurationMs = 1.0;
                }

                int frames = (int)Math.Ceiling(totalDurationMs / FrameMs);
                if (frames <= 0) frames = 1;

                double[] f0 = new double[frames];
                double[] gender = new double[frames];
                double[] tension = new double[frames];
                double[] breathiness = new double[frames];
                double[] voicing = new double[frames];

                Array.Fill(gender, 0.5);
                Array.Fill(tension, 0.5);
                Array.Fill(breathiness, 0.5);
                Array.Fill(voicing, 1.0);

                var ipcItems = new List<ItemData>();

                foreach (var item in items)
                {
                    double posMs = item.PositionMs;
                    double skipMs = item.SkipOverMs;
                    double lengthMs = item.DurationMs;
                    double fadeInMs = item.FadeInMs;
                    double fadeOutMs = item.FadeOutMs;

                    if (lengthMs < 1.0)
                    {
                        lengthMs = 1.0;
                        fadeInMs = 0.5;
                        fadeOutMs = 0.5;
                    }
                    else
                    {
                        if (fadeInMs < 0) fadeInMs = 0;
                        if (fadeOutMs < 0) fadeOutMs = 0;
                        if (fadeInMs + fadeOutMs > lengthMs)
                        {
                            double totalFade = fadeInMs + fadeOutMs;
                            fadeInMs = (fadeInMs / totalFade) * lengthMs;
                            fadeOutMs = (fadeOutMs / totalFade) * lengthMs;
                        }
                    }

                    var ipcItem = new ItemData
                    {
                        Sample = item.Sample,
                        FrqData = item.FrqData,
                        Pitches = item.Pitches,
                        SampleFs = item.Request.sample_fs,
                        Tone = item.Request.tone,
                        ConVel = item.Request.con_vel,
                        Offset = item.Request.offset,
                        RequiredLength = item.Request.required_length,
                        Consonant = item.Request.consonant,
                        CutOff = item.Request.cut_off,
                        Volume = item.Request.volume,
                        Modulation = item.Request.modulation,
                        Tempo = item.Request.tempo,
                        FlagG = item.Request.flag_g,
                        FlagO = item.Request.flag_O,
                        FlagP = item.Request.flag_P,
                        FlagMt = item.Request.flag_Mt,
                        FlagMb = item.Request.flag_Mb,
                        FlagMv = item.Request.flag_Mv,
                        PosMs = posMs,
                        SkipMs = skipMs,
                        LengthMs = lengthMs,
                        FadeInMs = fadeInMs,
                        FadeOutMs = fadeOutMs
                    };
                    ipcItems.Add(ipcItem);

                    var (itemF0, _, _, _, _) =
                        context.GetCurves(item.Note.AbsoluteTick, context.GetTickDuration(item.Note.Length), item.Request.tone);

                    int startFrame = (int)Math.Floor(posMs / FrameMs);
                    for (int i = 0; i < itemF0.Length; i++)
                    {
                        int frameIndex = startFrame + i;
                        if (frameIndex >= 0 && frameIndex < frames)
                        {
                            f0[frameIndex] = WorldlineUtils.ToneToFreq(itemF0[i]);
                        }
                    }
                }

                var requestData = new SynthRequestData
                {
                    Items = ipcItems,
                    F0 = f0,
                    Gender = gender,
                    Tension = tension,
                    Breathiness = breathiness,
                    Voicing = voicing
                };

                string jsonRequest = JsonSerializer.Serialize(requestData, IpcJsonContext.Default.SynthRequestData);
                string jsonResponse = SendRequestToHost(jsonRequest);
                var response = JsonSerializer.Deserialize<SynthResponse>(jsonResponse, IpcJsonContext.Default.SynthResponse);

                if (response == null || !response.Success)
                {
                    throw new Exception($"WorldlineHost synthesis failed: {response?.ErrorMessage ?? "Unknown error"}");
                }

                return response.AudioData ?? Array.Empty<float>();
            }
            catch (Exception ex)
            {
                Logger.Error("Worldline synthesis failed.", ex);
                return Array.Empty<float>();
            }
        }
    }

    public class SynthRequestData
    {
        public List<ItemData> Items { get; set; } = new();
        public double[] F0 { get; set; } = Array.Empty<double>();
        public double[] Gender { get; set; } = Array.Empty<double>();
        public double[] Tension { get; set; } = Array.Empty<double>();
        public double[] Breathiness { get; set; } = Array.Empty<double>();
        public double[] Voicing { get; set; } = Array.Empty<double>();
    }

    public class ItemData
    {
        public double[] Sample { get; set; } = Array.Empty<double>();
        public byte[]? FrqData { get; set; }
        public double[] Pitches { get; set; } = Array.Empty<double>();
        public int SampleFs { get; set; }
        public int Tone { get; set; }
        public double ConVel { get; set; }
        public double Offset { get; set; }
        public double RequiredLength { get; set; }
        public double Consonant { get; set; }
        public double CutOff { get; set; }
        public double Volume { get; set; }
        public double Modulation { get; set; }
        public double Tempo { get; set; }
        public int FlagG { get; set; }
        public int FlagO { get; set; }
        public int FlagP { get; set; }
        public int FlagMt { get; set; }
        public int FlagMb { get; set; }
        public int FlagMv { get; set; }
        public double PosMs { get; set; }
        public double SkipMs { get; set; }
        public double LengthMs { get; set; }
        public double FadeInMs { get; set; }
        public double FadeOutMs { get; set; }
    }

    public class SynthResponse
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public float[]? AudioData { get; set; }
    }

    [JsonSerializable(typeof(SynthRequestData))]
    [JsonSerializable(typeof(SynthResponse))]
    internal partial class IpcJsonContext : JsonSerializerContext { }
}
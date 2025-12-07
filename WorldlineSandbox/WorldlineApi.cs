using System.Runtime.InteropServices;

namespace WorldlineHost
{
    public class SynthRequestError : Exception
    {
        public SynthRequestError(string message) : base(message) { }
    }
    public class CutOffExceedDurationError : SynthRequestError
    {
        public CutOffExceedDurationError(string message) : base(message) { }
    }
    public class CutOffBeforeOffsetError : SynthRequestError
    {
        public CutOffBeforeOffsetError(string message) : base(message) { }
    }

    internal static class WorldlineApi
    {
        private const string WorldlineDll = "worldline";

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int GetF0Length(int x_length, int fs, double frame_period);

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern void Dio(double[] x, int x_length, int fs, double frame_period,
            double[] time_axis, double[] f0);

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.Cdecl)]
        static extern int F0(
            float[] samples, int length, int fs, double framePeriod, int method, ref IntPtr f0);

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.Cdecl)]
        static extern int DecodeMgc(
            int f0Length, double[] mgc, int mgcSize,
            int fftSize, int fs, ref IntPtr spectrogram);

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.Cdecl)]
        static extern int DecodeBap(
            int f0Length, double[] bap,
            int fftSize, int fs, ref IntPtr aperiodicity);

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.Cdecl)]
        static extern int WorldSynthesis(
            double[] f0, int f0Length,
            double[,] mgcOrSp, bool isMgc, int mgcSize,
            double[,] bapOrAp, bool isBap, int fftSize,
            double framePeriod, int fs, ref IntPtr y,
            double[] gender, double[] tension,
            double[] breathiness, double[] voicing);

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.Cdecl)]
        static extern int WorldSynthesis(
            double[] f0, int f0Length,
            double[] mgcOrSp, bool isMgc, int mgcSize,
            double[] bapOrAp, bool isBap, int fftSize,
            double framePeriod, int fs, ref IntPtr y,
            double[] gender, double[] tension,
            double[] breathiness, double[] voicing);

        [StructLayout(LayoutKind.Sequential)]
        public struct SynthRequest
        {
            public int sample_fs;
            public int sample_length;
            public IntPtr sample;
            public int frq_length;
            public IntPtr frq;
            public int tone;
            public double con_vel;
            public double offset;
            public double required_length;
            public double consonant;
            public double cut_off;
            public double volume;
            public double modulation;
            public double tempo;
            public int pitch_bend_length;
            public IntPtr pitch_bend;
            public int flag_g;
            public int flag_O;
            public int flag_P;
            public int flag_Mt;
            public int flag_Mb;
            public int flag_Mv;
        };

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.Cdecl)]
        public static extern int Resample(IntPtr request, ref IntPtr y);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        public delegate void LogCallback(string log);

        private static void WorldlineLog(string log)
        {
            Logger.Info($"[worldline.dll] {log}");
        }

        private static readonly LogCallback _staticLogCallback = WorldlineLog;

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.Cdecl)]
        static extern IntPtr PhraseSynthNew();

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.Cdecl)]
        static extern void PhraseSynthDelete(IntPtr phrase_synth);

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.StdCall)]
        static extern void PhraseSynthAddRequest(
            IntPtr phrase_synth, ref SynthRequest request,
            double posMs, double skipMs, double lengthMs,
            double fadeInMs, double fadeOutMs, LogCallback logCallback);

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.StdCall)]
        static extern void PhraseSynthSetCurves(
            IntPtr phraseSynth, double[] f0,
            double[] gender, double[] tension,
            double[] breathiness, double[] voicing,
            int length, LogCallback logCallback);

        [DllImport(WorldlineDll, CallingConvention = CallingConvention.StdCall)]
        static extern int PhraseSynthSynth(
            IntPtr phrase_synth,
            ref IntPtr y, LogCallback logCallback);

        public class PhraseSynth : IDisposable
        {
            private IntPtr ptr;
            private bool disposedValue;
            private readonly GCHandle[] _pinnedHandles;

            public PhraseSynth(int itemCount)
            {
                ptr = PhraseSynthNew();
                _pinnedHandles = new GCHandle[itemCount * 3];
            }

            public void AddRequest(ref SynthRequest request,
                double posMs, double skipMs, double lengthMs,
                double fadeInMs, double fadeOutMs,
                int itemIndex, GCHandle sampleHandle, GCHandle? frqHandle, GCHandle? pitchHandle)
            {
                _pinnedHandles[itemIndex * 3] = sampleHandle;
                request.sample = sampleHandle.AddrOfPinnedObject();

                if (frqHandle.HasValue)
                {
                    _pinnedHandles[itemIndex * 3 + 1] = frqHandle.Value;
                    request.frq = frqHandle.Value.AddrOfPinnedObject();
                }
                else
                {
                    request.frq = IntPtr.Zero;
                }

                if (pitchHandle.HasValue)
                {
                    _pinnedHandles[itemIndex * 3 + 2] = pitchHandle.Value;
                    request.pitch_bend = pitchHandle.Value.AddrOfPinnedObject();
                }
                else
                {
                    request.pitch_bend = IntPtr.Zero;
                }

                try
                {
                    PhraseSynthAddRequest(
                        ptr, ref request,
                        posMs, skipMs, lengthMs,
                        fadeInMs, fadeOutMs, _staticLogCallback);
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to add Worldline request.", ex);
                    throw;
                }
            }

            public void SetCurves(double[] f0, double[] gender, double[] tension, double[] breathiness, double[] voicing)
            {
                PhraseSynthSetCurves(
                    ptr, f0,
                    gender, tension, breathiness, voicing,
                    f0.Length, _staticLogCallback);
            }

            public float[] Synth()
            {
                IntPtr buffer = IntPtr.Zero;
                int size = PhraseSynthSynth(ptr, ref buffer, _staticLogCallback);
                if (size == 0 || buffer == IntPtr.Zero)
                {
                    return Array.Empty<float>();
                }
                var data = new float[size];
                Marshal.Copy(buffer, data, 0, size);
                Marshal.FreeCoTaskMem(buffer);
                return data;
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    PhraseSynthDelete(ptr);
                    foreach (var handle in _pinnedHandles)
                    {
                        if (handle.IsAllocated)
                        {
                            handle.Free();
                        }
                    }
                    disposedValue = true;
                }
            }

            ~PhraseSynth()
            {
                Dispose(disposing: false);
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }
    }
}
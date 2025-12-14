using System;
using System.Runtime.InteropServices;

namespace MIDI.Utils.Telemetry
{
    public static class NativeKeyProvider
    {
        private const string DllName = "telemetry_key_provider.dll";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr get_secret_key();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void free_secret_key(IntPtr ptr);

        public static string GetSecretKey()
        {
            IntPtr ptr = IntPtr.Zero;
            try
            {
                ptr = get_secret_key();
                if (ptr == IntPtr.Zero)
                {
                    return string.Empty;
                }

                string key = Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
                return key;
            }
            finally
            {
                if (ptr != IntPtr.Zero)
                {
                    free_secret_key(ptr);
                }
            }
        }
    }
}
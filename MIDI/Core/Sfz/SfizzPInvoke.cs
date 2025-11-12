using System;
using System.Runtime.InteropServices;
using System.Security;

namespace MIDI
{
    [SuppressUnmanagedCodeSecurity]
    internal static class SfizzPInvoke
    {
        private const string DllName = "sfizz";

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr sfizz_new();

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sfizz_free(IntPtr synth);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern bool sfizz_load_sfz_file(IntPtr synth, string path);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sfizz_set_sample_rate(IntPtr synth, float sample_rate);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sfizz_set_samples_per_block(IntPtr synth, int samples_per_block);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int sfizz_get_samples_per_block(IntPtr synth);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sfizz_note_on(IntPtr synth, int delay, int note_number, int velocity);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sfizz_note_off(IntPtr synth, int delay, int note_number, int velocity);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sfizz_cc(IntPtr synth, int delay, int cc_number, int cc_value);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sfizz_pitch_wheel(IntPtr synth, int delay, int pitch);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sfizz_all_sounds_off(IntPtr synth);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void sfizz_render_block(IntPtr synth, IntPtr stero_out_buffer, int num_channels, int num_frames);
    }
}
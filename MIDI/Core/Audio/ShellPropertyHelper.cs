using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace MIDI.Core.Audio
{
    internal static class ShellPropertyHelper
    {
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SHGetPropertyStoreFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            GETPROPERTYSTOREFLAGS flags,
            [In] ref Guid riid,
            out IPropertyStore ppv);

        [ComImport]
        [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            void GetCount(out uint cProps);
            void GetAt(uint iProp, out PROPERTYKEY pkey);
            void GetValue([In] ref PROPERTYKEY key, out PROPVARIANT pv);
            void SetValue([In] ref PROPERTYKEY key, [In] ref PROPVARIANT propvar);
            void Commit();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 4)]
        private struct PROPERTYKEY
        {
            public Guid fmtid;
            public uint pid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROPVARIANT
        {
            public ushort vt;
            public ushort wReserved1;
            public ushort wReserved2;
            public ushort wReserved3;
            public IntPtr pszVal;
            public ulong uhVal;
        }

        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PROPVARIANT pvar);

        private enum GETPROPERTYSTOREFLAGS
        {
            GPS_DEFAULT = 0,
            GPS_HANDLERPROPERTIESONLY = 1,
            GPS_READWRITE = 2,
            GPS_TEMPORARY = 4,
            GPS_FASTPROPERTIESONLY = 8,
            GPS_OPENSLOWITEM = 16,
            GPS_DELAYCREATION = 32,
            GPS_BESTEFFORT = 64,
            GPS_NO_OPLOCK = 128,
            GPS_PREFERQUERYPROPERTIES = 256,
            GPS_EXTRINSICPROPERTIES = 512,
            GPS_EXTRINSICPROPERTIESONLY = 1024,
            GPS_MASK_VALID = 4095
        }

        private static Guid IPropertyStoreGUID = new Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
        private static PROPERTYKEY PKEY_Title = new PROPERTYKEY { fmtid = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), pid = 2 };
        private static PROPERTYKEY PKEY_Author = new PROPERTYKEY { fmtid = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), pid = 4 };
        private static PROPERTYKEY PKEY_Album = new PROPERTYKEY { fmtid = new Guid("56A3372E-CE9C-11D2-9F0E-006097C686F6"), pid = 100 };
        private static PROPERTYKEY PKEY_Keywords = new PROPERTYKEY { fmtid = new Guid("F29F85E0-4FF9-1068-AB91-08002B27B3D9"), pid = 5 };

        public static void WriteMetadata(string filePath, string title, string artist, string album, string tags)
        {
            int hr = SHGetPropertyStoreFromParsingName(filePath, IntPtr.Zero, GETPROPERTYSTOREFLAGS.GPS_READWRITE, ref IPropertyStoreGUID, out var store);
            if (hr != 0) return;

            try
            {
                if (!string.IsNullOrEmpty(title))
                {
                    SetValue(store, PKEY_Title, title);
                }
                if (!string.IsNullOrEmpty(artist))
                {
                    SetValue(store, PKEY_Author, artist);
                }
                if (!string.IsNullOrEmpty(album))
                {
                    SetValue(store, PKEY_Album, album);
                }
                if (!string.IsNullOrEmpty(tags))
                {
                    SetValue(store, PKEY_Keywords, tags);
                }
                store.Commit();
            }
            finally
            {
                Marshal.ReleaseComObject(store);
            }
        }

        private static void SetValue(IPropertyStore store, PROPERTYKEY key, string value)
        {
            PROPVARIANT pv = new PROPVARIANT();
            try
            {
                pv.vt = (ushort)VarEnum.VT_LPWSTR;
                pv.pszVal = Marshal.StringToCoTaskMemUni(value);
                store.SetValue(ref key, ref pv);
            }
            finally
            {
                PropVariantClear(ref pv);
            }
        }
    }
}
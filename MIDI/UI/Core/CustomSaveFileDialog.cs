using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace MIDI.UI.Core
{
    [Flags]
    public enum FOS : uint
    {
        FOS_OVERWRITEPROMPT = 0x00000002,
        FOS_STRICTFILETYPES = 0x00000004,
        FOS_NOCHANGEDIR = 0x00000008,
        FOS_PICKFOLDERS = 0x00000020,
        FOS_FORCEFILESYSTEM = 0x00000040,
        FOS_ALLNONSTORAGEITEMS = 0x00000080,
        FOS_NOVALIDATE = 0x00000100,
        FOS_ALLOWMULTISELECT = 0x00000200,
        FOS_PATHMUSTEXIST = 0x00000800,
        FOS_FILEMUSTEXIST = 0x00001000,
        FOS_CREATEPROMPT = 0x00002000,
        FOS_SHAREAWARE = 0x00004000,
        FOS_NOREADONLYRETURN = 0x00008000,
        FOS_NOTESTFILECREATE = 0x00010000,
        FOS_HIDEMRUPLACES = 0x00020000,
        FOS_HIDEPINNEDPLACES = 0x00040000,
        FOS_NODEREFERENCELINKS = 0x00100000,
        FOS_DONTADDTORECENT = 0x02000000,
        FOS_FORCESHOWHIDDEN = 0x10000000,
        FOS_DEFAULTNOMINIMODE = 0x20000000
    }

    public enum FDE_SHAREVIOLATION_RESPONSE { FDESVR_DEFAULT = 0, FDESVR_ACCEPT = 1, FDESVR_REFUSE = 2 }
    public enum FDE_OVERWRITE_RESPONSE { FDEOR_DEFAULT = 0, FDEOR_ACCEPT = 1, FDEOR_REFUSE = 2 }
    public enum SIGDN : uint { SIGDN_FILESYSPATH = 0x80058000 }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct COMDLG_FILTERSPEC
    {
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pszName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string pszSpec;
    }

    [ComImport]
    [Guid("b4db1657-70d7-485e-8e3e-6fcb5a5c1802")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IModalWindow
    {
        [PreserveSig]
        int Show([In] IntPtr parent);
    }

    [ComImport]
    [Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IShellItem
    {
        void BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
        void GetParent(out IShellItem ppsi);
        void GetDisplayName([In] SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
        void GetAttributes([In] uint sfgaoMask, out uint psfgaoAttribs);
        void Compare([In] IShellItem psi, [In] uint hint, out int piOrder);
    }

    [ComImport]
    [Guid("42f85136-db7e-439c-85f1-e4075d135fc8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileDialog : IModalWindow
    {
        [PreserveSig]
        new int Show([In] IntPtr parent);

        [PreserveSig]
        int SetFileTypes([In] uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);

        [PreserveSig]
        int SetFileTypeIndex([In] uint iFileType);

        [PreserveSig]
        int GetFileTypeIndex(out uint piFileType);

        [PreserveSig]
        int Advise([In, MarshalAs(UnmanagedType.Interface)] IFileDialogEvents pfde, out uint pdwCookie);

        [PreserveSig]
        int Unadvise([In] uint dwCookie);

        [PreserveSig]
        int SetOptions([In] FOS fos);

        [PreserveSig]
        int GetOptions(out FOS pfos);

        [PreserveSig]
        int SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

        [PreserveSig]
        int SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

        [PreserveSig]
        int GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [PreserveSig]
        int GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [PreserveSig]
        int SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);

        [PreserveSig]
        int GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

        [PreserveSig]
        int SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

        [PreserveSig]
        int SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

        [PreserveSig]
        int SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        [PreserveSig]
        int GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [PreserveSig]
        int AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, int alignment);

        [PreserveSig]
        int SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

        [PreserveSig]
        int Close([MarshalAs(UnmanagedType.Error)] int hr);

        [PreserveSig]
        int SetClientGuid([In] ref Guid guid);

        [PreserveSig]
        int ClearClientData();

        [PreserveSig]
        int SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);
    }

    [ComImport]
    [Guid("84bccd23-5fde-4cdb-aea4-af64b83d78ab")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileSaveDialog : IFileDialog
    {
        [PreserveSig]
        new int Show([In] IntPtr parent);

        [PreserveSig]
        new int SetFileTypes([In] uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);

        [PreserveSig]
        new int SetFileTypeIndex([In] uint iFileType);

        [PreserveSig]
        new int GetFileTypeIndex(out uint piFileType);

        [PreserveSig]
        new int Advise([In, MarshalAs(UnmanagedType.Interface)] IFileDialogEvents pfde, out uint pdwCookie);

        [PreserveSig]
        new int Unadvise([In] uint dwCookie);

        [PreserveSig]
        new int SetOptions([In] FOS fos);

        [PreserveSig]
        new int GetOptions(out FOS pfos);

        [PreserveSig]
        new int SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

        [PreserveSig]
        new int SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

        [PreserveSig]
        new int GetFolder([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [PreserveSig]
        new int GetCurrentSelection([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [PreserveSig]
        new int SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);

        [PreserveSig]
        new int GetFileName([MarshalAs(UnmanagedType.LPWStr)] out string pszName);

        [PreserveSig]
        new int SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);

        [PreserveSig]
        new int SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

        [PreserveSig]
        new int SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        [PreserveSig]
        new int GetResult([MarshalAs(UnmanagedType.Interface)] out IShellItem ppsi);

        [PreserveSig]
        new int AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, int alignment);

        [PreserveSig]
        new int SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);

        [PreserveSig]
        new int Close([MarshalAs(UnmanagedType.Error)] int hr);

        [PreserveSig]
        new int SetClientGuid([In] ref Guid guid);

        [PreserveSig]
        new int ClearClientData();

        [PreserveSig]
        new int SetFilter([MarshalAs(UnmanagedType.Interface)] IntPtr pFilter);

        [PreserveSig]
        int SetSaveAsItem([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);

        [PreserveSig]
        int SetProperties([In] IntPtr pStore);

        [PreserveSig]
        int SetCollectedProperties([In] IntPtr pList, [In] bool fAppendDefault);

        [PreserveSig]
        int GetProperties(out IntPtr ppStore);

        [PreserveSig]
        int ApplyProperties([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] IntPtr pStore, [In] IntPtr hwnd, [In] IntPtr pSink);
    }

    [ComImport]
    [Guid("973510db-7d7f-452b-8975-74a85828d354")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileDialogEvents
    {
        [PreserveSig]
        int OnFileOk([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

        [PreserveSig]
        int OnFolderChanging([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psiFolder);

        [PreserveSig]
        int OnFolderChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

        [PreserveSig]
        int OnSelectionChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

        [PreserveSig]
        int OnShareViolation([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, out FDE_SHAREVIOLATION_RESPONSE pResponse);

        [PreserveSig]
        int OnTypeChange([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd);

        [PreserveSig]
        int OnOverwrite([In, MarshalAs(UnmanagedType.Interface)] IFileDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, out FDE_OVERWRITE_RESPONSE pResponse);
    }

    [ComImport]
    [Guid("e6fdd21a-163f-4975-9c8c-a69f1ba37034")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IFileDialogCustomize
    {
        [PreserveSig]
        int EnableOpenDropDown([In] uint dwIDCtl);

        [PreserveSig]
        int AddMenu([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        [PreserveSig]
        int AddPushButton([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        [PreserveSig]
        int AddComboBox([In] uint dwIDCtl);

        [PreserveSig]
        int AddRadioButtonList([In] uint dwIDCtl);

        [PreserveSig]
        int AddCheckButton([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel, [In] bool bChecked);

        [PreserveSig]
        int AddEditBox([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

        [PreserveSig]
        int AddSeparator([In] uint dwIDCtl);

        [PreserveSig]
        int AddText([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

        [PreserveSig]
        int SetControlLabel([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        [PreserveSig]
        int GetControlState([In] uint dwIDCtl, out uint pdwState);

        [PreserveSig]
        int SetControlState([In] uint dwIDCtl, [In] uint dwState);

        [PreserveSig]
        int GetEditBoxText([In] uint dwIDCtl, [MarshalAs(UnmanagedType.LPWStr)] out string ppszText);

        [PreserveSig]
        int SetEditBoxText([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszText);

        [PreserveSig]
        int GetCheckButtonState([In] uint dwIDCtl, out bool pbChecked);

        [PreserveSig]
        int SetCheckButtonState([In] uint dwIDCtl, [In] bool bChecked);

        [PreserveSig]
        int AddControlItem([In] uint dwIDCtl, [In] uint dwIDItem, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        [PreserveSig]
        int RemoveControlItem([In] uint dwIDCtl, [In] uint dwIDItem);

        [PreserveSig]
        int RemoveAllControlItems([In] uint dwIDCtl);

        [PreserveSig]
        int GetControlItemState([In] uint dwIDCtl, [In] uint dwIDItem, out uint pdwState);

        [PreserveSig]
        int SetControlItemState([In] uint dwIDCtl, [In] uint dwIDItem, [In] uint dwState);

        [PreserveSig]
        int GetSelectedControlItem([In] uint dwIDCtl, out uint pdwIDItem);

        [PreserveSig]
        int SetSelectedControlItem([In] uint dwIDCtl, [In] uint dwIDItem);

        [PreserveSig]
        int StartVisualGroup([In] uint dwIDCtl, [In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);

        [PreserveSig]
        int EndVisualGroup();

        [PreserveSig]
        int MakeProminent([In] uint dwIDCtl);
    }

    [ComImport]
    [Guid("C0B4E2F3-BA21-4773-8DBA-335EC946EB8B")]
    [ClassInterface(ClassInterfaceType.None)]
    internal class FileSaveDialogRCW { }

    public class CustomSaveFileDialog : IFileDialogEvents
    {
        private const uint CONTROL_ID_BITRATE_LABEL = 101;
        private const uint CONTROL_ID_BITRATE_COMBO = 102;
        private const uint CONTROL_ID_WAV_GROUP = 103;
        private const uint CONTROL_ID_BITDEPTH_LABEL = 104;
        private const uint CONTROL_ID_BITDEPTH_COMBO = 105;
        private const uint CONTROL_ID_SAMPLERATE_LABEL = 106;
        private const uint CONTROL_ID_SAMPLERATE_COMBO = 107;
        private const uint CONTROL_ID_CHANNELS_LABEL = 108;
        private const uint CONTROL_ID_CHANNELS_COMBO = 109;
        private const uint CONTROL_ID_MP3_GROUP = 110;
        private const uint CONTROL_ID_ENCODE_QUALITY_LABEL = 111;
        private const uint CONTROL_ID_ENCODE_QUALITY_COMBO = 112;
        private const uint CONTROL_ID_VBR_MODE_LABEL = 113;
        private const uint CONTROL_ID_VBR_MODE_COMBO = 114;
        private const uint CONTROL_ID_ID3_GROUP = 115;
        private const uint CONTROL_ID_TITLE_LABEL = 116;
        private const uint CONTROL_ID_TITLE_EDIT = 117;
        private const uint CONTROL_ID_ARTIST_LABEL = 118;
        private const uint CONTROL_ID_ARTIST_EDIT = 119;
        private const uint CONTROL_ID_ALBUM_LABEL = 120;
        private const uint CONTROL_ID_ALBUM_EDIT = 121;
        private const uint CONTROL_ID_NORMALIZATION_LABEL = 122;
        private const uint CONTROL_ID_NORMALIZATION_COMBO = 123;
        private const uint CONTROL_ID_DITHERING_LABEL = 124;
        private const uint CONTROL_ID_DITHERING_COMBO = 125;
        private const uint CONTROL_ID_FADE_LABEL = 126;
        private const uint CONTROL_ID_FADE_COMBO = 127;
        private const uint CONTROL_ID_MP3_CHANNEL_MODE_LABEL = 128;
        private const uint CONTROL_ID_MP3_CHANNEL_MODE_COMBO = 129;
        private const uint CONTROL_ID_MP3_LPF_LABEL = 130;
        private const uint CONTROL_ID_MP3_LPF_COMBO = 131;
        private const uint CONTROL_ID_CLIPPING_CHECK = 132;
        private const uint CONTROL_ID_TRIM_LABEL = 133;
        private const uint CONTROL_ID_TRIM_COMBO = 134;
        private const uint CONTROL_ID_SPLIT_GROUP = 135;
        private const uint CONTROL_ID_SPLIT_TYPE_LABEL = 136;
        private const uint CONTROL_ID_SPLIT_TYPE_COMBO = 137;
        private const uint CONTROL_ID_SPLIT_VALUE_LABEL = 138;
        private const uint CONTROL_ID_SPLIT_VALUE_EDIT = 139;
        private const uint CONTROL_ID_PROJECT_GROUP = 140;
        private const uint CONTROL_ID_SAVE_ALL_CHECK = 141;
        private const uint CONTROL_ID_COMPRESS_CHECK = 142;
        private const uint CDCS_INACTIVE = 0x00000000;
        private const uint CDCS_ENABLED = 0x00000001;
        private const uint CDCS_VISIBLE = 0x00000002;
        private const uint CDCS_ENABLEDVISIBLE = 0x00000003;

        public int OnFileOk(IFileDialog pfd)
        {
            return 0;
        }

        public int OnFolderChanging(IFileDialog pfd, IShellItem psiFolder)
        {
            return 0;
        }

        public int OnFolderChange(IFileDialog pfd)
        {
            return 0;
        }

        public int OnSelectionChange(IFileDialog pfd)
        {
            return 0;
        }

        public int OnShareViolation(IFileDialog pfd, IShellItem psi, out FDE_SHAREVIOLATION_RESPONSE pResponse)
        {
            pResponse = FDE_SHAREVIOLATION_RESPONSE.FDESVR_DEFAULT;
            return 0;
        }

        public int OnTypeChange(IFileDialog pfd)
        {
            try
            {
                var hr = pfd.GetFileTypeIndex(out uint index);
                if (hr < 0) return 0;

                var customize = (IFileDialogCustomize)pfd;

                customize.SetControlState(CONTROL_ID_WAV_GROUP, CDCS_INACTIVE);
                customize.SetControlState(CONTROL_ID_MP3_GROUP, CDCS_INACTIVE);
                customize.SetControlState(CONTROL_ID_ID3_GROUP, CDCS_INACTIVE);
                customize.SetControlState(CONTROL_ID_PROJECT_GROUP, CDCS_INACTIVE);
                customize.SetControlState(CONTROL_ID_NORMALIZATION_LABEL, CDCS_INACTIVE);
                customize.SetControlState(CONTROL_ID_NORMALIZATION_COMBO, CDCS_INACTIVE);
                customize.SetControlState(CONTROL_ID_FADE_LABEL, CDCS_INACTIVE);
                customize.SetControlState(CONTROL_ID_FADE_COMBO, CDCS_INACTIVE);
                customize.SetControlState(CONTROL_ID_CLIPPING_CHECK, CDCS_INACTIVE);
                customize.SetControlState(CONTROL_ID_TRIM_LABEL, CDCS_INACTIVE);
                customize.SetControlState(CONTROL_ID_TRIM_COMBO, CDCS_INACTIVE);
                customize.SetControlState(CONTROL_ID_SPLIT_GROUP, CDCS_INACTIVE);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnTypeChange エラー: {ex}");
            }
            return 0;
        }

        public int OnOverwrite(IFileDialog pfd, IShellItem psi, out FDE_OVERWRITE_RESPONSE pResponse)
        {
            pResponse = FDE_OVERWRITE_RESPONSE.FDEOR_DEFAULT;
            return 0;
        }
    }
}
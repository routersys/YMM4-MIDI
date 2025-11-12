using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace MIDI.Voice.Services
{
    internal static class FolderBrowserHelper
    {
        public static string? ShowDialog(Window owner, string title)
        {
            IntPtr hwndOwner = new WindowInteropHelper(owner).Handle;
            IFileOpenDialog? dialog = null;
            try
            {
                dialog = (IFileOpenDialog)new FileOpenDialog();
                dialog.SetOptions(FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS | FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM);

                if (!string.IsNullOrEmpty(title))
                {
                    dialog.SetTitle(title);
                }

                HRESULT hr = dialog.Show(hwndOwner);
                if (hr != HRESULT.S_OK)
                {
                    return null;
                }

                dialog.GetResult(out IShellItem shellItem);
                if (shellItem != null)
                {
                    shellItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path);
                    Marshal.ReleaseComObject(shellItem);
                    return path;
                }
                return null;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                if (dialog != null)
                {
                    Marshal.ReleaseComObject(dialog);
                }
            }
        }

        private enum HRESULT : uint
        {
            S_OK = 0x0000,
            E_FAIL = 0x80004005,
            E_INVALIDARG = 0x80070057,
            E_OUTOFMEMORY = 0x8007000E,
            E_NOINTERFACE = 0x80004002,
            E_NOTIMPL = 0x80004001,
            E_UNEXPECTED = 0x8000FFFF,
            ERROR_CANCELLED = 0x800704C7
        }

        [ComImport, Guid("DC1C5A9C-E88A-4dde-A5A1-60F82A20AEF7")]
        private class FileOpenDialog
        {
        }

        [ComImport, Guid("42f85136-db7e-439c-85f1-e4075d135fc8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileOpenDialog
        {
            [PreserveSig]
            HRESULT Show([In] IntPtr parent);
            void SetFileTypes([In] uint cFileTypes, [In, MarshalAs(UnmanagedType.LPArray)] COMDLG_FILTERSPEC[] rgFilterSpec);
            void SetFileTypeIndex([In] uint iFileType);
            void GetFileTypeIndex(out uint piFileType);
            void Advise([In, MarshalAs(UnmanagedType.Interface)] IFileDialogEvents pfde, out uint pdwCookie);
            void Unadvise([In] uint dwCookie);
            void SetOptions([In] FILEOPENDIALOGOPTIONS fos);
            void GetOptions(out FILEOPENDIALOGOPTIONS pfos);
            void SetDefaultFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);
            void SetFolder([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi);
            void GetFolder(out IShellItem ppsi);
            void GetCurrentSelection(out IShellItem ppsi);
            void SetFileName([In, MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetFileName(out string pszName);
            void SetTitle([In, MarshalAs(UnmanagedType.LPWStr)] string pszTitle);
            void SetOkButtonLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszText);
            void SetFileNameLabel([In, MarshalAs(UnmanagedType.LPWStr)] string pszLabel);
            void GetResult(out IShellItem ppsi);
            void AddPlace([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, uint fdap);
            void SetDefaultExtension([In, MarshalAs(UnmanagedType.LPWStr)] string pszDefaultExtension);
            void Close([MarshalAs(UnmanagedType.Error)] int hr);
            void SetClientGuid([In] ref Guid guid);
            void ClearClientData();
            void SetFilter([In, MarshalAs(UnmanagedType.Interface)] object pFilter);
            void GetResults(out IShellItemArray ppenum);
            void GetSelectedItems(out IShellItemArray ppsai);
        }

        [ComImport, Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItem
        {
            void BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
            void GetParent(out IShellItem ppsi);
            void GetDisplayName([In] SIGDN sigdnName, [MarshalAs(UnmanagedType.LPWStr)] out string ppszName);
            void GetAttributes([In] uint sfgaoMask, out uint psfgaoAttribs);
            void Compare([In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, [In] uint hint, out int piOrder);
        }

        [ComImport, Guid("B63EA76D-1F85-456F-A19C-48159EFA858B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemArray
        {
            void BindToHandler([In] IntPtr pbc, [In] ref Guid bhid, [In] ref Guid riid, out IntPtr ppv);
            void GetPropertyStore([In] int flags, [In] ref Guid riid, out IntPtr ppv);
            void GetPropertyDescriptionList([In] ref Guid riid, out IntPtr ppv);
            void GetAttributes([In] int AttribFlags, [In] uint sfgaoMask, out uint psfgaoAttribs);
            void GetCount(out uint pdwNumItems);
            void GetItemAt([In] uint dwIndex, out IShellItem ppsi);
            void EnumItems(out IntPtr ppenumShellItems);
        }

        [ComImport, Guid("973510DB-7D7F-452B-8975-74A85828D354"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IFileDialogEvents
        {
            [PreserveSig]
            HRESULT OnFileOk([In, MarshalAs(UnmanagedType.Interface)] IFileOpenDialog pfd);
            [PreserveSig]
            HRESULT OnFolderChanging([In, MarshalAs(UnmanagedType.Interface)] IFileOpenDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psiFolder);
            [PreserveSig]
            HRESULT OnFolderChange([In, MarshalAs(UnmanagedType.Interface)] IFileOpenDialog pfd);
            [PreserveSig]
            HRESULT OnSelectionChange([In, MarshalAs(UnmanagedType.Interface)] IFileOpenDialog pfd);
            [PreserveSig]
            HRESULT OnShareViolation([In, MarshalAs(UnmanagedType.Interface)] IFileOpenDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, out uint pResponse);
            [PreserveSig]
            HRESULT OnTypeChange([In, MarshalAs(UnmanagedType.Interface)] IFileOpenDialog pfd);
            [PreserveSig]
            HRESULT OnOverwrite([In, MarshalAs(UnmanagedType.Interface)] IFileOpenDialog pfd, [In, MarshalAs(UnmanagedType.Interface)] IShellItem psi, out uint pResponse);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct COMDLG_FILTERSPEC
        {
            public string pszName;
            public string pszSpec;
        }

        [Flags]
        private enum FILEOPENDIALOGOPTIONS : uint
        {
            FOS_OVERWRITEPROMPT = 0x00000002,
            FOS_STRICTFILETYPES = 0x00000004,
            FOS_NOCHANGEDIR = 0x00000008,
            FOS_PICKFOLDERS = 0x00000020,
            FOS_FORCEFILESYSTEM = 0x00000040,
            FOS_ALLFINDFILE = 0x00000080,
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
            FOS_DEFAULTNOMINIMODE = 0x20000000,
            FOS_FORCEPREVIEWPANEON = 0x40000000,
            FOS_SUPPORTSTREAMABLEITEMS = 0x80000000
        }

        private enum SIGDN : uint
        {
            SIGDN_DESKTOPABSOLUTEEDITING = 0x8004C000,
            SIGDN_DESKTOPABSOLUTEPARSING = 0x80028000,
            SIGDN_FILESYSPATH = 0x80058000,
            SIGDN_NORMALDISPLAY = 0x00000000,
            SIGDN_PARENTRELATIVE = 0x80080001,
            SIGDN_PARENTRELATIVEEDITING = 0x80031001,
            SIGDN_PARENTRELATIVEFORADDRESSBAR = 0x8007C001,
            SIGDN_PARENTRELATIVEPARSING = 0x80018001,
            SIGDN_URL = 0x80068000
        }
    }
}
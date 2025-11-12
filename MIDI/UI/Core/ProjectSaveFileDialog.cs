using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace MIDI.UI.Core
{
    public class ProjectSaveFileDialog : IFileDialogEvents
    {
        private const uint CONTROL_ID_PROJECT_GROUP = 140;
        private const uint CONTROL_ID_SAVE_ALL_CHECK = 141;
        private const uint CONTROL_ID_COMPRESS_CHECK = 142;
        private const uint CDCS_INACTIVE = 0x00000000;
        private const uint CDCS_ENABLED = 0x00000001;
        private const uint CDCS_VISIBLE = 0x00000002;
        private const uint CDCS_ENABLEDVISIBLE = 0x00000003;

        private IFileSaveDialog? _dialog;
        private uint _cookie;
        private bool _eventConnected;

        public string? FilePath { get; private set; }
        public bool SaveAllData { get; private set; } = false;
        public bool CompressProject { get; private set; } = false;


        public ProjectSaveFileDialog(string initialFileName)
        {
            try
            {
                _dialog = (IFileSaveDialog)new FileSaveDialogRCW();

                _dialog.SetOptions(FOS.FOS_FORCEFILESYSTEM | FOS.FOS_PATHMUSTEXIST | FOS.FOS_OVERWRITEPROMPT);
                _dialog.SetTitle("プロジェクトを名前を付けて保存");
                _dialog.SetFileName(initialFileName);
                _dialog.SetFileTypes(1, new[]
                {
                    new COMDLG_FILTERSPEC { pszName = "YMM4 MIDIプロジェクト (*.ymidi)", pszSpec = "*.ymidi" },
                });
                _dialog.SetFileTypeIndex(1);
                _dialog.SetDefaultExtension("ymidi");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"初期化エラー: {ex}");
                throw;
            }
        }

        public bool? ShowDialog(IntPtr owner = default)
        {
            if (_dialog == null) return false;

            IFileDialogCustomize? customize = null;

            try
            {
                var hr = _dialog.Advise(this, out _cookie);
                if (hr < 0) Marshal.ThrowExceptionForHR(hr);
                _eventConnected = true;

                customize = (IFileDialogCustomize)_dialog;

                customize.StartVisualGroup(CONTROL_ID_PROJECT_GROUP, "プロジェクト設定");
                customize.AddCheckButton(CONTROL_ID_SAVE_ALL_CHECK, "すべての情報を保存する", false);
                customize.AddCheckButton(CONTROL_ID_COMPRESS_CHECK, "圧縮する", false);
                customize.EndVisualGroup();

                OnTypeChange(_dialog);

                var result = _dialog.Show(owner);

                if (result == 0) // S_OK
                {
                    hr = _dialog.GetResult(out var shellItem);
                    if (hr >= 0 && shellItem != null)
                    {
                        try
                        {
                            shellItem.GetDisplayName(SIGDN.SIGDN_FILESYSPATH, out string path);
                            FilePath = path;
                        }
                        finally
                        {
                            Marshal.ReleaseComObject(shellItem);
                        }
                    }

                    customize.GetCheckButtonState(CONTROL_ID_SAVE_ALL_CHECK, out bool saveAll);
                    SaveAllData = saveAll;
                    customize.GetCheckButtonState(CONTROL_ID_COMPRESS_CHECK, out bool compress);
                    CompressProject = compress;

                    return true;
                }
                else if (result == unchecked((int)0x800704C7))
                {
                    return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ダイアログエラー: {ex}");
                return false;
            }
            finally
            {
                if (_eventConnected && _dialog != null && _cookie != 0)
                {
                    try
                    {
                        _dialog.Unadvise(_cookie);
                    }
                    catch { }
                    _eventConnected = false;
                }
            }
        }

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

                if (index == 1) // YMIDI
                {
                    customize.SetControlState(CONTROL_ID_PROJECT_GROUP, CDCS_ENABLEDVISIBLE);
                }
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
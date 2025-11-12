using System;
using System.Runtime.InteropServices;

namespace MIDI.UI.Core
{
    public class AudioSaveFileDialog : IFileDialogEvents
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

        private const uint CDCS_INACTIVE = 0x00000000;
        private const uint CDCS_ENABLED = 0x00000001;
        private const uint CDCS_VISIBLE = 0x00000002;
        private const uint CDCS_ENABLEDVISIBLE = 0x00000003;

        private IFileSaveDialog? _dialog;
        private uint _cookie;
        private bool _eventConnected;

        public string? FilePath { get; private set; }
        public string SelectedFormat { get; private set; } = "WAV";
        public int SelectedBitrate { get; private set; } = 128;
        public int SelectedBitDepth { get; private set; } = 16;
        public int SelectedSampleRate { get; private set; } = 44100;
        public int SelectedChannels { get; private set; } = 2;
        public string SelectedEncodeQuality { get; private set; } = "Standard";
        public string SelectedVbrMode { get; private set; } = "CBR";
        public string Title { get; private set; } = "";
        public string Artist { get; private set; } = "";
        public string Album { get; private set; } = "";
        public string NormalizationType { get; private set; } = "Off";
        public string DitheringType { get; private set; } = "None";
        public double FadeLength { get; private set; } = 0;
        public string Mp3ChannelMode { get; private set; } = "JointStereo";
        public string Mp3LowPassFilter { get; private set; } = "Auto";
        public bool PreventClipping { get; private set; } = true;
        public string TrimSilence { get; private set; } = "Off";
        public string SplitType { get; private set; } = "None";
        public string SplitValue { get; private set; } = "";


        public AudioSaveFileDialog(string initialFileName)
        {
            try
            {
                _dialog = (IFileSaveDialog)new FileSaveDialogRCW();

                _dialog.SetOptions(FOS.FOS_FORCEFILESYSTEM | FOS.FOS_PATHMUSTEXIST | FOS.FOS_OVERWRITEPROMPT);
                _dialog.SetTitle("オーディオ書き出し");
                _dialog.SetFileName(initialFileName);
                _dialog.SetFileTypes(2, new[]
                {
                    new COMDLG_FILTERSPEC { pszName = "WAV Audio (*.wav)", pszSpec = "*.wav" },
                    new COMDLG_FILTERSPEC { pszName = "MP3 Audio (*.mp3)", pszSpec = "*.mp3" }
                });
                _dialog.SetFileTypeIndex(1);
                _dialog.SetDefaultExtension("wav");
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

                customize.StartVisualGroup(CONTROL_ID_WAV_GROUP, "WAV 設定");
                customize.AddText(CONTROL_ID_BITDEPTH_LABEL, "ビット深度:");
                customize.AddComboBox(CONTROL_ID_BITDEPTH_COMBO);
                customize.AddControlItem(CONTROL_ID_BITDEPTH_COMBO, 0, "16-bit");
                customize.AddControlItem(CONTROL_ID_BITDEPTH_COMBO, 1, "24-bit");
                customize.AddControlItem(CONTROL_ID_BITDEPTH_COMBO, 2, "32-bit float");
                customize.SetSelectedControlItem(CONTROL_ID_BITDEPTH_COMBO, 0);
                customize.AddText(CONTROL_ID_SAMPLERATE_LABEL, "サンプルレート:");
                customize.AddComboBox(CONTROL_ID_SAMPLERATE_COMBO);
                customize.AddControlItem(CONTROL_ID_SAMPLERATE_COMBO, 0, "44.1 kHz");
                customize.AddControlItem(CONTROL_ID_SAMPLERATE_COMBO, 1, "48 kHz");
                customize.AddControlItem(CONTROL_ID_SAMPLERATE_COMBO, 2, "96 kHz");
                customize.AddControlItem(CONTROL_ID_SAMPLERATE_COMBO, 3, "192 kHz");
                customize.SetSelectedControlItem(CONTROL_ID_SAMPLERATE_COMBO, 0);
                customize.AddText(CONTROL_ID_CHANNELS_LABEL, "チャンネル数:");
                customize.AddComboBox(CONTROL_ID_CHANNELS_COMBO);
                customize.AddControlItem(CONTROL_ID_CHANNELS_COMBO, 0, "モノラル");
                customize.AddControlItem(CONTROL_ID_CHANNELS_COMBO, 1, "ステレオ");
                customize.SetSelectedControlItem(CONTROL_ID_CHANNELS_COMBO, 1);
                customize.AddText(CONTROL_ID_DITHERING_LABEL, "ディザリング:");
                customize.AddComboBox(CONTROL_ID_DITHERING_COMBO);
                customize.AddControlItem(CONTROL_ID_DITHERING_COMBO, 0, "なし");
                customize.AddControlItem(CONTROL_ID_DITHERING_COMBO, 1, "トライアングラー");
                customize.AddControlItem(CONTROL_ID_DITHERING_COMBO, 2, "TPDF");
                customize.SetSelectedControlItem(CONTROL_ID_DITHERING_COMBO, 0);
                customize.EndVisualGroup();

                customize.StartVisualGroup(CONTROL_ID_MP3_GROUP, "MP3 設定");
                customize.AddText(CONTROL_ID_ENCODE_QUALITY_LABEL, "エンコード品質:");
                customize.AddComboBox(CONTROL_ID_ENCODE_QUALITY_COMBO);
                customize.AddControlItem(CONTROL_ID_ENCODE_QUALITY_COMBO, 0, "高速");
                customize.AddControlItem(CONTROL_ID_ENCODE_QUALITY_COMBO, 1, "標準");
                customize.AddControlItem(CONTROL_ID_ENCODE_QUALITY_COMBO, 2, "高品質");
                customize.SetSelectedControlItem(CONTROL_ID_ENCODE_QUALITY_COMBO, 1);
                customize.AddText(CONTROL_ID_VBR_MODE_LABEL, "ビットレートモード:");
                customize.AddComboBox(CONTROL_ID_VBR_MODE_COMBO);
                customize.AddControlItem(CONTROL_ID_VBR_MODE_COMBO, 0, "CBR (固定)");
                customize.AddControlItem(CONTROL_ID_VBR_MODE_COMBO, 1, "VBR (品質ベース)");
                customize.AddControlItem(CONTROL_ID_VBR_MODE_COMBO, 2, "ABR (平均)");
                customize.SetSelectedControlItem(CONTROL_ID_VBR_MODE_COMBO, 0);
                customize.AddText(CONTROL_ID_BITRATE_LABEL, "ビットレート (kbps):");
                customize.AddComboBox(CONTROL_ID_BITRATE_COMBO);
                customize.AddControlItem(CONTROL_ID_BITRATE_COMBO, 0, "96");
                customize.AddControlItem(CONTROL_ID_BITRATE_COMBO, 1, "128");
                customize.AddControlItem(CONTROL_ID_BITRATE_COMBO, 2, "192");
                customize.AddControlItem(CONTROL_ID_BITRATE_COMBO, 3, "256");
                customize.AddControlItem(CONTROL_ID_BITRATE_COMBO, 4, "320");
                customize.SetSelectedControlItem(CONTROL_ID_BITRATE_COMBO, 1);
                customize.AddText(CONTROL_ID_MP3_CHANNEL_MODE_LABEL, "チャンネルモード:");
                customize.AddComboBox(CONTROL_ID_MP3_CHANNEL_MODE_COMBO);
                customize.AddControlItem(CONTROL_ID_MP3_CHANNEL_MODE_COMBO, 0, "ジョイントステレオ");
                customize.AddControlItem(CONTROL_ID_MP3_CHANNEL_MODE_COMBO, 1, "ステレオ");
                customize.AddControlItem(CONTROL_ID_MP3_CHANNEL_MODE_COMBO, 2, "モノラル");
                customize.SetSelectedControlItem(CONTROL_ID_MP3_CHANNEL_MODE_COMBO, 0);
                customize.AddText(CONTROL_ID_MP3_LPF_LABEL, "ローパスフィルター:");
                customize.AddComboBox(CONTROL_ID_MP3_LPF_COMBO);
                customize.AddControlItem(CONTROL_ID_MP3_LPF_COMBO, 0, "自動");
                customize.AddControlItem(CONTROL_ID_MP3_LPF_COMBO, 1, "16 kHz");
                customize.AddControlItem(CONTROL_ID_MP3_LPF_COMBO, 2, "18 kHz");
                customize.AddControlItem(CONTROL_ID_MP3_LPF_COMBO, 3, "20 kHz");
                customize.AddControlItem(CONTROL_ID_MP3_LPF_COMBO, 4, "なし");
                customize.SetSelectedControlItem(CONTROL_ID_MP3_LPF_COMBO, 0);
                customize.EndVisualGroup();

                customize.StartVisualGroup(CONTROL_ID_ID3_GROUP, "ID3 タグ");
                customize.AddText(CONTROL_ID_TITLE_LABEL, "タイトル:");
                customize.AddEditBox(CONTROL_ID_TITLE_EDIT, "");
                customize.AddText(CONTROL_ID_ARTIST_LABEL, "アーティスト:");
                customize.AddEditBox(CONTROL_ID_ARTIST_EDIT, "");
                customize.AddText(CONTROL_ID_ALBUM_LABEL, "アルバム:");
                customize.AddEditBox(CONTROL_ID_ALBUM_EDIT, "");
                customize.EndVisualGroup();

                customize.StartVisualGroup(0, "共通設定");
                customize.AddText(CONTROL_ID_NORMALIZATION_LABEL, "正規化:");
                customize.AddComboBox(CONTROL_ID_NORMALIZATION_COMBO);
                customize.AddControlItem(CONTROL_ID_NORMALIZATION_COMBO, 0, "オフ");
                customize.AddControlItem(CONTROL_ID_NORMALIZATION_COMBO, 1, "ピーク正規化 (-0.1 dB)");
                customize.AddControlItem(CONTROL_ID_NORMALIZATION_COMBO, 2, "ピーク正規化 (-1 dB)");
                customize.AddControlItem(CONTROL_ID_NORMALIZATION_COMBO, 3, "ピーク正規化 (-3 dB)");
                customize.AddControlItem(CONTROL_ID_NORMALIZATION_COMBO, 4, "ラウドネス正規化 (LUFS)");
                customize.SetSelectedControlItem(CONTROL_ID_NORMALIZATION_COMBO, 0);
                customize.AddText(CONTROL_ID_FADE_LABEL, "フェード処理:");
                customize.AddComboBox(CONTROL_ID_FADE_COMBO);
                customize.AddControlItem(CONTROL_ID_FADE_COMBO, 0, "0秒");
                customize.AddControlItem(CONTROL_ID_FADE_COMBO, 1, "0.5秒");
                customize.AddControlItem(CONTROL_ID_FADE_COMBO, 2, "1秒");
                customize.AddControlItem(CONTROL_ID_FADE_COMBO, 3, "2秒");
                customize.SetSelectedControlItem(CONTROL_ID_FADE_COMBO, 0);
                customize.AddCheckButton(CONTROL_ID_CLIPPING_CHECK, "クリッピング防止", true);
                customize.AddText(CONTROL_ID_TRIM_LABEL, "無音部分のトリミング:");
                customize.AddComboBox(CONTROL_ID_TRIM_COMBO);
                customize.AddControlItem(CONTROL_ID_TRIM_COMBO, 0, "オフ");
                customize.AddControlItem(CONTROL_ID_TRIM_COMBO, 1, "開始のみ");
                customize.AddControlItem(CONTROL_ID_TRIM_COMBO, 2, "終了のみ");
                customize.AddControlItem(CONTROL_ID_TRIM_COMBO, 3, "両端");
                customize.SetSelectedControlItem(CONTROL_ID_TRIM_COMBO, 0);
                customize.EndVisualGroup();

                customize.StartVisualGroup(CONTROL_ID_SPLIT_GROUP, "ファイル分割");
                customize.AddText(CONTROL_ID_SPLIT_TYPE_LABEL, "分割方法:");
                customize.AddComboBox(CONTROL_ID_SPLIT_TYPE_COMBO);
                customize.AddControlItem(CONTROL_ID_SPLIT_TYPE_COMBO, 0, "分割しない");
                customize.AddControlItem(CONTROL_ID_SPLIT_TYPE_COMBO, 1, "時間で分割 (分)");
                customize.AddControlItem(CONTROL_ID_SPLIT_TYPE_COMBO, 2, "サイズで分割 (MB)");
                customize.SetSelectedControlItem(CONTROL_ID_SPLIT_TYPE_COMBO, 0);
                customize.AddText(CONTROL_ID_SPLIT_VALUE_LABEL, "分割値:");
                customize.AddEditBox(CONTROL_ID_SPLIT_VALUE_EDIT, "");
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

                    if (SelectedFormat == "WAV")
                    {
                        customize.GetSelectedControlItem(CONTROL_ID_BITDEPTH_COMBO, out uint bitDepthIndex);
                        SelectedBitDepth = bitDepthIndex switch { 0 => 16, 1 => 24, 2 => 32, _ => 16 };
                        customize.GetSelectedControlItem(CONTROL_ID_SAMPLERATE_COMBO, out uint sampleRateIndex);
                        SelectedSampleRate = sampleRateIndex switch { 0 => 44100, 1 => 48000, 2 => 96000, 3 => 192000, _ => 44100 };
                        customize.GetSelectedControlItem(CONTROL_ID_CHANNELS_COMBO, out uint channelsIndex);
                        SelectedChannels = channelsIndex switch { 0 => 1, 1 => 2, _ => 2 };
                        customize.GetSelectedControlItem(CONTROL_ID_DITHERING_COMBO, out uint ditherIndex);
                        DitheringType = ditherIndex switch { 0 => "None", 1 => "Triangular", 2 => "TPDF", _ => "None" };
                    }
                    else if (SelectedFormat == "MP3")
                    {
                        customize.GetSelectedControlItem(CONTROL_ID_ENCODE_QUALITY_COMBO, out uint qualityIndex);
                        SelectedEncodeQuality = qualityIndex switch { 0 => "Fast", 1 => "Standard", 2 => "High", _ => "Standard" };
                        customize.GetSelectedControlItem(CONTROL_ID_VBR_MODE_COMBO, out uint vbrIndex);
                        SelectedVbrMode = vbrIndex switch { 0 => "CBR", 1 => "VBR", 2 => "ABR", _ => "CBR" };
                        customize.GetSelectedControlItem(CONTROL_ID_BITRATE_COMBO, out uint bitrateIndex);
                        SelectedBitrate = bitrateIndex switch { 0 => 96, 1 => 128, 2 => 192, 3 => 256, 4 => 320, _ => 128 };
                        customize.GetSelectedControlItem(CONTROL_ID_MP3_CHANNEL_MODE_COMBO, out uint channelModeIndex);
                        Mp3ChannelMode = channelModeIndex switch { 0 => "JointStereo", 1 => "Stereo", 2 => "Mono", _ => "JointStereo" };
                        customize.GetSelectedControlItem(CONTROL_ID_MP3_LPF_COMBO, out uint lpfIndex);
                        Mp3LowPassFilter = lpfIndex switch { 0 => "Auto", 1 => "16kHz", 2 => "18kHz", 3 => "20kHz", 4 => "None", _ => "Auto" };
                        customize.GetEditBoxText(CONTROL_ID_TITLE_EDIT, out string title);
                        Title = title;
                        customize.GetEditBoxText(CONTROL_ID_ARTIST_EDIT, out string artist);
                        Artist = artist;
                        customize.GetEditBoxText(CONTROL_ID_ALBUM_EDIT, out string album);
                        Album = album;
                    }

                    customize.GetSelectedControlItem(CONTROL_ID_NORMALIZATION_COMBO, out uint normIndex);
                    NormalizationType = normIndex switch { 0 => "Off", 1 => "Peak-0.1", 2 => "Peak-1", 3 => "Peak-3", 4 => "LUFS", _ => "Off" };
                    customize.GetSelectedControlItem(CONTROL_ID_FADE_COMBO, out uint fadeIndex);
                    FadeLength = fadeIndex switch { 0 => 0, 1 => 0.5, 2 => 1, 3 => 2, _ => 0 };
                    customize.GetCheckButtonState(CONTROL_ID_CLIPPING_CHECK, out bool clipping);
                    PreventClipping = clipping;
                    customize.GetSelectedControlItem(CONTROL_ID_TRIM_COMBO, out uint trimIndex);
                    TrimSilence = trimIndex switch { 0 => "Off", 1 => "Start", 2 => "End", 3 => "Both", _ => "Off" };
                    customize.GetSelectedControlItem(CONTROL_ID_SPLIT_TYPE_COMBO, out uint splitTypeIndex);
                    SplitType = splitTypeIndex switch { 0 => "None", 1 => "Time", 2 => "Size", _ => "None" };
                    customize.GetEditBoxText(CONTROL_ID_SPLIT_VALUE_EDIT, out string splitValue);
                    SplitValue = splitValue;

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

                var wavControls = new[] {
                    CONTROL_ID_WAV_GROUP, CONTROL_ID_BITDEPTH_LABEL, CONTROL_ID_BITDEPTH_COMBO,
                    CONTROL_ID_SAMPLERATE_LABEL, CONTROL_ID_SAMPLERATE_COMBO, CONTROL_ID_CHANNELS_LABEL,
                    CONTROL_ID_CHANNELS_COMBO, CONTROL_ID_DITHERING_LABEL, CONTROL_ID_DITHERING_COMBO
                };

                var mp3Controls = new[] {
                    CONTROL_ID_MP3_GROUP, CONTROL_ID_ENCODE_QUALITY_LABEL, CONTROL_ID_ENCODE_QUALITY_COMBO,
                    CONTROL_ID_VBR_MODE_LABEL, CONTROL_ID_VBR_MODE_COMBO, CONTROL_ID_BITRATE_LABEL,
                    CONTROL_ID_BITRATE_COMBO, CONTROL_ID_MP3_CHANNEL_MODE_LABEL, CONTROL_ID_MP3_CHANNEL_MODE_COMBO,
                    CONTROL_ID_MP3_LPF_LABEL, CONTROL_ID_MP3_LPF_COMBO
                };

                var id3Controls = new[] {
                    CONTROL_ID_ID3_GROUP, CONTROL_ID_TITLE_LABEL, CONTROL_ID_TITLE_EDIT,
                    CONTROL_ID_ARTIST_LABEL, CONTROL_ID_ARTIST_EDIT, CONTROL_ID_ALBUM_LABEL,
                    CONTROL_ID_ALBUM_EDIT
                };

                if (index == 1) // WAV
                {
                    foreach (var id in wavControls) customize.SetControlState(id, CDCS_ENABLEDVISIBLE);
                    foreach (var id in mp3Controls) customize.SetControlState(id, CDCS_INACTIVE);
                    foreach (var id in id3Controls) customize.SetControlState(id, CDCS_INACTIVE);
                    SelectedFormat = "WAV";
                }
                else if (index == 2) // MP3
                {
                    foreach (var id in wavControls) customize.SetControlState(id, CDCS_INACTIVE);
                    foreach (var id in mp3Controls) customize.SetControlState(id, CDCS_ENABLEDVISIBLE);
                    foreach (var id in id3Controls) customize.SetControlState(id, CDCS_ENABLEDVISIBLE);
                    SelectedFormat = "MP3";
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
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using MIDI.Utils;

namespace MIDI.UI.ViewModels.Services
{
    public class DragDropService : IDragDropService
    {
        private readonly MidiConfiguration _settings;
        private readonly IFileService _fileService;
        private readonly IPresetService _presetService;
        private readonly IPresetManager _presetManager;

        public DragDropService(MidiConfiguration settings, IFileService fileService, IPresetService presetService)
        {
            _settings = settings;
            _fileService = fileService;
            _presetService = presetService;
            _presetManager = new PresetManager();
        }

        public (bool, string) CanHandleDrop(DragEventArgs e)
        {
            if (e is null) return (false, "");

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
                {
                    var extension = Path.GetExtension(files[0]).ToLower();
                    if (extension == ".mpp")
                    {
                        e.Effects = DragDropEffects.Copy;
                        return (true, "プリセットファイルを追加");
                    }
                    else if (extension == ".wav" || extension == ".sf2")
                    {
                        e.Effects = DragDropEffects.Copy;
                        return (true, "ファイルを追加");
                    }
                }
            }
            e.Effects = DragDropEffects.None;
            return (false, "");
        }

        public async Task<bool> HandleDropAsync(IDataObject dataObject)
        {
            if (dataObject == null || !dataObject.GetDataPresent(DataFormats.FileDrop)) return false;
            if (dataObject.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return false;

            var filePath = files[0];
            var extension = Path.GetExtension(filePath)?.ToLower();
            bool presetsChanged = false;

            try
            {
                switch (extension)
                {
                    case ".mpp":
                        await HandlePresetDropAsync(filePath);
                        presetsChanged = true;
                        break;
                    case ".wav":
                        await HandleFileDropAsync(filePath, _settings.Synthesis.WavetableDirectory, "Wavetable");
                        break;
                    case ".sf2":
                        await HandleFileDropAsync(filePath, _settings.SoundFont.DefaultSoundFontDirectory, "SoundFont");
                        break;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ファイルの処理中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Error(LogMessages.FileDropError, ex, ex.Message);
            }

            return presetsChanged;
        }

        private async Task HandlePresetDropAsync(string filePath)
        {
            var presetContent = await File.ReadAllTextAsync(filePath);
            var (certificate, presetNode) = _presetManager.ParsePresetContent(presetContent);

            if (presetNode == null)
            {
                MessageBox.Show("プリセットファイルの読み込みに失敗しました。", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.Error(LogMessages.InvalidPresetFile, null);
                return;
            }

            var tempConfig = _settings.Clone();
            tempConfig.ApplyPreset(presetNode);

            var changedItems = tempConfig.GetChangedProperties(_settings);

            var confirmViewModel = new PresetDropConfirmViewModel(certificate, changedItems);
            var confirmWindow = new PresetDropConfirmWindow(confirmViewModel)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (confirmWindow.ShowDialog() == true)
            {
                await HandleFileDropAsync(filePath, PresetManager.PresetDirectory, "プリセット", false);
                await _presetService.LoadPresetFilesAsync();
            }
        }

        private async Task HandleFileDropAsync(string sourcePath, string targetDirectoryName, string fileType, bool showConfirmation = true)
        {
            var assemblyLocation = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "";
            var targetDir = Path.IsPathRooted(targetDirectoryName) ? targetDirectoryName : Path.Combine(assemblyLocation, targetDirectoryName);

            if (!Directory.Exists(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            var destPath = Path.Combine(targetDir, Path.GetFileName(sourcePath));

            if (showConfirmation && File.Exists(destPath) && new FileInfo(sourcePath).FullName != new FileInfo(destPath).FullName)
            {
                var vm = new OverwriteConfirmationViewModel(Path.GetFileName(sourcePath));
                var window = new OverwriteConfirmationWindow(vm)
                {
                    Owner = Application.Current.MainWindow,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };
                if (window.ShowDialog() != true)
                {
                    return;
                }
            }

            var progressViewModel = new FileDropProgressViewModel
            {
                FileName = Path.GetFileName(sourcePath),
                FileSize = $"{new FileInfo(sourcePath).Length / 1024.0:F2} KB",
                StatusMessage = "準備中..."
            };

            var progressWindow = new FileDropProgressWindow(progressViewModel)
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            progressWindow.Show();

            try
            {
                await _fileService.CopyFileWithProgressAsync(sourcePath, destPath, progressViewModel);
                progressViewModel.StatusMessage = "完了";
                progressViewModel.IsComplete = true;
            }
            catch (Exception ex)
            {
                progressViewModel.StatusMessage = $"エラー: {ex.Message}";
                progressViewModel.IsComplete = true;
            }
        }
    }
}
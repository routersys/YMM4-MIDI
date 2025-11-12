using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MIDI.UI.ViewModels.Models;
using MIDI.Utils;

namespace MIDI.UI.ViewModels.Services
{
    public class FileService : IFileService
    {
        private readonly MidiConfiguration _settings;

        public FileService(MidiConfiguration settings)
        {
            _settings = settings;
        }

        public async Task<List<SfzFileViewModel>> GetSfzFilesAsync()
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var sfzSearchDir = Path.Combine(assemblyLocation, _settings.SFZ.SfzSearchPath);

            var files = await Task.Run(() =>
            {
                if (!Directory.Exists(sfzSearchDir))
                {
                    try
                    {
                        Directory.CreateDirectory(sfzSearchDir);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(LogMessages.CreateSfzDirectoryFailed, ex);
                        return new List<string>();
                    }
                }
                return Directory.GetFiles(sfzSearchDir, "*.sfz", SearchOption.AllDirectories)
                                .Select(fullPath => Path.GetRelativePath(sfzSearchDir, fullPath))
                                .ToList();
            });

            var sfzFiles = new List<SfzFileViewModel>();
            var mappedFiles = _settings.SFZ.ProgramMaps.ToDictionary(m => m.FilePath, m => m);

            foreach (var file in files)
            {
                var vm = new SfzFileViewModel(file);
                if (mappedFiles.TryGetValue(file, out var map))
                {
                    vm.Map = map;
                }
                sfzFiles.Add(vm);
            }

            foreach (var map in _settings.SFZ.ProgramMaps)
            {
                if (sfzFiles.All(f => f.FileName != map.FilePath))
                {
                    var vm = new SfzFileViewModel(map.FilePath, isMissing: true)
                    {
                        Map = map
                    };
                    sfzFiles.Add(vm);
                }
            }
            return sfzFiles;
        }

        public async Task<List<SoundFontFileViewModel>> GetSoundFontFilesAsync()
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var sfDir = Path.Combine(assemblyLocation, _settings.SoundFont.DefaultSoundFontDirectory);

            var files = await Task.Run(() =>
            {
                if (!Directory.Exists(sfDir))
                {
                    try
                    {
                        Directory.CreateDirectory(sfDir);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(LogMessages.CreateSoundFontDirectoryFailed, ex);
                        return new List<string>();
                    }
                }
                return Directory.GetFiles(sfDir, "*.sf2", SearchOption.AllDirectories)
                                .Select(Path.GetFileName)
                                .Where(f => f != null)
                                .Select(f => f!)
                                .ToList();
            });

            var soundFontFiles = new List<SoundFontFileViewModel>();
            var mappedFiles = _settings.SoundFont.Rules.ToDictionary(r => r.SoundFontFile, r => r);

            foreach (var file in files)
            {
                var vm = new SoundFontFileViewModel(file);
                if (mappedFiles.TryGetValue(file, out var rule))
                {
                    vm.Rule = rule;
                }
                soundFontFiles.Add(vm);
            }

            foreach (var rule in _settings.SoundFont.Rules)
            {
                if (soundFontFiles.All(f => f.FileName != rule.SoundFontFile))
                {
                    var vm = new SoundFontFileViewModel(rule.SoundFontFile, isMissing: true)
                    {
                        Rule = rule
                    };
                    soundFontFiles.Add(vm);
                }
            }
            return soundFontFiles;
        }

        public async Task<List<string>> GetWavetableFilesAsync()
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "";
            var wavetableDir = Path.Combine(assemblyLocation, _settings.Synthesis.WavetableDirectory);

            return await Task.Run(() =>
            {
                if (!Directory.Exists(wavetableDir))
                {
                    try { Directory.CreateDirectory(wavetableDir); } catch { return new List<string>(); }
                }
                return Directory.GetFiles(wavetableDir, "*.wav", SearchOption.AllDirectories)
                                .Select(Path.GetFileName)
                                .Where(f => f != null)
                                .Select(f => f!)
                                .ToList();
            });
        }

        public async Task CopyFileWithProgressAsync(string sourcePath, string destPath, FileDropProgressViewModel viewModel)
        {
            var buffer = new byte[81920];
            long totalBytesRead = 0;
            var fileInfo = new FileInfo(sourcePath);
            var totalBytes = fileInfo.Length;
            var stopwatch = Stopwatch.StartNew();

            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, true))
            using (var destStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true))
            {
                int bytesRead;
                while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await destStream.WriteAsync(buffer, 0, bytesRead);
                    totalBytesRead += bytesRead;

                    var percentage = (double)totalBytesRead / totalBytes * 100;
                    viewModel.Progress = percentage;

                    var elapsedSeconds = stopwatch.Elapsed.TotalSeconds;
                    if (elapsedSeconds > 0.1 && totalBytesRead > 0)
                    {
                        var bytesPerSecond = totalBytesRead / elapsedSeconds;
                        var remainingBytes = totalBytes - totalBytesRead;
                        var remainingSeconds = remainingBytes / bytesPerSecond;
                        viewModel.EstimatedTimeRemaining = $"残り: {System.TimeSpan.FromSeconds(remainingSeconds):mm\\:ss}";
                    }
                    viewModel.StatusMessage = "コピー中...";
                }
            }
            stopwatch.Stop();
        }
    }
}
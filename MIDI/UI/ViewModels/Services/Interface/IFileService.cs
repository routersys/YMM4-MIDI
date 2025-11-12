using System.Collections.Generic;
using System.Threading.Tasks;
using MIDI.UI.ViewModels.Models;

namespace MIDI.UI.ViewModels.Services
{
    public interface IFileService
    {
        Task<List<SfzFileViewModel>> GetSfzFilesAsync();
        Task<List<SoundFontFileViewModel>> GetSoundFontFilesAsync();
        Task<List<string>> GetWavetableFilesAsync();
        Task CopyFileWithProgressAsync(string sourcePath, string destPath, FileDropProgressViewModel viewModel);
    }
}
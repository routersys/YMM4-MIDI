using System.Threading.Tasks;

namespace MIDI.UI.ViewModels.Services
{
    public interface IPresetService
    {
        string CurrentSettingsPresetName { get; }
        Task SavePresetWithOptionsAsync(string newPresetName);
        Task LoadPresetAsync(string presetName);
        Task DeletePresetAsync(string presetName);
        Task LoadPresetFilesAsync();
    }
}
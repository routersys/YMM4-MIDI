using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using MIDI.UI.ViewModels.Models;
using MIDI.Utils;

namespace MIDI.UI.ViewModels.Services
{
    public class PresetService : IPresetService
    {
        private readonly MidiConfiguration _settings;
        private readonly ObservableCollection<PresetViewModel> _presets;
        private readonly IPresetManager _presetManager;
        private MidiConfiguration? _cachedSettings;
        public string CurrentSettingsPresetName => "現在の設定";

        public PresetService(MidiConfiguration settings, ObservableCollection<PresetViewModel> presets, IPresetManager presetManager)
        {
            _settings = settings;
            _presets = presets;
            _presetManager = presetManager;
        }

        public async Task SavePresetWithOptionsAsync(string newPresetName)
        {
            if (string.IsNullOrWhiteSpace(newPresetName) || newPresetName == CurrentSettingsPresetName)
            {
                MessageBox.Show("有効なプリセット名を入力してください。", "保存エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var optionsWindow = new SavePresetOptionsWindow
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            if (optionsWindow.ShowDialog() == true && optionsWindow.SelectedCategories != null && optionsWindow.SelectedCategories.Any())
            {
                await _presetManager.SavePresetAsync(newPresetName, _settings, optionsWindow.SelectedCategories);
                await LoadPresetFilesAsync();
            }
        }

        public async Task LoadPresetAsync(string presetName)
        {
            _settings.BeginApplyPreset();

            if (presetName == CurrentSettingsPresetName)
            {
                if (_cachedSettings != null)
                {
                    _settings.CopyFrom(_cachedSettings);
                    _cachedSettings = null;
                }
            }
            else
            {
                if (_cachedSettings == null)
                {
                    _cachedSettings = _settings.Clone();
                }
                var (_, presetNode) = await _presetManager.LoadPresetAsync(presetName);
                if (presetNode != null)
                {
                    var baseConfig = new MidiConfiguration();
                    baseConfig.ApplyPreset(presetNode);
                    _settings.CopyFrom(baseConfig);
                }
                else
                {
                    Logger.Error(LogMessages.PresetLoadFailed, null, presetName);
                }
            }

            _settings.EndApplyPreset();
        }

        public async Task DeletePresetAsync(string presetName)
        {
            if (!string.IsNullOrEmpty(presetName) && presetName != CurrentSettingsPresetName)
            {
                await _presetManager.DeletePresetAsync(presetName);
                await LoadPresetFilesAsync();
            }
        }

        public async Task LoadPresetFilesAsync()
        {
            var presetFiles = await _presetManager.GetPresetListAsync();
            var defaultConfig = new MidiConfiguration();

            var currentSelectionName = _presets.FirstOrDefault(p => p.Name == CurrentSettingsPresetName)?.Name ?? CurrentSettingsPresetName;
            _presets.Clear();
            _presets.Add(new PresetViewModel { Name = CurrentSettingsPresetName, ChangesCount = _settings.CountChanges(defaultConfig) });

            foreach (var presetName in presetFiles)
            {
                var (_, presetNode) = await _presetManager.LoadPresetAsync(presetName);
                if (presetNode != null)
                {
                    var tempConfig = new MidiConfiguration();
                    tempConfig.ApplyPreset(presetNode);
                    _presets.Add(new PresetViewModel
                    {
                        Name = presetName,
                        ChangesCount = tempConfig.CountChanges(defaultConfig)
                    });
                }
            }
        }
    }
}
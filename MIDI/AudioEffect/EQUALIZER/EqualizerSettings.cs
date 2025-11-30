using YukkuriMovieMaker.Plugin;
using MIDI.AudioEffect.EQUALIZER.Views;
using MIDI.AudioEffect.EQUALIZER.Interfaces;

namespace MIDI.AudioEffect.EQUALIZER
{
    public class EqualizerSettings : SettingsBase<EqualizerSettings>
    {
        private readonly IConfigService _configService;

        public override string Name => "GUIイコライザー設定";
        public override SettingsCategory Category => SettingsCategory.Voice;
        public override bool HasSettingView => true;
        public override object SettingView => new EqualizerSettingsWindow
        {
            DataContext = new ViewModels.EqualizerSettingsViewModel()
        };

        public EqualizerSettings()
        {
            _configService = ServiceLocator.ConfigService;
        }

        public bool HighQualityMode
        {
            get => _configService.HighQualityMode;
            set => _configService.HighQualityMode = value;
        }

        public double EditorHeight
        {
            get => _configService.EditorHeight;
            set => _configService.EditorHeight = value;
        }

        public string DefaultPreset
        {
            get => _configService.DefaultPreset;
            set => _configService.DefaultPreset = value;
        }

        public override void Initialize()
        {
            _configService.Load();
        }

        public override void Save()
        {
            _configService.Save();
        }
    }
}
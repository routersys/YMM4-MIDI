using MIDI.AudioEffect.EQUALIZER.Interfaces;

namespace MIDI.AudioEffect.EQUALIZER.Services
{
    public static class ServiceLocator
    {
        private static IConfigService? _configService;
        private static IPresetService? _presetService;

        public static IConfigService ConfigService => _configService ??= new ConfigService();
        public static IPresetService PresetService => _presetService ??= new PresetService();
    }
}
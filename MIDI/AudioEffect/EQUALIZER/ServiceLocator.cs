using MIDI.AudioEffect.EQUALIZER.Interfaces;
using MIDI.AudioEffect.EQUALIZER.Services;

namespace MIDI.AudioEffect.EQUALIZER
{
    public static class ServiceLocator
    {
        private static IConfigService? _configService;
        private static IPresetService? _presetService;

        public static IConfigService ConfigService => _configService ??= new ConfigService();
        public static IPresetService PresetService => _presetService ??= new PresetService();
    }
}
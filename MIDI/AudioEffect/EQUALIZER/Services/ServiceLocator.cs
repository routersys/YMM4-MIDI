using MIDI.AudioEffect.EQUALIZER.Interfaces;

namespace MIDI.AudioEffect.EQUALIZER.Services
{
    public static class ServiceLocator
    {
        private static IConfigService? _configService;
        private static IPresetService? _presetService;
        private static IGroupService? _groupService;

        public static IConfigService ConfigService => _configService ??= new ConfigService();
        public static IPresetService PresetService => _presetService ??= new PresetService();
        public static IGroupService GroupService => _groupService ??= new GroupService();
    }
}
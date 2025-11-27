using System.Reflection;
using MIDI.API.Attributes;
using MIDI.API.Context;

namespace MIDI.API.Commands
{
    [ApiCommandGroup("System")]
    public class SystemCommands
    {
        private readonly ApiContext _context;

        public SystemCommands(ApiContext context)
        {
            _context = context;
        }

        [ApiCommand("get_plugin_version")]
        public object GetPluginVersion() => new { version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() };

        [ApiCommand("clear_audio_cache")]
        public object ClearAudioCache()
        {
            MidiAudioSource.ClearCache();
            return new { success = true };
        }
    }
}
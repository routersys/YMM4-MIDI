using MIDI.Configuration.Models;

namespace MIDI.API.Context
{
    public class ApiContext
    {
        public MidiSettingsViewModel ViewModel { get; }
        public MidiConfiguration Configuration { get; }
        public MidiEditorSettings EditorSettings { get; }

        public ApiContext(MidiSettingsViewModel viewModel, MidiConfiguration configuration, MidiEditorSettings editorSettings)
        {
            ViewModel = viewModel;
            Configuration = configuration;
            EditorSettings = editorSettings;
        }
    }
}
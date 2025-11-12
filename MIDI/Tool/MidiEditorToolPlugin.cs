using System;
using System.Reflection;
using System.Windows;
using MIDI.Tool.ViewModel;
using MIDI.Tool.View;
using YukkuriMovieMaker.Plugin;
using System.Globalization;

namespace MIDI.Tool
{
    [PluginDetails(
        AuthorName = "routersys",
        ContentId = ""
    )]
    public class MidiEditorToolPlugin : IToolPlugin
    {
        public string Name => Translate.PluginToolName;

        public PluginDetailsAttribute Details =>
            GetType().GetCustomAttribute<PluginDetailsAttribute>() ?? new();

        public Type ViewModelType { get; } = typeof(MidiEditorToolViewModel);

        public Type ViewType { get; } = typeof(MidiEditorToolView);

        public void SetCulture(CultureInfo cultureInfo)
        {
            Translate.Culture = cultureInfo;
        }
    }
}
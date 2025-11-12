using YukkuriMovieMaker.Plugin.Voice;
using YukkuriMovieMaker.Controls;
using MIDI.Voice.Views;
using System.ComponentModel.DataAnnotations;

namespace MIDI.Voice
{
    public class NoteVoiceParameter : VoiceParameterBase
    {
        [Display(Name = "記法ヘルプ")]
        [NoteNotationHelp]
        public string? NotationHelp { get; set; }
    }
}
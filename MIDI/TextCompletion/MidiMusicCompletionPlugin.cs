using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using YukkuriMovieMaker.Plugin.TextCompletion;

namespace MIDI.TextCompletion
{
    public class MidiMusicCompletionPlugin : ITextCompletionPlugin
    {
        public string Name => "MIDI音楽用語補完";

        public object? SettingsView
        {
            get
            {
                var settings = MidiMusicCompletionSettings.Default;
                var viewModel = new ViewModels.MidiMusicCompletionSettingsViewModel(settings);
                return new MidiMusicCompletionSettingsView { DataContext = viewModel };
            }
        }

        public async Task<string> ProcessAsync(string systemPrompt, string text)
        {
            await Task.Delay(100);

            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var lastWord = text.Split(new[] { ' ', '\t', '\n', '\r', '。', '、', ',', '.' }, StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

            if (string.IsNullOrWhiteSpace(lastWord))
            {
                return string.Empty;
            }

            var term = TerminologyData.SearchTerm(lastWord);
            if (term != null)
            {
                if (systemPrompt.Contains("意味") || systemPrompt.Contains("definition"))
                {
                    return $"{term.JapaneseName} ({term.EnglishName}): {term.Description}";
                }
                else if (systemPrompt.Contains("英訳") || systemPrompt.Contains("translate"))
                {
                    return $"{term.JapaneseName} -> {term.EnglishName}";
                }
            }

            var completions = TerminologyData.GetCompletions(lastWord);
            if (completions.Any())
            {
                var firstCompletion = completions.First();
                if (MidiMusicCompletionSettings.Default.EnableDescriptionCompletion)
                {
                    return $"{firstCompletion.JapaneseName} ({firstCompletion.EnglishName}): {firstCompletion.Description}";
                }
                else
                {
                    return firstCompletion.JapaneseName;
                }
            }

            return $"「{lastWord}」に関する補完候補は見つかりませんでした。";
        }
    }
}
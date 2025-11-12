using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace MIDI.TextCompletion.Models
{
    public class MusicTerm : ViewModels.ViewModelBase
    {
        private string japaneseName = string.Empty;
        public string JapaneseName { get => japaneseName; set => Set(ref japaneseName, value); }

        private string englishName = string.Empty;
        public string EnglishName { get => englishName; set => Set(ref englishName, value); }

        private string description = string.Empty;
        public string Description { get => description; set => Set(ref description, value); }

        private List<string> aliases = new List<string>();
        public List<string> Aliases
        {
            get => aliases;
            set
            {
                if (Set(ref aliases, value))
                {
                    RaisePropertyChanged(nameof(AliasText));
                }
            }
        }

        public string AliasText
        {
            get => string.Join(", ", aliases);
            set
            {
                var newAliases = value.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                       .Select(s => s.Trim())
                                       .Where(s => !string.IsNullOrEmpty(s))
                                       .ToList();

                if (!aliases.SequenceEqual(newAliases))
                {
                    aliases = newAliases;
                    RaisePropertyChanged(nameof(Aliases));
                    RaisePropertyChanged(nameof(AliasText));
                }
            }
        }

        public MusicTerm Clone()
        {
            return new MusicTerm
            {
                JapaneseName = this.JapaneseName,
                EnglishName = this.EnglishName,
                Description = this.Description,
                Aliases = new List<string>(this.Aliases)
            };
        }
    }
}
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;

namespace MIDI.Tool.EMEL.Core
{
    public static class EmelSyntaxDefinition
    {
        private static bool isLoaded = false;
        private static readonly object loadLock = new object();

        public static void Register()
        {
            lock (loadLock)
            {
                if (isLoaded) return;

                IHighlightingDefinition emelDefinition = new CustomEmelHighlightingDefinition();
                HighlightingManager.Instance.RegisterHighlighting("EMEL", new[] { ".emel" }, emelDefinition);
                isLoaded = true;
            }
        }
    }

    internal class CustomEmelHighlightingDefinition : IHighlightingDefinition
    {
        public string Name { get; }
        public HighlightingRuleSet MainRuleSet { get; }
        public IEnumerable<HighlightingColor> NamedHighlightingColors { get; }
        public IDictionary<string, string> Properties { get; }

        public CustomEmelHighlightingDefinition()
        {
            Name = "EMEL";
            MainRuleSet = new HighlightingRuleSet();
            NamedHighlightingColors = new List<HighlightingColor>();
            Properties = new Dictionary<string, string>();

            var commentColor = new HighlightingColor
            {
                Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString("#FF6A9955"))
            };
            var stringColor = new HighlightingColor
            {
                Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString("#FFD69D85"))
            };
            var numberColor = new HighlightingColor
            {
                Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString("#FFB5CEA8"))
            };
            var keywordsColor = new HighlightingColor
            {
                Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString("#FF569CD6")),
                FontWeight = FontWeights.Bold
            };
            var builtInFuncsColor = new HighlightingColor
            {
                Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString("#FFDCDCAA"))
            };
            var operatorsColor = new HighlightingColor
            {
                Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString("#FFB4B4B4"))
            };
            var delimitersColor = new HighlightingColor
            {
                Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString("#FFD4D4D4"))
            };
            var variablesColor = new HighlightingColor
            {
                Foreground = new SimpleHighlightingBrush((Color)ColorConverter.ConvertFromString("#FF9CDCFE"))
            };

            MainRuleSet.Spans.Add(new HighlightingSpan
            {
                StartExpression = new Regex("//"),
                EndExpression = new Regex("$"),
                RuleSet = null,
                StartColor = commentColor,
                EndColor = commentColor
            });

            var stringRuleSet = new HighlightingRuleSet();
            stringRuleSet.Spans.Add(new HighlightingSpan
            {
                StartExpression = new Regex(@"\\"),
                EndExpression = new Regex("."),
                StartColor = operatorsColor,
                EndColor = operatorsColor
            });
            MainRuleSet.Spans.Add(new HighlightingSpan
            {
                StartExpression = new Regex("\""),
                EndExpression = new Regex("\""),
                RuleSet = stringRuleSet,
                StartColor = stringColor,
                EndColor = stringColor
            });

            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"\b(let|repeat|if|else|Track|Global|func)\b", RegexOptions.Compiled),
                Color = keywordsColor
            });

            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"\b(Note|NoteEx|Rest|CC|Program|PitchBend|ChannelPressure|Chord)\b", RegexOptions.Compiled),
                Color = builtInFuncsColor
            });

            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"\b\-?\d+(\.\d+)?\b", RegexOptions.Compiled),
                Color = numberColor
            });

            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"[\+\-\*/%=]|==|!=|>=|<=|&&|\|\||>|<", RegexOptions.Compiled),
                Color = operatorsColor
            });

            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"[\(\)\{\}\,;\[\]]", RegexOptions.Compiled),
                Color = delimitersColor
            });

            MainRuleSet.Rules.Add(new HighlightingRule
            {
                Regex = new Regex(@"[a-zA-Z_][a-zA-Z0-9_]*", RegexOptions.Compiled),
                Color = variablesColor
            });
        }

        public HighlightingRuleSet GetNamedRuleSet(string name)
        {
            return null!;
        }

        public HighlightingColor GetNamedColor(string name)
        {
            return null!;
        }
    }
}
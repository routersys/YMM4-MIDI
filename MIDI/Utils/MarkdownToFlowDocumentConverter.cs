using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace MIDI.Utils
{
    public static class MarkdownToFlowDocumentConverter
    {
        public static FlowDocument Convert(string markdown)
        {
            var document = new FlowDocument();
            document.SetResourceReference(FlowDocument.ForegroundProperty, SystemColors.ControlTextBrushKey);
            document.SetResourceReference(FlowDocument.BackgroundProperty, SystemColors.WindowBrushKey);

            var lines = markdown.Replace("\r\n", "\n").Split('\n');
            var listStack = new Stack<List>();
            var indentStack = new Stack<int>();

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var trimmedLine = line.TrimStart();
                var indentLevel = line.Length - trimmedLine.Length;

                if (trimmedLine.StartsWith("* ") || trimmedLine.StartsWith("- "))
                {
                    var itemText = trimmedLine.Substring(2).Trim();
                    var listItem = new ListItem(CreateParagraph(itemText));

                    while (indentStack.Any() && indentLevel < indentStack.Peek())
                    {
                        listStack.Pop();
                        indentStack.Pop();
                    }

                    if (!listStack.Any() || indentLevel > indentStack.Peek())
                    {
                        var newList = new List { MarkerStyle = TextMarkerStyle.Disc, Padding = new Thickness(20, 0, 0, 0) };
                        if (listStack.Any())
                        {
                            var parentItem = listStack.Peek().ListItems.LastOrDefault();
                            if (parentItem == null)
                            {
                                parentItem = new ListItem();
                                listStack.Peek().ListItems.Add(parentItem);
                            }
                            parentItem.Blocks.Add(newList);
                        }
                        else
                        {
                            document.Blocks.Add(newList);
                        }
                        listStack.Push(newList);
                        indentStack.Push(indentLevel);
                    }

                    listStack.Peek().ListItems.Add(listItem);
                }
                else
                {
                    listStack.Clear();
                    indentStack.Clear();

                    if (line.StartsWith("```"))
                    {
                        var codeBlockContent = new List<string>();
                        i++;
                        while (i < lines.Length && !lines[i].StartsWith("```"))
                        {
                            codeBlockContent.Add(lines[i]);
                            i++;
                        }
                        var codeParagraph = new Paragraph(new Run(string.Join("\n", codeBlockContent)))
                        {
                            FontFamily = new FontFamily("Consolas"),
                            FontSize = 12
                        };

                        var doc = new FlowDocument(codeParagraph);
                        doc.SetResourceReference(FlowDocument.ForegroundProperty, SystemColors.ControlTextBrushKey);

                        var border = new Border
                        {
                            Padding = new Thickness(10),
                            CornerRadius = new CornerRadius(3),
                            BorderThickness = new Thickness(1),
                            Child = new FlowDocumentScrollViewer
                            {
                                Document = doc,
                                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
                            }
                        };
                        border.SetResourceReference(Border.BackgroundProperty, SystemColors.ControlLightBrushKey);
                        border.SetResourceReference(Border.BorderBrushProperty, SystemColors.ControlDarkBrushKey);
                        document.Blocks.Add(new BlockUIContainer(border));
                    }
                    else if (line.StartsWith("###"))
                    {
                        document.Blocks.Add(CreateParagraph(line.Substring(3).Trim(), 16, FontWeights.Bold));
                    }
                    else if (line.StartsWith("##"))
                    {
                        document.Blocks.Add(CreateParagraph(line.Substring(2).Trim(), 18, FontWeights.Bold));
                    }
                    else if (line.StartsWith("#"))
                    {
                        document.Blocks.Add(CreateParagraph(line.Substring(1).Trim(), 22, FontWeights.Bold));
                    }
                    else if (!string.IsNullOrWhiteSpace(line))
                    {
                        document.Blocks.Add(CreateParagraph(line));
                    }
                }
            }

            return document;
        }

        private static Paragraph CreateParagraph(string text, double fontSize = 14, FontWeight? fontWeight = null)
        {
            var paragraph = new Paragraph();

            var regex = new Regex(@"(\*\*(.*?)\*\*|`(.*?)`)");
            var matches = regex.Matches(text);
            int lastIndex = 0;

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    paragraph.Inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));
                }

                if (match.Value.StartsWith("**"))
                {
                    paragraph.Inlines.Add(new Run(match.Groups[2].Value) { FontWeight = FontWeights.Bold });
                }
                else if (match.Value.StartsWith("`"))
                {
                    var textBlock = new TextBlock(new Run(match.Groups[3].Value))
                    {
                        FontFamily = new FontFamily("Consolas")
                    };

                    var border = new Border
                    {
                        Padding = new Thickness(4, 1, 4, 1),
                        CornerRadius = new CornerRadius(3),
                        BorderThickness = new Thickness(1),
                        Child = textBlock
                    };
                    border.SetResourceReference(Border.BorderBrushProperty, SystemColors.ControlDarkBrushKey);

                    paragraph.Inlines.Add(new InlineUIContainer(border)
                    {
                        BaselineAlignment = BaselineAlignment.TextBottom
                    });
                }
                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                paragraph.Inlines.Add(new Run(text.Substring(lastIndex)));
            }

            paragraph.FontSize = fontSize;
            if (fontWeight.HasValue)
            {
                paragraph.FontWeight = fontWeight.Value;
            }

            return paragraph;
        }
    }
}
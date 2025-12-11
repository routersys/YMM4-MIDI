using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MIDI.Utils
{
    public static class MarkdownToFlowDocumentConverter
    {
        public static FlowDocument Convert(string markdown)
        {
            var document = new FlowDocument();
            document.SetResourceReference(FlowDocument.ForegroundProperty, SystemColors.ControlTextBrushKey);
            document.SetResourceReference(FlowDocument.BackgroundProperty, SystemColors.WindowBrushKey);
            document.PagePadding = new Thickness(10);

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
                    var listItem = new ListItem();
                    AddContentToBlockCollection(listItem.Blocks, itemText);

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
                        var p = new Paragraph();
                        AddContentToInlineCollection(p.Inlines, line.Substring(3).Trim());
                        p.FontSize = 16;
                        p.FontWeight = FontWeights.Bold;
                        document.Blocks.Add(p);
                    }
                    else if (line.StartsWith("##"))
                    {
                        var p = new Paragraph();
                        AddContentToInlineCollection(p.Inlines, line.Substring(2).Trim());
                        p.FontSize = 18;
                        p.FontWeight = FontWeights.Bold;
                        document.Blocks.Add(p);
                    }
                    else if (line.StartsWith("#"))
                    {
                        var p = new Paragraph();
                        AddContentToInlineCollection(p.Inlines, line.Substring(1).Trim());
                        p.FontSize = 22;
                        p.FontWeight = FontWeights.Bold;
                        document.Blocks.Add(p);
                    }
                    else
                    {
                        if (trimmedLine.StartsWith("<img") && trimmedLine.EndsWith("/>"))
                        {
                            var match = Regex.Match(trimmedLine, "src=\"([^\"]+)\"");
                            if (match.Success)
                            {
                                var src = match.Groups[1].Value;
                                try
                                {
                                    var image = new Image
                                    {
                                        Source = new BitmapImage(new Uri(src)),
                                        Stretch = Stretch.Uniform,
                                        MaxWidth = 600
                                    };
                                    var widthMatch = Regex.Match(trimmedLine, "width=\"([^\"]+)\"");
                                    if (widthMatch.Success && double.TryParse(widthMatch.Groups[1].Value, out double w)) image.Width = w;

                                    var heightMatch = Regex.Match(trimmedLine, "height=\"([^\"]+)\"");
                                    if (heightMatch.Success && double.TryParse(heightMatch.Groups[1].Value, out double h)) image.Height = h;

                                    document.Blocks.Add(new BlockUIContainer(image));
                                }
                                catch { }
                            }
                        }
                        else if (!string.IsNullOrWhiteSpace(line))
                        {
                            var p = new Paragraph();
                            AddContentToInlineCollection(p.Inlines, line);
                            document.Blocks.Add(p);
                        }
                    }
                }
            }

            return document;
        }

        private static void AddContentToBlockCollection(BlockCollection blocks, string text)
        {
            var p = new Paragraph();
            AddContentToInlineCollection(p.Inlines, text);
            blocks.Add(p);
        }

        private static void AddContentToInlineCollection(InlineCollection inlines, string text)
        {
            var regex = new Regex(@"(\*\*(.*?)\*\*|`(.*?)`|\[(.*?)\]\((.*?)\)|<img[^>]*src=""([^""]*)""[^>]*/>)");
            var matches = regex.Matches(text);
            int lastIndex = 0;

            foreach (Match match in matches)
            {
                if (match.Index > lastIndex)
                {
                    inlines.Add(new Run(text.Substring(lastIndex, match.Index - lastIndex)));
                }

                if (match.Value.StartsWith("**"))
                {
                    inlines.Add(new Run(match.Groups[2].Value) { FontWeight = FontWeights.Bold });
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

                    inlines.Add(new InlineUIContainer(border)
                    {
                        BaselineAlignment = BaselineAlignment.TextBottom
                    });
                }
                else if (match.Value.StartsWith("["))
                {
                    var linkText = match.Groups[4].Value;
                    var linkUrl = match.Groups[5].Value;
                    try
                    {
                        var hyperlink = new Hyperlink(new Run(linkText))
                        {
                            NavigateUri = new Uri(linkUrl)
                        };
                        inlines.Add(hyperlink);
                    }
                    catch
                    {
                        inlines.Add(new Run(linkText));
                    }
                }
                else if (match.Value.StartsWith("<img"))
                {
                    var src = match.Groups[6].Value;
                    try
                    {
                        var image = new Image
                        {
                            Source = new BitmapImage(new Uri(src)),
                            Stretch = Stretch.Uniform,
                            MaxWidth = 600
                        };
                        inlines.Add(new InlineUIContainer(image));
                    }
                    catch { }
                }
                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < text.Length)
            {
                inlines.Add(new Run(text.Substring(lastIndex)));
            }
        }
    }
}
using System;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using System.Windows.Controls;
using System.Windows.Data;

namespace MIDI.Tool.EMEL.Core
{
    public class EmelCompletionData : ICSharpCode.AvalonEdit.CodeCompletion.ICompletionData
    {
        public EmelCompletionData(string text, string description = "")
        {
            Text = text;
            Description = description;
        }

        public ImageSource? Image => null;

        public string Text { get; }

        public object Content
        {
            get
            {
                var sp = new StackPanel { Orientation = Orientation.Horizontal };

                var textBlock = new TextBlock
                {
                    Text = Text,
                    FontWeight = System.Windows.FontWeights.Bold,
                    Foreground = System.Windows.Media.Brushes.White
                };

                var descBlock = new TextBlock
                {
                    Text = $" ({Description})",
                    Margin = new System.Windows.Thickness(5, 0, 0, 0),
                    Foreground = System.Windows.Media.Brushes.Gray
                };

                sp.Children.Add(textBlock);
                sp.Children.Add(descBlock);

                return sp;
            }
        }

        public object Description { get; }

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            textArea.Document.Replace(completionSegment, Text);
        }
    }
}
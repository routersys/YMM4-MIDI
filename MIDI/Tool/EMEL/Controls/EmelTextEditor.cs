using System.Linq;
using System.Windows.Input;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using MIDI.Tool.EMEL.Core;
using MIDI.Tool.EMEL.ViewModel;
using System.Windows;
using ICSharpCode.AvalonEdit.Document;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Rendering;
using System.Threading;
using System;

namespace MIDI.Tool.EMEL.Controls
{
    public class EmelTextEditor : TextEditor
    {
        private CompletionWindow? _completionWindow;
        private EmelCompletionProvider? _completionProvider;
        private Timer? _typingTimer;
        private readonly ErrorBackgroundRenderer _errorRenderer;

        public EmelEditorViewModel? ViewModel
        {
            get => DataContext as EmelEditorViewModel;
            set => DataContext = value;
        }

        public EmelTextEditor()
        {
            EmelSyntaxDefinition.Register();
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("EMEL");

            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E1E1E"));
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#D4D4D4"));

            Options.EnableRectangularSelection = true;
            Options.EnableTextDragDrop = true;
            Options.ConvertTabsToSpaces = true;
            Options.IndentationSize = 4;
            ShowLineNumbers = true;
            FontFamily = new System.Windows.Media.FontFamily("Consolas");
            FontSize = 14;

            TextArea.TextEntering += OnTextEntering;
            TextArea.TextEntered += OnTextEntered;
            TextArea.PreviewKeyDown += OnPreviewKeyDown;
            TextArea.Caret.PositionChanged += OnCaretPositionChanged;
            Document.TextChanged += OnDocumentTextChanged;

            _errorRenderer = new ErrorBackgroundRenderer(this);
            TextArea.TextView.BackgroundRenderers.Add(_errorRenderer);

            DataContextChanged += EmelTextEditor_DataContextChanged;
        }

        private void OnDocumentTextChanged(object? sender, EventArgs e)
        {
            _typingTimer?.Dispose();
            _typingTimer = new Timer(
                _ => Dispatcher.Invoke(() => ViewModel?.DocumentTextChangedCommand.Execute(null)),
                null,
                500,
                Timeout.Infinite);
        }

        private void OnCaretPositionChanged(object? sender, EventArgs e)
        {
            if (ViewModel?.CurrentErrorLine > 0)
            {
                int maxLine = Document.LineCount;
                int errorLineNumber = ViewModel.CurrentErrorLine;

                if (errorLineNumber > maxLine)
                {
                    ToolTip = null;
                    return;
                }

                var targetErrorLine = Document.GetLineByNumber(errorLineNumber);
                if (CaretOffset >= targetErrorLine.Offset && CaretOffset <= targetErrorLine.EndOffset)
                {
                    ToolTip = ViewModel.ErrorMessage;
                }
                else
                {
                    ToolTip = null;
                }
            }
            else
            {
                ToolTip = null;
            }
        }

        private void EmelTextEditor_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ViewModel != null)
            {
                _completionProvider = new EmelCompletionProvider(ViewModel);
                Document = ViewModel.Document;
                _errorRenderer.SetViewModel(ViewModel);
            }
            else
            {
                _completionProvider = null;
                _errorRenderer.SetViewModel(null);
            }
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None)
            {
                if (_completionWindow != null)
                {
                    _completionWindow.CompletionList.RequestInsertion(e);
                    e.Handled = true;
                    return;
                }

                if (TextArea.Selection.IsEmpty)
                {
                    var indent = new string(' ', Options.IndentationSize);
                    TextArea.Document.Insert(CaretOffset, indent);
                }
                else
                {
                    int startLine = TextArea.Document.GetLineByOffset(TextArea.Selection.SurroundingSegment.Offset).LineNumber;
                    int endLine = TextArea.Document.GetLineByOffset(TextArea.Selection.SurroundingSegment.EndOffset).LineNumber;

                    TextArea.Document.BeginUpdate();
                    for (int i = startLine; i <= endLine; i++)
                    {
                        var line = TextArea.Document.GetLineByNumber(i);
                        if (line.Length > 0)
                        {
                            TextArea.Document.Insert(line.Offset, new string(' ', Options.IndentationSize));
                        }
                    }
                    TextArea.Document.EndUpdate();
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && _completionWindow != null)
            {
                _completionWindow.CompletionList.RequestInsertion(e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape && _completionWindow != null)
            {
                _completionWindow.Close();
                e.Handled = true;
            }
        }

        private void OnTextEntered(object sender, TextCompositionEventArgs e)
        {
            if (_completionProvider == null || e.Text == null || e.Text.Length == 0) return;

            char enteredChar = e.Text[0];

            if (char.IsLetter(enteredChar) || enteredChar == '_' || (char.IsDigit(enteredChar) && _completionWindow != null) || enteredChar == '.')
            {
                if (_completionWindow == null)
                {
                    var completions = _completionProvider.GetCompletions(Document.Text, CaretOffset);
                    if (!completions.Any()) return;

                    int wordStartOffset = GetWordStartOffset(CaretOffset);

                    _completionWindow = new CompletionWindow(TextArea);
                    _completionWindow.StartOffset = wordStartOffset;
                    _completionWindow.CloseWhenCaretAtBeginning = true;
                    _completionWindow.MaxHeight = 300;
                    _completionWindow.Width = 400;

                    var data = _completionWindow.CompletionList.CompletionData;
                    foreach (var completion in completions)
                    {
                        data.Add(completion);
                    }

                    _completionWindow.Show();
                    _completionWindow.Closed += (o, args) => _completionWindow = null;
                }
            }
        }

        private int GetWordStartOffset(int offset)
        {
            int wordStart = offset;
            while (wordStart > 0)
            {
                char c = Document.GetCharAt(wordStart - 1);
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    break;
                }
                wordStart--;
            }
            return wordStart;
        }

        private void OnTextEntering(object sender, TextCompositionEventArgs e)
        {
            if (e.Text.Length > 0 && _completionWindow != null)
            {
                if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_' && e.Text[0] != '.')
                {
                    _completionWindow.CompletionList.RequestInsertion(e);
                }
            }
        }

        private class ErrorBackgroundRenderer : IBackgroundRenderer
        {
            private EmelTextEditor _editor;
            private EmelEditorViewModel? _viewModel;
            private Pen _errorPen;
            private SolidColorBrush _errorBrush;

            public ErrorBackgroundRenderer(EmelTextEditor editor)
            {
                _editor = editor;
                _errorBrush = new SolidColorBrush(Color.FromRgb(0xDC, 0x32, 0x32));
                _errorBrush.Freeze();
                _errorPen = new Pen(_errorBrush, 1);
                _errorPen.Freeze();
            }

            public void SetViewModel(EmelEditorViewModel? viewModel)
            {
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
                }
                _viewModel = viewModel;
                if (_viewModel != null)
                {
                    _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                }
                _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
            }

            private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
            {
                if (e.PropertyName == nameof(EmelEditorViewModel.CurrentErrorLine))
                {
                    _editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
                }
            }

            public KnownLayer Layer => KnownLayer.Background;

            public void Draw(TextView textView, DrawingContext drawingContext)
            {
                if (_viewModel == null || _viewModel.CurrentErrorLine <= 0) return;

                var document = _editor.Document;
                if (document == null) return;

                DocumentLine line;
                try
                {
                    if (_viewModel.CurrentErrorLine > document.LineCount) return;

                    line = document.GetLineByNumber(_viewModel.CurrentErrorLine);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return;
                }

                foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, line))
                {
                    double y = rect.Bottom - 1.0;

                    double x = rect.X;
                    double width = rect.Width;

                    if (width > 0)
                    {
                        var geometry = new StreamGeometry();
                        using (var ctx = geometry.Open())
                        {
                            ctx.BeginFigure(new Point(x, y), false, false);
                            double segmentWidth = 4;
                            int segments = (int)(width / segmentWidth);
                            for (int i = 0; i < segments; i++)
                            {
                                double startX = x + i * segmentWidth;
                                double endX = startX + segmentWidth;
                                double middleX = startX + segmentWidth / 2.0;

                                Point p1 = new Point(startX, y);
                                Point p2 = new Point(middleX, y + 2.0);
                                Point p3 = new Point(endX, y);

                                if (i == 0)
                                {
                                    ctx.LineTo(p2, true, false);
                                }
                                else
                                {
                                    ctx.LineTo(p1, true, false);
                                    ctx.LineTo(p2, true, false);
                                }
                                ctx.LineTo(p3, true, false);
                            }
                        }
                        geometry.Freeze();
                        drawingContext.DrawGeometry(null, _errorPen, geometry);
                    }
                }
            }
        }
    }
}
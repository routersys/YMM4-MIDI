using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using YukkuriMovieMaker.Commons;
using MIDI.AudioEffect.EQUALIZER.Models;
using MIDI.AudioEffect.EQUALIZER.ViewModels;

namespace MIDI.AudioEffect.EQUALIZER.Views
{
    public partial class EqualizerControl : UserControl, IPropertyEditorControl
    {
        public static readonly DependencyProperty ItemsSourceProperty =
            DependencyProperty.Register(nameof(ItemsSource), typeof(ObservableCollection<EQBand>), typeof(EqualizerControl), new PropertyMetadata(null, OnItemsSourceChanged));
        public ObservableCollection<EQBand> ItemsSource
        {
            get => (ObservableCollection<EQBand>)GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        public event EventHandler? BeginEdit;
        public event EventHandler? EndEdit;

        private EqualizerEditorViewModel ViewModel => (EqualizerEditorViewModel)DataContext;

        private Point lastRightClickPosition;
        private double minFreq = 20, maxFreq = 20000;
        private double minGain = -24, maxGain = 24;
        private bool isDragging = false;

        private AnimationValue? _targetFreqKeyframe;
        private AnimationValue? _targetGainKeyframe;

        private SolidColorBrush gridLineBrush = new(Color.FromArgb(50, 255, 255, 255));
        private SolidColorBrush gridTextBrush = new(Color.FromArgb(100, 255, 255, 255));
        private SolidColorBrush thumbFillBrush = new(Color.FromRgb(0, 180, 255));
        private SolidColorBrush thumbSelectedFillBrush = new(Color.FromRgb(255, 215, 0));
        private SolidColorBrush thumbStrokeBrush = new(Colors.White);
        private SolidColorBrush curveBrush = new(Color.FromRgb(0, 200, 255));
        private SolidColorBrush curveFillBrush = new(Color.FromArgb(30, 0, 200, 255));
        private SolidColorBrush timelineBrush = new(Color.FromArgb(150, 255, 50, 50));
        private Path? eqCurvePath;
        private Path? eqCurveFillPath;

        public EqualizerControl()
        {
            InitializeComponent();
            DataContext = new EqualizerEditorViewModel();

            ViewModel.RequestRedraw += (s, e) => DrawAll();
            ViewModel.BeginEdit += (s, e) => BeginEdit?.Invoke(this, EventArgs.Empty);
            ViewModel.EndEdit += (s, e) => EndEdit?.Invoke(this, EventArgs.Empty);

            PresetToggleButton.Unchecked += async (s, e) =>
            {
                PresetToggleButton.IsHitTestVisible = false;
                await Task.Delay(200);
                PresetToggleButton.IsHitTestVisible = true;
            };

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var dp = DependencyPropertyDescriptor.FromProperty(Border.BackgroundProperty, typeof(Border));
            dp?.AddValueChanged(CanvasBorder, OnBackgroundChanged);
            UpdateTheme();
            DrawAll();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            var dp = DependencyPropertyDescriptor.FromProperty(Border.BackgroundProperty, typeof(Border));
            dp?.RemoveValueChanged(CanvasBorder, OnBackgroundChanged);
        }

        private void OnBackgroundChanged(object? sender, EventArgs e)
        {
            UpdateTheme();
            DrawAll();
        }

        private void UpdateTheme()
        {
            if (CanvasBorder.Background is SolidColorBrush bg)
            {
                var c = bg.Color;
                var brightness = (c.R * 0.299 + c.G * 0.587 + c.B * 0.114) / 255;
                bool isDark = brightness < 0.5;

                if (isDark)
                {
                    gridLineBrush = new SolidColorBrush(Color.FromArgb(50, 255, 255, 255));
                    gridTextBrush = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255));
                    thumbFillBrush = new SolidColorBrush(Color.FromRgb(0, 180, 255));
                    thumbStrokeBrush = new SolidColorBrush(Colors.White);
                    curveBrush = new SolidColorBrush(Color.FromRgb(0, 200, 255));
                    curveFillBrush = new SolidColorBrush(Color.FromArgb(30, 0, 200, 255));
                }
                else
                {
                    gridLineBrush = new SolidColorBrush(Color.FromArgb(50, 0, 0, 0));
                    gridTextBrush = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
                    thumbFillBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    thumbStrokeBrush = new SolidColorBrush(Colors.Black);
                    curveBrush = new SolidColorBrush(Color.FromRgb(0, 100, 200));
                    curveFillBrush = new SolidColorBrush(Color.FromArgb(30, 0, 100, 200));
                }

                gridLineBrush.Freeze();
                gridTextBrush.Freeze();
                thumbFillBrush.Freeze();
                thumbStrokeBrush.Freeze();
                curveBrush.Freeze();
                curveFillBrush.Freeze();
            }
        }

        private static void OnItemsSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (EqualizerControl)d;
            if (e.OldValue is ObservableCollection<EQBand> oldSource)
            {
                oldSource.CollectionChanged -= control.ItemsSource_CollectionChanged;
                foreach (var band in oldSource) band.PropertyChanged -= control.Band_PropertyChanged;
            }

            var newSource = e.NewValue as ObservableCollection<EQBand>;
            if (newSource != null)
            {
                newSource.CollectionChanged += control.ItemsSource_CollectionChanged;
                foreach (var band in newSource) band.PropertyChanged += control.Band_PropertyChanged;
            }
            control.ViewModel.Bands = newSource;
            control.UpdateDefaultSelection();
            control.UpdateTimeSliderRange();
            control.DrawAll();
        }

        private void UpdateDefaultSelection()
        {
            if (ViewModel.SelectedBand is null && ItemsSource?.Any() == true)
            {
                ViewModel.SelectedBand = ItemsSource.OrderBy(b => b.Frequency.Values.FirstOrDefault()?.Value ?? 0).FirstOrDefault();
            }
        }

        private void ItemsSource_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
                {
                    item.PropertyChanged -= Band_PropertyChanged;
                }
            }
            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
                {
                    item.PropertyChanged += Band_PropertyChanged;
                }
            }
            UpdateBandHeaders();
            UpdateDefaultSelection();
            UpdateTimeSliderRange();
            DrawAll();
        }

        private void Band_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (isDragging) return;

            if (sender is EQBand band && band == ViewModel.SelectedBand)
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    var currentSelection = ViewModel.SelectedBand;
                    if (currentSelection == band)
                    {
                        ViewModel.SelectedBand = null;
                        ViewModel.SelectedBand = currentSelection;
                    }
                }));
            }

            if (e.PropertyName == nameof(EQBand.Frequency) || e.PropertyName == nameof(EQBand.Gain) || e.PropertyName == nameof(EQBand.Q))
            {
                UpdateTimeSliderRange();
            }

            DrawAll();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            DrawAll();
        }

        private void TimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (this.IsLoaded && !isDragging) DrawAll();
        }

        private void UpdateTimeSliderRange()
        {
            if (ItemsSource == null || !ItemsSource.Any()) return;

            int maxFrames = 0;
            foreach (var band in ItemsSource)
            {
                maxFrames = Math.Max(maxFrames, Math.Max(band.Frequency.Values.Count, Math.Max(band.Gain.Values.Count, band.Q.Values.Count)));
            }

            if (maxFrames > 1)
            {
                TimeSlider.Maximum = 1.0;
                TimeSlider.TickFrequency = 1.0 / (maxFrames - 1);
            }
            else
            {
                TimeSlider.Maximum = 1.0;
                TimeSlider.TickFrequency = 0.1;
            }
        }

        private void UpdateBandHeaders()
        {
            if (ItemsSource is null) return;
            var sortedBands = ItemsSource.OrderBy(b => b.Frequency.GetValue(0, 1, 60)).ToList();
            for (int i = 0; i < sortedBands.Count; i++)
            {
                sortedBands[i].Header = $"バンド {i + 1}";
            }
        }

        private void DrawAll()
        {
            if (ItemsSource is null || MainCanvas.ActualWidth <= 0 || MainCanvas.ActualHeight <= 0) return;
            MainCanvas.Children.Clear();
            maxGain = ViewModel.Zoom;
            minGain = -ViewModel.Zoom;
            DrawGrid();
            DrawCurve();
            DrawThumbs();
            DrawTimeline();
        }

        private void DrawGrid()
        {
            var freqLabels = new[] { 50, 100, 200, 500, 1000, 2000, 5000, 10000 };
            foreach (var freq in freqLabels)
            {
                double x = FreqToX(freq);
                MainCanvas.Children.Add(new Line { X1 = x, Y1 = 0, X2 = x, Y2 = MainCanvas.ActualHeight, Stroke = gridLineBrush, StrokeThickness = 1 });
                var text = new TextBlock { Text = freq < 1000 ? $"{freq}" : $"{freq / 1000}k", Foreground = gridTextBrush, FontSize = 9, Margin = new Thickness(x + 2, -2, 0, 0) };
                MainCanvas.Children.Add(text);
            }

            int numGainLines = (int)(maxGain / 6);
            for (int i = -numGainLines; i <= numGainLines; i++)
            {
                if (i == 0) continue;
                double gain = i * 6;
                if (Math.Abs(gain) > maxGain) continue;
                double y = GainToY(gain);
                MainCanvas.Children.Add(new Line { X1 = 0, Y1 = y, X2 = MainCanvas.ActualWidth, Y2 = y, Stroke = gridLineBrush, StrokeThickness = 1 });
                var text = new TextBlock { Text = $"{gain:F0}", Foreground = gridTextBrush, FontSize = 9, Margin = new Thickness(2, y, 0, 0) };
                MainCanvas.Children.Add(text);
            }
            double zeroY = GainToY(0);
            MainCanvas.Children.Add(new Line { X1 = 0, Y1 = zeroY, X2 = MainCanvas.ActualWidth, Y2 = zeroY, Stroke = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)), StrokeThickness = 1 });
            MainCanvas.Children.Add(new TextBlock { Text = "0dB", Foreground = gridTextBrush, FontSize = 9, Margin = new Thickness(2, zeroY - 12, 0, 0) });
        }

        private void DrawTimeline()
        {
            double x = MainCanvas.ActualWidth * ViewModel.CurrentTime;
            var line = new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = MainCanvas.ActualHeight,
                Stroke = timelineBrush,
                StrokeThickness = 1,
                IsHitTestVisible = false
            };
            Panel.SetZIndex(line, 100);
            MainCanvas.Children.Add(line);
        }

        private void DrawCurve()
        {
            if (ItemsSource is null) return;
            if (eqCurvePath is not null) MainCanvas.Children.Remove(eqCurvePath);
            if (eqCurveFillPath is not null) MainCanvas.Children.Remove(eqCurveFillPath);

            int maxFrames = 1;
            foreach (var band in ItemsSource)
            {
                maxFrames = Math.Max(maxFrames, Math.Max(band.Frequency.Values.Count, Math.Max(band.Gain.Values.Count, band.Q.Values.Count)));
            }

            int totalFrames = Math.Max(maxFrames, 1000);
            int currentFrame = (int)(totalFrames * ViewModel.CurrentTime);

            var activeBands = ItemsSource.Where(b => b.IsEnabled).OrderBy(b => b.Frequency.GetValue(currentFrame, totalFrames, 60)).ToList();
            PathGeometry geometry;

            if (activeBands.Count == 0)
            {
                var zeroY = GainToY(0);
                geometry = new PathGeometry(new[] { new PathFigure(new Point(0, zeroY), new[] { new LineSegment(new Point(MainCanvas.ActualWidth, zeroY), true) }, false) });
            }
            else
            {
                var points = activeBands.Select(b => new Point(
                    FreqToX(b.Frequency.GetValue(currentFrame, totalFrames, 60)),
                    GainToY(b.Gain.GetValue(currentFrame, totalFrames, 60))
                )).ToList();

                var firstBand = activeBands.First();
                double startY = firstBand.Type == MIDI.AudioEffect.EQUALIZER.Models.FilterType.LowShelf ? points.First().Y : GainToY(0);
                points.Insert(0, new Point(0, startY));

                var lastBand = activeBands.Last();
                double endY = lastBand.Type == MIDI.AudioEffect.EQUALIZER.Models.FilterType.HighShelf ? points.Last().Y : GainToY(0);
                points.Add(new Point(MainCanvas.ActualWidth, endY));

                geometry = CreateSpline(points);
            }

            eqCurvePath = new Path { Stroke = curveBrush, StrokeThickness = 2, Data = geometry };

            var fillGeometry = geometry.Clone();
            if (fillGeometry.Figures.Count > 0)
            {
                var figure = fillGeometry.Figures[0];
                figure.Segments.Add(new LineSegment(new Point(MainCanvas.ActualWidth, GainToY(minGain)), false));
                figure.Segments.Add(new LineSegment(new Point(0, GainToY(minGain)), false));
                figure.IsClosed = true;
            }
            eqCurveFillPath = new Path { Fill = curveFillBrush, Data = fillGeometry };

            Panel.SetZIndex(eqCurveFillPath, -2);
            Panel.SetZIndex(eqCurvePath, -1);
            MainCanvas.Children.Add(eqCurveFillPath);
            MainCanvas.Children.Add(eqCurvePath);
        }

        private void DrawThumbs()
        {
            if (ItemsSource == null) return;

            int maxFrames = 1;
            foreach (var band in ItemsSource)
            {
                maxFrames = Math.Max(maxFrames, Math.Max(band.Frequency.Values.Count, Math.Max(band.Gain.Values.Count, band.Q.Values.Count)));
            }

            int totalFrames = Math.Max(maxFrames, 1000);
            int currentFrame = (int)(totalFrames * ViewModel.CurrentTime);

            foreach (var band in ItemsSource)
            {
                bool isSelected = band == ViewModel.SelectedBand;
                var thumb = new Thumb { Width = 12, Height = 12, DataContext = band, Template = CreateThumbTemplate(band.IsEnabled ? (isSelected ? thumbSelectedFillBrush : thumbFillBrush) : Brushes.Gray) };
                thumb.Tag = band;

                var freq = band.Frequency.GetValue(currentFrame, totalFrames, 60);
                var gain = band.Gain.GetValue(currentFrame, totalFrames, 60);
                var q = band.Q.GetValue(currentFrame, totalFrames, 60);
                thumb.ToolTip = new TextBlock
                {
                    Inlines =
                    {
                        new System.Windows.Documents.Run("周波数: ") { FontWeight = FontWeights.Bold },
                        new System.Windows.Documents.Run($"{freq:F0} Hz"),
                        new System.Windows.Documents.LineBreak(),
                        new System.Windows.Documents.Run("ゲイン: ") { FontWeight = FontWeights.Bold },
                        new System.Windows.Documents.Run($"{gain:F1} dB"),
                        new System.Windows.Documents.LineBreak(),
                        new System.Windows.Documents.Run("Q: ") { FontWeight = FontWeights.Bold },
                        new System.Windows.Documents.Run($"{q:F2}")
                    }
                };

                thumb.DragStarted += Thumb_DragStarted;
                thumb.DragDelta += Thumb_DragDelta;
                thumb.DragCompleted += Thumb_DragCompleted;
                thumb.MouseDoubleClick += (s, e) =>
                {
                    ViewModel.DeletePointCommand.Execute(band);
                    e.Handled = true;
                };

                Canvas.SetLeft(thumb, FreqToX(freq) - thumb.Width / 2);
                Canvas.SetTop(thumb, GainToY(gain) - thumb.Height / 2);
                Panel.SetZIndex(thumb, 1);
                MainCanvas.Children.Add(thumb);
            }
        }

        private void MainCanvas_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            lastRightClickPosition = Mouse.GetPosition(MainCanvas);
            MainCanvas.Tag = null;

            if (e.Source is FrameworkElement fe && fe.DataContext is EQBand band)
            {
                MainCanvas.Tag = band;
            }
            else
            {
                MainCanvas.Tag = new Point(XToFreq(lastRightClickPosition.X), YToGain(lastRightClickPosition.Y));
            }
        }

        private void MainCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return;

            if (e.OriginalSource == MainCanvas || e.OriginalSource is Path)
            {
                var pos = e.GetPosition(MainCanvas);

                if (e.OriginalSource is DependencyObject obj && FindVisualParent<Thumb>(obj) != null)
                {
                    return;
                }

                var point = new Point(XToFreq(pos.X), YToGain(pos.Y));
                ViewModel.AddPointCommand.Execute(point);
            }
        }

        private static T? FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindVisualParent<T>(parentObject);
        }

        private void Thumb_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is EQBand band)
            {
                ViewModel.SelectedBand = band;
                isDragging = true;

                _targetFreqKeyframe = GetTargetKeyframe(band.Frequency);
                _targetGainKeyframe = GetTargetKeyframe(band.Gain);

                double startX = FreqToX(_targetFreqKeyframe.Value);
                double startY = GainToY(_targetGainKeyframe.Value);
                Canvas.SetLeft(thumb, startX - thumb.Width / 2);
                Canvas.SetTop(thumb, startY - thumb.Height / 2);

                BeginEdit?.Invoke(this, EventArgs.Empty);
            }
        }

        private AnimationValue GetTargetKeyframe(Animation animation)
        {
            if (animation.Values.Count <= 1)
            {
                return animation.Values.First();
            }

            if (animation.Values.Count == 2)
            {
                return ViewModel.CurrentTime < 0.5 ? animation.Values.First() : animation.Values.Last();
            }
            else
            {
                int targetIndex = (int)Math.Round(ViewModel.CurrentTime * (animation.Values.Count - 1));
                targetIndex = Math.Clamp(targetIndex, 0, animation.Values.Count - 1);
                return animation.Values[targetIndex];
            }
        }

        private void Thumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (sender is Thumb thumb && thumb.DataContext is EQBand band && band.IsEnabled && _targetFreqKeyframe != null && _targetGainKeyframe != null)
            {
                double newX = Canvas.GetLeft(thumb) + e.HorizontalChange;
                double newY = Canvas.GetTop(thumb) + e.VerticalChange;

                newX = Math.Clamp(newX, 0, MainCanvas.ActualWidth - thumb.Width);
                newY = Math.Clamp(newY, 0, MainCanvas.ActualHeight - thumb.Height);

                Canvas.SetLeft(thumb, newX);
                Canvas.SetTop(thumb, newY);

                _targetFreqKeyframe.Value = XToFreq(newX + thumb.Width / 2);
                _targetGainKeyframe.Value = YToGain(newY + thumb.Height / 2);

                DrawCurve();
            }
        }

        private void Thumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            isDragging = false;
            _targetFreqKeyframe = null;
            _targetGainKeyframe = null;
            DrawAll();
            EndEdit?.Invoke(this, EventArgs.Empty);
        }

        private double FreqToX(double freq) => MainCanvas.ActualWidth * (Math.Log10(freq / minFreq) / Math.Log10(maxFreq / minFreq));
        private double XToFreq(double x) => minFreq * Math.Pow(maxFreq / minFreq, x / MainCanvas.ActualWidth);
        private double GainToY(double gain) => MainCanvas.ActualHeight * (1 - (gain - minGain) / (maxGain - minGain));
        private double YToGain(double y) => (1 - y / MainCanvas.ActualHeight) * (maxGain - minGain) + minGain;

        private ControlTemplate CreateThumbTemplate(Brush fill)
        {
            var factory = new FrameworkElementFactory(typeof(Ellipse));
            factory.SetValue(System.Windows.Shapes.Shape.FillProperty, fill);
            factory.SetValue(System.Windows.Shapes.Shape.StrokeProperty, thumbStrokeBrush);
            factory.SetValue(System.Windows.Shapes.Shape.StrokeThicknessProperty, 1.5);
            return new ControlTemplate(typeof(Thumb)) { VisualTree = factory };
        }

        public static PathGeometry CreateSpline(List<Point> points, double tension = 0.5)
        {
            if (points == null || points.Count < 2) return new PathGeometry();
            var pathFigure = new PathFigure { StartPoint = points[0] };
            var polyBezierSegment = new PolyBezierSegment();
            double controlScale = tension / 0.5 * 0.175;
            for (int i = 0; i < points.Count - 1; i++)
            {
                Point p0 = i == 0 ? points[i] : points[i - 1];
                Point p1 = points[i];
                Point p2 = points[i + 1];
                Point p3 = i == points.Count - 2 ? points[i + 1] : points[i + 2];
                double cp1X = p1.X + (p2.X - p0.X) * controlScale;
                double cp1Y = p1.Y + (p2.Y - p0.Y) * controlScale;
                double cp2X = p2.X - (p3.X - p1.X) * controlScale;
                double cp2Y = p2.Y - (p3.Y - p1.Y) * controlScale;
                polyBezierSegment.Points.Add(new Point(cp1X, cp1Y));
                polyBezierSegment.Points.Add(new Point(cp2X, cp2Y));
                polyBezierSegment.Points.Add(p2);
            }
            pathFigure.Segments.Add(polyBezierSegment);
            return new PathGeometry(new[] { pathFigure });
        }

        private void ResizeThumb_DragStarted(object sender, DragStartedEventArgs e) => ViewModel.NotifyBeginEdit();
        private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
        {
            var newHeight = EditorGrid.ActualHeight + e.VerticalChange;
            ViewModel.EditorHeight = Math.Clamp(newHeight, 150, 600);
        }
        private void ResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            ViewModel.NotifyEndEdit();
        }

        private void Band_BeginEdit(object? sender, EventArgs e) => ViewModel.NotifyBeginEdit();
        private void Band_EndEdit(object? sender, EventArgs e) => ViewModel.NotifyEndEdit();

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
            e.Handled = true;
        }

        private void PresetToggleButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (PresetToggleButton.IsChecked == true)
            {
                ViewModel.IsPopupOpen = false;
                e.Handled = true;
            }
        }
    }
}
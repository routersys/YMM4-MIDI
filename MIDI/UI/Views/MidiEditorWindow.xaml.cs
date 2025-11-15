using AvalonDock.Layout;
using AvalonDock.Layout.Serialization;
using Microsoft.Win32;
using MIDI.Configuration.Models;
using MIDI.UI.Core;
using MIDI.UI.ViewModels;
using MIDI.UI.ViewModels.MidiEditor;
using MIDI.UI.Views.MidiEditor.Modals;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Path = System.IO.Path;

namespace MIDI.UI.Views
{
    public partial class MidiEditorWindow : Window
    {
        private readonly MidiEditorViewModel _viewModel;
        private readonly DispatcherTimer _autoScrollTimer;
        private int _autoScrollDirection = 0;
        private bool _isThumbnailDragging = false;
        private readonly Dictionary<string, LayoutAnchorable> _layoutPanes = new Dictionary<string, LayoutAnchorable>();
        private Point _thumbnailDragStartPoint;
        private double _thumbnailDragStartOffset;

        private static string AbnormalShutdownFlagPath => Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", "backup", "editor", ".abnormal_shutdown");


        public MidiEditorWindow(string? filePath = null)
        {
            InitializeComponent();
            LoadResources();
            _viewModel = new MidiEditorViewModel(filePath);
            DataContext = _viewModel;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            Loaded += MidiEditorWindow_Loaded;
            Closing += MidiEditorWindow_Closing;

            _autoScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(30)
            };
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;
        }

        private void AutoScrollTimer_Tick(object? sender, EventArgs e)
        {
            if (_autoScrollDirection != 0 && DataContext is MidiEditorViewModel vm)
            {
                var timeStep = _viewModel.MidiFile != null ?
                    MidiProcessor.TicksToTimeSpan(100, _viewModel.MidiFile.DeltaTicksPerQuarterNote, MidiProcessor.ExtractTempoMap(_viewModel.MidiFile, MidiConfiguration.Default))
                    : TimeSpan.FromMilliseconds(50);

                var newTime = vm.CurrentTime + TimeSpan.FromSeconds(timeStep.TotalSeconds * _autoScrollDirection);

                if (newTime < TimeSpan.Zero)
                {
                    newTime = TimeSpan.Zero;
                }
                vm.CurrentTime = newTime;
            }
        }

        private void Pane_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LayoutAnchorable.IsVisible) && sender is LayoutAnchorable pane)
            {
                var propName = _layoutPanes.FirstOrDefault(kvp => kvp.Value == pane).Key;
                if (propName != null)
                {
                    var vmProp = _viewModel.GetType().GetProperty($"Is{propName}Visible");
                    if (vmProp != null && (bool)vmProp.GetValue(_viewModel)! != pane.IsVisible)
                    {
                        vmProp.SetValue(_viewModel, pane.IsVisible);
                    }
                }
            }
        }

        private void MidiEditorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel.CheckForBackup();

            try
            {
                var directory = Path.GetDirectoryName(AbnormalShutdownFlagPath);
                if (directory != null)
                {
                    Directory.CreateDirectory(directory);
                    File.WriteAllText(AbnormalShutdownFlagPath, "running");
                }
            }
            catch (Exception)
            {
            }

            ThumbnailPopup.PlacementTarget = ThumbnailCanvas;
            _viewModel.NotesLoaded += OnNotesLoaded;

            if (_viewModel.IsMidiFileLoaded)
            {
                CenterViewOnNotesIfReady();
            }

            try
            {
                if (!string.IsNullOrEmpty(MidiEditorSettings.Default.LayoutXml))
                {
                    var serializer = new XmlLayoutSerializer(DockingManager);
                    using (var reader = new StringReader(MidiEditorSettings.Default.LayoutXml))
                    {
                        serializer.Deserialize(reader);
                    }
                }
            }
            catch (Exception)
            {
            }

            LayoutInitializer.EnsurePanelsExist(DockingManager);

            Dispatcher.BeginInvoke(new Action(InitializeLayoutPanes), DispatcherPriority.ContextIdle);
        }

        private void InitializeLayoutPanes()
        {
            _layoutPanes.Clear();
            var anchorables = DockingManager.Layout.Descendents().OfType<LayoutAnchorable>().ToList();
            var viewModelType = typeof(MidiEditorViewModel);

            var visibilityProperties = viewModelType.GetProperties()
                .Where(p => p.Name.StartsWith("Is") && p.Name.EndsWith("Visible") && p.PropertyType == typeof(bool));

            foreach (var prop in visibilityProperties)
            {
                string baseName = prop.Name.Substring(2, prop.Name.Length - "Is".Length - "Visible".Length);
                string contentId = char.ToLowerInvariant(baseName[0]) + baseName.Substring(1);

                var pane = anchorables.FirstOrDefault(a => a.ContentId == contentId);
                if (pane != null)
                {
                    _layoutPanes[baseName] = pane;
                    pane.PropertyChanged += Pane_PropertyChanged;

                    if ((bool)prop.GetValue(_viewModel)! != pane.IsVisible)
                    {
                        prop.SetValue(_viewModel, pane.IsVisible);
                    }
                }
            }
        }

        private void OnNotesLoaded()
        {
            CenterViewOnNotesIfReady();
        }

        private void CenterViewOnNotesIfReady()
        {
            if (PianoRollScrollViewer.IsLoaded)
            {
                Dispatcher.BeginInvoke(new Action(CenterViewOnNotes), DispatcherPriority.ContextIdle);
            }
            else
            {
                PianoRollScrollViewer.Loaded += PianoRollScrollViewer_OnLoaded_ForCentering;
            }
        }

        private void PianoRollScrollViewer_OnLoaded_ForCentering(object sender, RoutedEventArgs e)
        {
            PianoRollScrollViewer.Loaded -= PianoRollScrollViewer_OnLoaded_ForCentering;
            CenterViewOnNotes();
        }

        private void CenterViewOnNotes()
        {
            if (!_viewModel.AllNotes.Any()) return;

            var minNote = _viewModel.AllNotes.Min(n => n.NoteNumber);
            var maxNote = _viewModel.AllNotes.Max(n => n.NoteNumber);

            var yForMaxNote = (_viewModel.MaxNoteNumber - maxNote - 1) * 20.0 * _viewModel.VerticalZoom / _viewModel.KeyYScale;
            var yForMinNote = (_viewModel.MaxNoteNumber - minNote - 1) * 20.0 * _viewModel.VerticalZoom / _viewModel.KeyYScale;

            var noteContentHeight = yForMinNote - yForMaxNote + (20.0 * _viewModel.VerticalZoom / _viewModel.KeyYScale);

            var middleY = yForMaxNote + noteContentHeight / 2;

            var viewportHeight = PianoRollScrollViewer.ActualHeight;
            if (viewportHeight <= 0) return;

            var targetOffset = middleY - (viewportHeight / 2);

            targetOffset = Math.Max(0, targetOffset);
            targetOffset = Math.Min(PianoRollScrollViewer.ScrollableHeight, targetOffset);

            PianoRollScrollViewer.ScrollToVerticalOffset(targetOffset);
        }


        private void LoadResources()
        {
            try
            {
                var appResources = Application.Current.Resources;
                foreach (var dictionary in appResources.MergedDictionaries)
                {
                    this.Resources.MergedDictionaries.Add(dictionary);
                }
            }
            catch (Exception)
            {
            }
        }

        private void MidiEditorWindow_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.OriginalSource is TextBox) return;

            TimeSpan moveStep;

            if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
            {
                moveStep = TimeSpan.FromSeconds(1);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                moveStep = TimeSpan.FromMilliseconds(10);
            }
            else if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                moveStep = TimeSpan.FromMilliseconds(100);
            }
            else
            {
                moveStep = TimeSpan.FromMilliseconds(1);
            }

            switch (e.Key)
            {
                case Key.Right:
                    if (_viewModel.IsPlaying) _viewModel.PlayPauseCommand.Execute(null);
                    _viewModel.SetCurrentTimeFromArrowKey(_viewModel.CurrentTime + moveStep);
                    e.Handled = true;
                    break;
                case Key.Left:
                    if (_viewModel.IsPlaying) _viewModel.PlayPauseCommand.Execute(null);
                    _viewModel.SetCurrentTimeFromArrowKey(_viewModel.CurrentTime > moveStep ? _viewModel.CurrentTime - moveStep : TimeSpan.Zero);
                    e.Handled = true;
                    break;
                case Key.Up:
                    PianoRollScrollViewer.ScrollToVerticalOffset(PianoRollScrollViewer.VerticalOffset - 20);
                    e.Handled = true;
                    break;
                case Key.Down:
                    PianoRollScrollViewer.ScrollToVerticalOffset(PianoRollScrollViewer.VerticalOffset + 20);
                    e.Handled = true;
                    break;
                case Key.Delete:
                    if (_viewModel.SelectedNotes.Any())
                    {
                        _viewModel.DeleteSelectedNotesCommand.Execute(null);
                    }
                    else if (_viewModel.SelectedFlags.Any())
                    {
                        _viewModel.DeleteSelectedFlagsCommand.Execute(null);
                    }
                    e.Handled = true;
                    break;
                default:
                    _viewModel.HandleKeyDown(e.Key);
                    break;
            }
        }

        private void MidiEditorWindow_KeyUp(object sender, KeyEventArgs e)
        {
            _viewModel.HandleKeyUp(e.Key);
        }

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MidiEditorViewModel.PlaybackCursorPosition))
            {
                if (DataContext is MidiEditorViewModel vm)
                {
                    double cursorPosition = vm.PlaybackCursorPosition;
                    double viewportWidth = PianoRollScrollViewer.ActualWidth;
                    double horizontalOffset = PianoRollScrollViewer.HorizontalOffset;

                    if (cursorPosition < horizontalOffset || cursorPosition > horizontalOffset + viewportWidth)
                    {
                        PianoRollScrollViewer.ScrollToHorizontalOffset(cursorPosition - viewportWidth / 2);
                    }
                    UpdateThumbnail();
                }
            }
            else if (e.PropertyName == nameof(MidiEditorViewModel.ShowThumbnail))
            {
                UpdateThumbnail();
            }
            else if (e.PropertyName?.StartsWith("Is") == true && e.PropertyName.EndsWith("Visible"))
            {
                UpdateLayoutPanesVisibility();
            }
        }

        private void UpdateLayoutPanesVisibility()
        {
            if (DataContext is not MidiEditorViewModel vm) return;

            foreach (var (propName, pane) in _layoutPanes)
            {
                var vmProp = vm.GetType().GetProperty($"Is{propName}Visible");
                if (vmProp != null)
                {
                    var isVisible = (bool)vmProp.GetValue(vm)!;
                    if (isVisible && !pane.IsVisible)
                        pane.Show();
                    else if (!isVisible && pane.IsVisible)
                        pane.Hide();
                }
            }
        }

        private void MidiEditorWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (File.Exists(AbnormalShutdownFlagPath))
                {
                    File.Delete(AbnormalShutdownFlagPath);
                }
            }
            catch (Exception)
            {
            }

            var serializer = new XmlLayoutSerializer(DockingManager);
            using (var writer = new StringWriter())
            {
                serializer.Serialize(writer);
                MidiEditorSettings.Default.LayoutXml = writer.ToString();
            }
            MidiEditorSettings.Default.Save();

            if (DataContext is MidiEditorViewModel vm)
            {
                vm.PropertyChanged -= ViewModel_PropertyChanged;
                vm.NotesLoaded -= OnNotesLoaded;
                foreach (var pane in _layoutPanes.Values)
                {
                    pane.PropertyChanged -= Pane_PropertyChanged;
                }
                vm.Dispose();
            }
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void PianoRollScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (sender == PianoRollScrollViewer)
            {
                if (PianoKeysScrollViewer != null)
                    PianoKeysScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
                if (RulerScrollViewer != null)
                    RulerScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);

                if (DataContext is MidiEditorViewModel vm)
                {
                    vm.OnScrollChanged(e.HorizontalOffset, e.ViewportWidth, e.VerticalOffset, e.ViewportHeight);
                }
                UpdateThumbnail();
            }
            else if (sender == PianoKeysScrollViewer)
            {
                if (PianoRollScrollViewer != null)
                    PianoRollScrollViewer.ScrollToVerticalOffset(e.VerticalOffset);
            }
            else if (sender == RulerScrollViewer)
            {
                if (PianoRollScrollViewer != null)
                    PianoRollScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
            }
        }

        private void TimeBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MidiEditorViewModel vm && e.Source is FrameworkElement element)
            {
                element.Focus();
                vm.MouseHandler.OnTimeBarMouseDown(e.GetPosition(element));
                element.CaptureMouse();
                e.Handled = true;
            }
        }

        private void TimeBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && DataContext is MidiEditorViewModel vm && e.Source is FrameworkElement element)
            {
                if (element.IsMouseCaptured)
                {
                    var contentPosition = e.GetPosition(element);
                    vm.MouseHandler.OnTimeBarMouseMove(contentPosition);

                    var scrollViewerPosition = e.GetPosition(RulerScrollViewer);
                    double viewportWidth = RulerScrollViewer.ActualWidth;
                    double scrollMargin = 30;

                    if (scrollViewerPosition.X > viewportWidth - scrollMargin)
                    {
                        _autoScrollDirection = 1;
                        if (!_autoScrollTimer.IsEnabled) _autoScrollTimer.Start();
                    }
                    else if (scrollViewerPosition.X < scrollMargin)
                    {
                        _autoScrollDirection = -1;
                        if (!_autoScrollTimer.IsEnabled) _autoScrollTimer.Start();
                    }
                    else
                    {
                        _autoScrollDirection = 0;
                        _autoScrollTimer.Stop();
                    }
                }
            }
        }

        private void TimeBar_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _autoScrollTimer.Stop();
            _autoScrollDirection = 0;
            if (DataContext is MidiEditorViewModel vm && e.Source is FrameworkElement element)
            {
                if (element.IsMouseCaptured)
                {
                    element.ReleaseMouseCapture();
                    vm.MouseHandler.OnTimeBarMouseUp();
                }
            }
        }

        private void PianoRoll_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MidiEditorViewModel vm)
            {
                (sender as IInputElement)?.Focus();
                vm.OnPianoRollMouseDown(e.GetPosition(sender as IInputElement), e);
            }
        }

        private void PianoRoll_MouseMove(object sender, MouseEventArgs e)
        {
            if (DataContext is MidiEditorViewModel vm)
            {
                var currentPosition = e.GetPosition(sender as IInputElement);
                if (e.LeftButton == MouseButtonState.Pressed)
                {
                    vm.OnPianoRollMouseMove(currentPosition, e);
                }
                else
                {
                    var noteUnderCursor = vm.HitTestNote(currentPosition);

                    var mode = vm.MouseHandler.GetDragModeForPosition(currentPosition, noteUnderCursor);
                    switch (mode)
                    {
                        case PianoRollMouseHandler.DragMode.Move:
                            Cursor = Cursors.SizeAll;
                            break;
                        case PianoRollMouseHandler.DragMode.ResizeLeft:
                        case PianoRollMouseHandler.DragMode.ResizeRight:
                            Cursor = Cursors.SizeWE;
                            break;
                        case PianoRollMouseHandler.DragMode.DragFlag:
                            Cursor = Cursors.SizeWE;
                            break;
                        default:
                            Cursor = Cursors.Arrow;
                            break;
                    }
                }
            }
        }

        private void PianoRoll_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MidiEditorViewModel vm)
            {
                vm.OnPianoRollMouseUp(e);
            }
            Cursor = Cursors.Arrow;
            (sender as IInputElement)?.ReleaseMouseCapture();
        }

        private void PianoKey_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PianoKeyViewModel keyVm && DataContext is MidiEditorViewModel editorVm)
            {
                if (!keyVm.IsPressed)
                {
                    editorVm.PlayPianoKey(keyVm.NoteNumber);
                    keyVm.IsPressed = true;
                }
            }
        }

        private void PianoKey_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PianoKeyViewModel keyVm && DataContext is MidiEditorViewModel editorVm)
            {
                editorVm.StopPianoKey(keyVm.NoteNumber);
                keyVm.IsPressed = false;
            }
        }

        private void PianoKey_MouseEnter(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement element && element.DataContext is PianoKeyViewModel keyVm && DataContext is MidiEditorViewModel editorVm)
            {
                if (!keyVm.IsPressed)
                {
                    editorVm.PlayPianoKey(keyVm.NoteNumber);
                    keyVm.IsPressed = true;
                }
            }
        }

        private void PianoKey_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is PianoKeyViewModel keyVm && DataContext is MidiEditorViewModel editorVm)
            {
                if (keyVm.IsPressed)
                {
                    editorVm.StopPianoKey(keyVm.NoteNumber);
                    keyVm.IsPressed = false;
                }
            }
        }


        private void PianoRoll_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is MidiEditorViewModel vm)
            {
                vm.UpdateContextMenuState(e.GetPosition(sender as IInputElement));
            }
        }

        private void PianoRollScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Shift)
            {
                var scrollViewer = (ScrollViewer)sender;
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToHorizontalOffset(scrollViewer.HorizontalOffset - e.Delta);
                    e.Handled = true;
                }
            }
        }

        private void UpdateThumbnail()
        {
            if (!_viewModel.ShowThumbnail) return;

            var totalDuration = _viewModel.MaxTime.TotalSeconds;
            var canvasWidth = ThumbnailCanvas.ActualWidth;
            if (totalDuration <= 0 || canvasWidth <= 0) return;

            var viewportWidth = PianoRollScrollViewer.ViewportWidth;
            var horizontalOffset = PianoRollScrollViewer.HorizontalOffset;
            var totalWidth = _viewModel.PianoRollWidth;

            var viewportRectX = (horizontalOffset / totalWidth) * canvasWidth;
            var viewportRectWidth = (viewportWidth / totalWidth) * canvasWidth;

            Canvas.SetLeft(ThumbnailViewport, viewportRectX);
            ThumbnailViewport.Width = viewportRectWidth;
            ThumbnailViewport.Height = ThumbnailCanvas.ActualHeight;

            var timelineX = (_viewModel.CurrentTime.TotalSeconds / totalDuration) * canvasWidth;
            Canvas.SetLeft(ThumbnailTimeline, timelineX);
        }

        private void Thumbnail_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource == ThumbnailViewport)
            {
                _isThumbnailDragging = true;
                _thumbnailDragStartPoint = e.GetPosition(ThumbnailCanvas);
                _thumbnailDragStartOffset = PianoRollScrollViewer.HorizontalOffset;
                ThumbnailViewport.CaptureMouse();
            }
        }

        private void Thumbnail_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isThumbnailDragging)
            {
                var currentPoint = e.GetPosition(ThumbnailCanvas);
                var delta = currentPoint.X - _thumbnailDragStartPoint.X;
                UpdateScrollFromThumbnail(delta);
            }
            UpdateThumbnailPopup(e);
        }

        private void Thumbnail_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (_isThumbnailDragging)
            {
                _isThumbnailDragging = false;
                ThumbnailViewport.ReleaseMouseCapture();
            }
        }

        private void UpdateScrollFromThumbnail(double deltaX)
        {
            var canvasWidth = ThumbnailCanvas.ActualWidth;
            if (canvasWidth <= 0) return;

            var totalWidth = _viewModel.PianoRollWidth;
            var offsetPerPixel = totalWidth / canvasWidth;
            var newOffset = _thumbnailDragStartOffset + (deltaX * offsetPerPixel);
            PianoRollScrollViewer.ScrollToHorizontalOffset(newOffset);
        }

        private void Thumbnail_MouseEnter(object sender, MouseEventArgs e)
        {
            ThumbnailPopup.IsOpen = true;
        }

        private void Thumbnail_MouseLeave(object sender, MouseEventArgs e)
        {
            ThumbnailPopup.IsOpen = false;
        }

        private void UpdateThumbnailPopup(MouseEventArgs e)
        {
            if (!_viewModel.ShowThumbnail || _viewModel.MidiFile == null || !_viewModel.AllNotes.Any()) return;

            Point mousePosition = e.GetPosition(ThumbnailCanvas);
            var canvasWidth = ThumbnailCanvas.ActualWidth;
            var popupCanvas = ThumbnailPopupCanvas;
            if (canvasWidth <= 0 || popupCanvas == null) return;

            var popupWidth = popupCanvas.Width;
            var popupHeight = popupCanvas.Height;

            var totalDuration = _viewModel.MaxTime.TotalSeconds;
            var horizontalRatio = mousePosition.X / canvasWidth;
            var centerTime = totalDuration * horizontalRatio;

            var zoomFactor = 5.0;
            var timeWindow = totalDuration / zoomFactor;
            var startTime = centerTime - timeWindow / 2.0;
            var endTime = centerTime + timeWindow / 2.0;

            popupCanvas.Children.Clear();

            var bg = new Rectangle { Width = popupWidth, Height = popupHeight, Fill = new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)) };
            popupCanvas.Children.Add(bg);

            var gridBrush = new SolidColorBrush(Colors.LightGray);
            var ticksPerGrid = _viewModel.GetTicksPerGrid();
            var tempoMap = MidiProcessor.ExtractTempoMap(_viewModel.MidiFile, MidiConfiguration.Default);
            var totalTicks = _viewModel.MidiFile.Events.SelectMany(t => t).Any() ? _viewModel.MidiFile.Events.SelectMany(t => t).Max(ev => ev.AbsoluteTime) : 1;
            var totalTime = MidiProcessor.TicksToTimeSpan(totalTicks, _viewModel.MidiFile.DeltaTicksPerQuarterNote, tempoMap).TotalSeconds;

            if (totalTime > 0 && ticksPerGrid > 0)
            {
                var timePerGrid = MidiProcessor.TicksToTimeSpan((long)ticksPerGrid, _viewModel.MidiFile.DeltaTicksPerQuarterNote, tempoMap).TotalSeconds;
                if (timePerGrid > 0)
                {
                    var startGrid = Math.Floor(startTime / timePerGrid);
                    for (double i = startGrid; ; i++)
                    {
                        var time = i * timePerGrid;
                        if (time > endTime) break;
                        if (time >= startTime)
                        {
                            var x = ((time - startTime) / timeWindow) * popupWidth;
                            var line = new Line { X1 = x, Y1 = 0, X2 = x, Y2 = popupHeight, Stroke = gridBrush, StrokeThickness = 0.5 };
                            popupCanvas.Children.Add(line);
                        }
                    }
                }
            }


            int minNote = _viewModel.AllNotes.Min(n => n.NoteNumber);
            int maxNote = _viewModel.AllNotes.Max(n => n.NoteNumber);
            int noteRange = Math.Max(1, maxNote - minNote);

            var notesInView = _viewModel.AllNotes.Where(n => n.StartTime.TotalSeconds < endTime && (n.StartTime + n.Duration).TotalSeconds > startTime);

            foreach (var note in notesInView)
            {
                var x = ((note.StartTime.TotalSeconds - startTime) / timeWindow) * popupWidth;
                var w = (note.Duration.TotalSeconds / timeWindow) * popupWidth;
                var y = ((double)(note.NoteNumber - minNote) / noteRange) * (popupHeight - 1);
                var h = Math.Max(1.0, popupHeight / (double)noteRange);

                var rect = new Rectangle
                {
                    Width = Math.Max(1, w),
                    Height = h,
                    Fill = new SolidColorBrush(Color.FromArgb(255, 74, 144, 226)),
                    Stroke = new SolidColorBrush(Color.FromArgb(255, 53, 122, 189)),
                    StrokeThickness = 0.5
                };
                Canvas.SetLeft(rect, x);
                Canvas.SetTop(rect, popupHeight - y - h);
                popupCanvas.Children.Add(rect);
            }

            var timelineX = ((_viewModel.CurrentTime.TotalSeconds - startTime) / timeWindow) * popupWidth;
            if (timelineX >= 0 && timelineX <= popupWidth)
            {
                var timeline = new Line { X1 = timelineX, Y1 = 0, X2 = timelineX, Y2 = popupHeight, Stroke = Brushes.Red, StrokeThickness = 1 };
                popupCanvas.Children.Add(timeline);
            }

            ThumbnailPopup.Placement = PlacementMode.Relative;

            var hOffset = mousePosition.X - popupWidth / 2;
            if (hOffset < 0)
            {
                hOffset = 0;
            }
            if (hOffset + popupWidth > canvasWidth)
            {
                hOffset = canvasWidth - popupWidth;
            }

            ThumbnailPopup.HorizontalOffset = hOffset;
            ThumbnailPopup.VerticalOffset = -popupHeight - 5;
        }
    }
}
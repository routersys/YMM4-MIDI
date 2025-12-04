using MIDI.Configuration.Models;
using MIDI.UI.ViewModels.MidiEditor.Settings;
using MIDI.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace MIDI.UI.ViewModels.MidiEditor.Rendering
{
    public class PianoRollRenderer : IDisposable
    {
        private readonly MidiEditorViewModel _vm;
        private readonly DispatcherTimer _renderTimer;
        private readonly object _renderLock = new object();
        private readonly ConcurrentQueue<Rect> _dirtyRects = new ConcurrentQueue<Rect>();
        private bool _fullRedrawRequested = true;
        private Int32Rect _visibleRect;
        private bool _isDisposed;

        private Color _gridColor;
        private Color _horizontalLineColor;
        private Color _whiteKeyBackgroundColor;
        private Color _blackKeyBackgroundColor;

        public PianoRollRenderer(MidiEditorViewModel vm)
        {
            _vm = vm;
            UpdateColors(false);

            _renderTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _renderTimer.Tick += RenderTimer_Tick;
            _renderTimer.Start();
        }

        public void ThemeChanged(bool isDarkMode)
        {
            UpdateColors(isDarkMode);
            RequestRedraw(true);
        }

        private void UpdateColors(bool isDarkMode)
        {
            if (isDarkMode)
            {
                _whiteKeyBackgroundColor = Color.FromRgb(45, 45, 48);
                _blackKeyBackgroundColor = Color.FromRgb(60, 60, 60);
                _gridColor = Color.FromRgb(85, 85, 85);
                _horizontalLineColor = Color.FromRgb(70, 70, 70);
            }
            else
            {
                _whiteKeyBackgroundColor = Color.FromRgb(255, 255, 255);
                _blackKeyBackgroundColor = Color.FromRgb(240, 240, 240);
                _gridColor = Color.FromRgb(221, 221, 221);
                _horizontalLineColor = Color.FromRgb(230, 230, 230);
            }
        }

        public void InitializeBitmap()
        {
            lock (_renderLock)
            {
                int width = (int)Math.Ceiling(_vm.PianoRollWidth);
                int height = (int)Math.Ceiling(_vm.PianoRollHeight);

                if (width <= 0 || height <= 0)
                {
                    width = 3000;
                    height = 2560;
                }

                if (_vm.PianoRollBitmap == null || _vm.PianoRollBitmap.PixelWidth != width || _vm.PianoRollBitmap.PixelHeight != height)
                {
                    _vm.PianoRollBitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Pbgra32, null);
                }

                _fullRedrawRequested = true;
                _dirtyRects.Clear();
                RequestRedraw(true);
            }
        }

        private void RenderTimer_Tick(object? sender, EventArgs e)
        {
            if (_vm.PianoRollBitmap == null) return;

            bool needsFullRedraw;
            lock (_renderLock)
            {
                needsFullRedraw = _fullRedrawRequested;
                _fullRedrawRequested = false;
            }

            if (needsFullRedraw)
            {
                UpdateVisibleRect();
                var preloadRect = GetPreloadRect(_visibleRect);
                RenderRect(preloadRect);
            }
            else
            {
                var dirtyRects = new List<Rect>();
                while (_dirtyRects.TryDequeue(out var rect))
                {
                    dirtyRects.Add(rect);
                }

                if (dirtyRects.Any())
                {
                    Rect combinedRect = dirtyRects[0];
                    for (int i = 1; i < dirtyRects.Count; i++)
                    {
                        combinedRect.Union(dirtyRects[i]);
                    }

                    Int32Rect intRect = new Int32Rect(
                        (int)Math.Floor(combinedRect.X),
                        (int)Math.Floor(combinedRect.Y),
                        (int)Math.Ceiling(combinedRect.Width),
                        (int)Math.Ceiling(combinedRect.Height)
                    );
                    RenderRect(intRect);
                }
            }
        }

        public void UpdateVisibleRect()
        {
            _visibleRect = new Int32Rect((int)_vm.HorizontalOffset, (int)_vm.VerticalScrollOffset, (int)_vm.ViewportWidth, (int)_vm.ViewportHeight);
        }

        public void RequestNoteRedraw(NoteViewModel note)
        {
            Rect rect = GetNoteRectWPF(note);
            rect = new Rect(rect.X - 2, rect.Y - 2, rect.Width + 4, rect.Height + 4);
            _dirtyRects.Enqueue(rect);
        }

        public void RequestRedraw(bool fullRedraw)
        {
            lock (_renderLock)
            {
                _fullRedrawRequested = _fullRedrawRequested || fullRedraw;
            }
        }

        private Rect GetNoteRectWPF(NoteViewModel note)
        {
            double x = note.StartTime.TotalSeconds * _vm.HorizontalZoom;
            double y = (_vm.MaxNoteNumber - note.NoteNumber) * 20.0 * _vm.VerticalZoom / _vm.KeyYScale + (note.CentOffset / 100.0 * 20.0 * _vm.VerticalZoom / _vm.KeyYScale);
            double width = Math.Max(1.0, note.Duration.TotalSeconds * _vm.HorizontalZoom);
            double height = 20.0 * _vm.VerticalZoom / _vm.KeyYScale;

            return new Rect(x, y, width, height);
        }

        private Int32Rect GetPreloadRect(Int32Rect visible)
        {
            int preloadWidth = (int)_vm.ViewportWidth;
            int newX = Math.Max(0, visible.X - preloadWidth);
            int newWidth = visible.Width + 2 * preloadWidth;

            if (newX + newWidth > _vm.PianoRollWidth)
            {
                newWidth = Math.Max(0, (int)_vm.PianoRollWidth - newX);
            }

            int newY = 0;
            int newHeight = (int)_vm.PianoRollHeight;

            return new Int32Rect(newX, newY, newWidth, newHeight);
        }

        private void RenderRect(Int32Rect rect)
        {
            if (_vm.PianoRollBitmap == null || _vm.MidiFile == null) return;

            Rect wpfRect = new Rect(rect.X, rect.Y, rect.Width, rect.Height);
            wpfRect.Intersect(new Rect(0, 0, _vm.PianoRollBitmap.PixelWidth, _vm.PianoRollBitmap.PixelHeight));

            if (wpfRect.IsEmpty) return;

            rect = new Int32Rect(
                (int)Math.Floor(wpfRect.X),
                (int)Math.Floor(wpfRect.Y),
                (int)Math.Ceiling(wpfRect.Width),
                (int)Math.Ceiling(wpfRect.Height)
            );

            if (rect.Width <= 0 || rect.Height <= 0) return;

            try
            {
                _vm.PianoRollBitmap.Lock();
                unsafe
                {
                    IntPtr pBackBuffer = _vm.PianoRollBitmap.BackBuffer;
                    int stride = _vm.PianoRollBitmap.BackBufferStride;
                    byte* pBits = (byte*)pBackBuffer.ToPointer();

                    DrawBackground(pBits, stride, rect);
                    DrawGrid(pBits, stride, rect);
                    DrawNotes(pBits, stride, rect);
                }
                _vm.PianoRollBitmap.AddDirtyRect(rect);
            }
            finally
            {
                _vm.PianoRollBitmap.Unlock();
            }
        }

        private unsafe void DrawBackground(byte* pBits, int stride, Int32Rect rect)
        {
            if (_vm.PianoRollBitmap == null) return;

            int whiteColorInt = (_whiteKeyBackgroundColor.A << 24) | (_whiteKeyBackgroundColor.R << 16) | (_whiteKeyBackgroundColor.G << 8) | _whiteKeyBackgroundColor.B;
            int blackColorInt = (_blackKeyBackgroundColor.A << 24) | (_blackKeyBackgroundColor.R << 16) | (_blackKeyBackgroundColor.G << 8) | _blackKeyBackgroundColor.B;

            double noteHeight = 20.0 * _vm.VerticalZoom / _vm.KeyYScale;
            if (noteHeight <= 0) noteHeight = 20;

            for (int y = rect.Y; y < rect.Y + rect.Height; y++)
            {
                if (y >= _vm.PianoRollBitmap.PixelHeight) break;

                int noteNumber = _vm.MaxNoteNumber - (int)Math.Floor(y / noteHeight);
                if (noteNumber < 0 || noteNumber > _vm.MaxNoteNumber)
                {
                    int* pRow = (int*)(pBits + y * stride) + rect.X;
                    int* pRowEnd = pRow + rect.Width;
                    if (pRowEnd > (int*)(pBits + y * stride) + _vm.PianoRollBitmap.PixelWidth) pRowEnd = (int*)(pBits + y * stride) + _vm.PianoRollBitmap.PixelWidth;
                    while (pRow < pRowEnd) { *pRow = whiteColorInt; pRow++; }
                    continue;
                }

                bool isBlackKey = false;
                if (_vm.PianoKeysMap.TryGetValue(noteNumber, out var keyVm))
                {
                    isBlackKey = keyVm.IsBlackKey;
                }

                int colorInt = isBlackKey ? whiteColorInt : blackColorInt;

                int* pRowLine = (int*)(pBits + y * stride) + rect.X;
                int* pRowLineEnd = pRowLine + rect.Width;
                if (pRowLineEnd > (int*)(pBits + y * stride) + _vm.PianoRollBitmap.PixelWidth) pRowLineEnd = (int*)(pBits + y * stride) + _vm.PianoRollBitmap.PixelWidth;
                while (pRowLine < pRowLineEnd) { *pRowLine = colorInt; pRowLine++; }
            }
        }

        private unsafe void DrawGrid(byte* pBits, int stride, Int32Rect rect)
        {
            if (_vm.MidiFile == null || _vm.PianoRollBitmap == null) return;

            int gridColorInt = (_gridColor.A << 24) | (_gridColor.R << 16) | (_gridColor.G << 8) | _gridColor.B;

            var tempoMap = MidiProcessor.ExtractTempoMap(_vm.MidiFile, MidiConfiguration.Default);
            double ticksPerGrid = _vm.ViewManager.GetTicksPerGrid();
            if (ticksPerGrid <= 0) return;

            long ticksPerBar = _vm.TicksPerBar;
            long startTicks = _vm.ViewManager.TimeToTicks(TimeSpan.FromSeconds(rect.X / _vm.HorizontalZoom));
            long startGridLine = (long)Math.Floor(startTicks / ticksPerGrid);

            for (long ticks = startGridLine * (long)ticksPerGrid; ; ticks += (long)ticksPerGrid)
            {
                if (ticks < 0) continue;
                var time = MidiProcessor.TicksToTimeSpan(ticks, _vm.MidiFile.DeltaTicksPerQuarterNote, tempoMap);
                int x = (int)(time.TotalSeconds * _vm.HorizontalZoom);

                if (x > rect.X + rect.Width) break;
                if (x >= rect.X && x < _vm.PianoRollBitmap.PixelWidth)
                {
                    bool isMeasureLine = ticksPerBar > 0 && ticks % ticksPerBar == 0;
                    for (int y = rect.Y; y < rect.Y + rect.Height; y++)
                    {
                        if (y >= _vm.PianoRollBitmap.PixelHeight) break;
                        if (isMeasureLine || (y / 2) % 2 == 0)
                        {
                            int* pPixel = (int*)(pBits + y * stride + x * 4);
                            *pPixel = gridColorInt;
                        }
                    }
                }
            }

            int hLineColorInt = (_horizontalLineColor.A << 24) | (_horizontalLineColor.R << 16) | (_horizontalLineColor.G << 8) | _horizontalLineColor.B;
            double noteHeight = 20.0 * _vm.VerticalZoom / _vm.KeyYScale;
            if (noteHeight <= 0) noteHeight = 20;

            int startNote = _vm.MaxNoteNumber - (int)Math.Floor((rect.Y + rect.Height) / noteHeight);
            int endNote = _vm.MaxNoteNumber - (int)Math.Floor(rect.Y / noteHeight);

            for (int i = startNote; i <= endNote; i++)
            {
                if (i < 0 || i > _vm.MaxNoteNumber) continue;
                int y = (int)((_vm.MaxNoteNumber - i) * noteHeight);
                if (y >= rect.Y && y < rect.Y + rect.Height && y < _vm.PianoRollBitmap.PixelHeight)
                {
                    int* pRow = (int*)(pBits + y * stride) + rect.X;
                    int* pRowEnd = pRow + rect.Width;
                    if (pRowEnd > (int*)(pBits + y * stride) + _vm.PianoRollBitmap.PixelWidth) pRowEnd = (int*)(pBits + y * stride) + _vm.PianoRollBitmap.PixelWidth;
                    while (pRow < pRowEnd) { *pRow = hLineColorInt; pRow++; }
                }
            }
        }

        private unsafe void DrawNotes(byte* pBits, int stride, Int32Rect rect)
        {
            var minVisibleTime = TimeSpan.FromSeconds(rect.X / _vm.HorizontalZoom);
            var maxVisibleTime = TimeSpan.FromSeconds((rect.X + rect.Width) / _vm.HorizontalZoom);

            double noteHeight = 20.0 * _vm.VerticalZoom / _vm.KeyYScale;
            int minVisibleNote = _vm.MaxNoteNumber - (int)Math.Floor((rect.Y + rect.Height) / noteHeight);
            int maxVisibleNote = _vm.MaxNoteNumber - (int)Math.Floor(rect.Y / noteHeight);

            var notesToDraw = _vm.AllNotes.Where(n =>
                n.StartTime < maxVisibleTime &&
                (n.StartTime + n.Duration) > minVisibleTime &&
                n.NoteNumber >= minVisibleNote && n.NoteNumber <= maxVisibleNote
            ).ToList();

            Color selectedColor = MidiEditorSettings.Default.Note.SelectedNoteColor;

            foreach (var note in notesToDraw)
            {
                if (note.IsEditing) continue;

                Color fillColor = note.IsSelected ? selectedColor : note.Color;
                byte r = fillColor.R, g = fillColor.G, b = fillColor.B, a = fillColor.A;

                Rect noteRectWPF = GetNoteRectWPF(note);
                noteRectWPF.Intersect(new Rect(rect.X, rect.Y, rect.Width, rect.Height));
                if (noteRectWPF.IsEmpty) continue;

                Int32Rect noteRect = new Int32Rect(
                    (int)Math.Floor(noteRectWPF.X),
                    (int)Math.Floor(noteRectWPF.Y),
                    (int)Math.Ceiling(noteRectWPF.Width),
                    (int)Math.Ceiling(noteRectWPF.Height)
                );

                Color strokeColor = note.IsSelected ? Colors.Orange : Color.FromRgb(53, 122, 189);
                int strokeThickness = note.IsSelected ? 2 : 1;
                int strokeColorInt = (strokeColor.A << 24) | (strokeColor.R << 16) | (strokeColor.G << 8) | strokeColor.B;
                int fillColorInt = (a << 24) | (r << 16) | (g << 8) | b;

                for (int y = noteRect.Y; y < noteRect.Y + noteRect.Height; y++)
                {
                    int* pRow = (int*)(pBits + y * stride) + noteRect.X;
                    for (int x = noteRect.X; x < noteRect.X + noteRect.Width; x++)
                    {
                        if (x < noteRect.X + strokeThickness || x >= noteRect.X + noteRect.Width - strokeThickness ||
                            y < noteRect.Y + strokeThickness || y >= noteRect.Y + noteRect.Height - strokeThickness)
                        {
                            *pRow = strokeColorInt;
                        }
                        else
                        {
                            *pRow = fillColorInt;
                        }
                        pRow++;
                    }
                }
            }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _renderTimer.Stop();
        }
    }
}
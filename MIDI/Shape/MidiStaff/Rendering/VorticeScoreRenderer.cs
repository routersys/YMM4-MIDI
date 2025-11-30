using Manufaktura.Controls.Audio;
using Manufaktura.Controls.Model;
using Manufaktura.Controls.Model.Fonts;
using Manufaktura.Controls.Primitives;
using Manufaktura.Controls.Rendering;
using MIDI.Shape.MidiStaff.Models;
using System;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace MIDI.Shape.MidiStaff.Rendering
{
    public class VorticeScoreRenderer : ScoreRenderer<ID2D1DeviceContext>, IDisposable
    {
        private readonly ID2D1SolidColorBrush _brush;
        private readonly ID2D1SolidColorBrush _highlightBrush;
        private readonly IDWriteTextFormat _textFormat;
        private readonly ID2D1StrokeStyle _strokeStyle;

        private const float BaselineOffset = 23.8f;
        private const float HorizontalOffset = 1.0f;

        public double CurrentTime { get; set; } = -1;

        public override bool CanDrawCharacterInBounds => true;

        public VorticeScoreRenderer(ID2D1DeviceContext canvas, ID2D1SolidColorBrush brush, ID2D1SolidColorBrush highlightBrush, IDWriteTextFormat textFormat, ScoreRendererSettings settings)
            : base(canvas, settings)
        {
            _brush = brush;
            _highlightBrush = highlightBrush;
            _textFormat = textFormat;

            _strokeStyle = canvas.Factory.CreateStrokeStyle(new StrokeStyleProperties { StartCap = CapStyle.Square, EndCap = CapStyle.Square });
        }

        public override void DrawArc(Rectangle rect, double startAngle, double sweepAngle, Pen pen, MusicalSymbol owner)
        {
            var thickness = Math.Max((float)pen.Thickness, 1.0f);
            var ellipse = new Ellipse(new Vector2((float)rect.X + (float)rect.Width / 2, (float)rect.Y + (float)rect.Height / 2), (float)rect.Width / 2, (float)rect.Height / 2);
            Canvas.DrawEllipse(ellipse, GetBrush(owner), thickness, _strokeStyle);
        }

        public override void DrawBezier(Point p1, Point p2, Point p3, Point p4, Pen pen, MusicalSymbol owner)
        {
            var thickness = Math.Max((float)pen.Thickness, 1.0f);
            using var geometry = Canvas.Factory.CreatePathGeometry();
            using var sink = geometry.Open();
            sink.BeginFigure(new Vector2((float)p1.X, (float)p1.Y), FigureBegin.Hollow);
            sink.AddBezier(new BezierSegment(
                new Vector2((float)p2.X, (float)p2.Y),
                new Vector2((float)p3.X, (float)p3.Y),
                new Vector2((float)p4.X, (float)p4.Y)));
            sink.EndFigure(FigureEnd.Open);
            sink.Close();

            Canvas.DrawGeometry(geometry, GetBrush(owner), thickness, _strokeStyle);
        }

        public override void DrawLine(Point startPoint, Point endPoint, Pen pen, MusicalSymbol owner)
        {
            var thickness = ((float)pen.Thickness);
            if (thickness > 2.0f)
            {
                thickness *= 1.0f;
            }

            bool isHorizontal = Math.Abs(startPoint.Y - endPoint.Y) < 0.1;
            bool isVertical = Math.Abs(startPoint.X - endPoint.X) < 0.1;
            var brush = isHorizontal ? _brush : GetBrush(owner);

            if (!isHorizontal && !isVertical)
            {
                var dirX = endPoint.X - startPoint.X;
                var dirY = endPoint.Y - startPoint.Y;
                var unitDir = Vector2.Normalize(new Vector2((float)dirX, (float)dirY));
                var extension = 0.6f;

                var extendedStart = new Point(startPoint.X - unitDir.X * extension, startPoint.Y - unitDir.Y * extension);
                var extendedEnd = new Point(endPoint.X + unitDir.X * extension, endPoint.Y + unitDir.Y * extension);

                using var geometry = Canvas.Factory.CreatePathGeometry();
                using var sink = geometry.Open();
                var p1 = new Vector2((float)extendedStart.X, (float)extendedStart.Y - thickness / 2);
                var p2 = new Vector2((float)extendedEnd.X, (float)extendedEnd.Y - thickness / 2);
                var p3 = new Vector2((float)extendedEnd.X, (float)extendedEnd.Y + thickness / 2);
                var p4 = new Vector2((float)extendedStart.X, (float)extendedStart.Y + thickness / 2);
                sink.BeginFigure(p1, FigureBegin.Filled);
                sink.AddLine(p2);
                sink.AddLine(p3);
                sink.AddLine(p4);
                sink.EndFigure(FigureEnd.Closed);
                sink.Close();

                Canvas.FillGeometry(geometry, brush);
            }
            else
            {
                Canvas.DrawLine(
                    new Vector2((float)startPoint.X, (float)startPoint.Y),
                    new Vector2((float)endPoint.X, (float)endPoint.Y),
                    brush,
                    thickness, _strokeStyle);
            }
        }

        public override void DrawString(string text, MusicFontStyles fontStyle, Point location, Manufaktura.Controls.Primitives.Color color, MusicalSymbol owner)
        {
            var rect = new Rect((float)location.X + HorizontalOffset, (float)location.Y - BaselineOffset, (float)location.X + 2000, (float)location.Y + 2000);
            Canvas.DrawText(text, _textFormat, rect, GetBrush(owner));
        }

        public override void DrawCharacterInBounds(char character, MusicFontStyles fontStyle, Point location, Manufaktura.Controls.Primitives.Size size, Manufaktura.Controls.Primitives.Color color, MusicalSymbol owner)
        {
            var rect = new Rect((float)location.X + HorizontalOffset, (float)location.Y - BaselineOffset, (float)location.X + 2000, (float)location.Y + 2000);
            Canvas.DrawText(character.ToString(), _textFormat, rect, GetBrush(owner));
        }

        protected override void DrawPlaybackCursor(PlaybackCursorPosition position, Point start, Point end)
        {
        }

        private ID2D1SolidColorBrush GetBrush(MusicalSymbol owner)
        {
            if (owner is IMidiElement el && CurrentTime >= 0)
            {
                if (CurrentTime >= el.StartTime && CurrentTime < el.EndTime)
                {
                    return _highlightBrush;
                }
            }
            return _brush;
        }

        public void Dispose()
        {
            _strokeStyle.Dispose();
        }
    }
}
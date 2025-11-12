using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MIDI.Shape.MidiPianoRoll.Models;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using Color = Vortice.Mathematics.Color;
using D2D = Vortice.Direct2D1;

namespace MIDI.Shape.MidiPianoRoll.Rendering
{
    public class Direct2DResourceProvider : IDisposable
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private readonly DisposeCollector _disposer = new();
        private PianoRollResourcePack _currentResourcePack = new();
        private PianoRollOrientation _currentOrientation = PianoRollOrientation.Horizontal;

        public ID2D1SolidColorBrush? WhiteKeyBrush { get; private set; }
        public ID2D1SolidColorBrush? BlackKeyBrush { get; private set; }
        public ID2D1SolidColorBrush? NoteBrush { get; private set; }
        public ID2D1SolidColorBrush? SelectedNoteBrush { get; private set; }
        public ID2D1SolidColorBrush? PlayingKeyHighlightBrush { get; private set; }
        public ID2D1SolidColorBrush? LineBrush { get; private set; }
        public ID2D1SolidColorBrush? GridLineBrush { get; private set; }
        public ID2D1SolidColorBrush? KeySeparatorBrush { get; private set; }
        public ID2D1StrokeStyle? GridStrokeStyle { get; private set; }
        public ID2D1LinearGradientBrush? WhiteKeyGradientBrush { get; private set; }
        public ID2D1LinearGradientBrush? BlackKeyGradientBrush { get; private set; }
        public ID2D1SolidColorBrush? PressedKeyDarkEdgeBrush { get; private set; }
        public ID2D1SolidColorBrush? NoteHitGlowBrush { get; private set; }
        public ID2D1SolidColorBrush? NoteSplashGlowBrush { get; private set; }
        public Dictionary<int, ID2D1SolidColorBrush> ChannelBrushes { get; } = new();

        public Direct2DResourceProvider(IGraphicsDevicesAndContext devices)
        {
            _devices = devices;
            UpdateResources(new PianoRollResourcePack(), PianoRollOrientation.Horizontal);
        }

        public bool UpdateResources(PianoRollResourcePack resourcePack, PianoRollOrientation orientation)
        {
            if (object.ReferenceEquals(_currentResourcePack, resourcePack) && _currentOrientation == orientation)
                return false;

            _disposer.DisposeAndClear();
            ChannelBrushes.Clear();

            _currentResourcePack = resourcePack;
            _currentOrientation = orientation;
            var dc = _devices.DeviceContext;
            var factory = dc.Factory;

            WhiteKeyBrush = dc.CreateSolidColorBrush(RendererHelper.ToVorticeColor(_currentResourcePack.WhiteKeyColor));
            _disposer.Collect(WhiteKeyBrush);
            BlackKeyBrush = dc.CreateSolidColorBrush(RendererHelper.ToVorticeColor(_currentResourcePack.BlackKeyColor));
            _disposer.Collect(BlackKeyBrush);
            NoteBrush = dc.CreateSolidColorBrush(RendererHelper.ToVorticeColor(_currentResourcePack.NoteColor));
            _disposer.Collect(NoteBrush);
            SelectedNoteBrush = dc.CreateSolidColorBrush(RendererHelper.ToVorticeColor(_currentResourcePack.SelectedNoteColor));
            _disposer.Collect(SelectedNoteBrush);
            PlayingKeyHighlightBrush = dc.CreateSolidColorBrush(RendererHelper.ToVorticeColor(_currentResourcePack.PlayingKeyColor));
            _disposer.Collect(PlayingKeyHighlightBrush);
            LineBrush = dc.CreateSolidColorBrush(RendererHelper.ToVorticeColor(_currentResourcePack.LineColor));
            _disposer.Collect(LineBrush);
            var gridLineColor = RendererHelper.ToVorticeColor(_currentResourcePack.LineColor);
            GridLineBrush = dc.CreateSolidColorBrush(new Color4(gridLineColor.R, gridLineColor.G, gridLineColor.B, gridLineColor.A * 0.5f));
            _disposer.Collect(GridLineBrush);
            KeySeparatorBrush = dc.CreateSolidColorBrush(RendererHelper.ToVorticeColor(_currentResourcePack.KeySeparatorColor));
            _disposer.Collect(KeySeparatorBrush);
            PressedKeyDarkEdgeBrush = dc.CreateSolidColorBrush(RendererHelper.ToVorticeColor(_currentResourcePack.PressedKeyDarkEdgeColor));
            _disposer.Collect(PressedKeyDarkEdgeBrush);

            NoteHitGlowBrush = dc.CreateSolidColorBrush(RendererHelper.ToVorticeColor(_currentResourcePack.NoteHitGlowColor));
            _disposer.Collect(NoteHitGlowBrush);
            NoteSplashGlowBrush = dc.CreateSolidColorBrush(RendererHelper.ToVorticeColor(_currentResourcePack.NoteSplashGlowColor));
            _disposer.Collect(NoteSplashGlowBrush);

            GradientStop[] whiteStops = [
                new GradientStop(0.0f, RendererHelper.ToVorticeColor(_currentResourcePack.WhiteKeyGradientStop1)),
                new GradientStop(1.0f, RendererHelper.ToVorticeColor(_currentResourcePack.WhiteKeyGradientStop2))
            ];
            GradientStop[] blackStops = [
                new GradientStop(0.0f, RendererHelper.ToVorticeColor(_currentResourcePack.BlackKeyGradientStop1)),
                new GradientStop(1.0f, RendererHelper.ToVorticeColor(_currentResourcePack.BlackKeyGradientStop2))
            ];

            var gradientProps = _currentOrientation == PianoRollOrientation.Horizontal ?
                new LinearGradientBrushProperties { StartPoint = Vector2.Zero, EndPoint = new Vector2(0, 1) } :
                new LinearGradientBrushProperties { StartPoint = Vector2.Zero, EndPoint = new Vector2(1, 0) };

            WhiteKeyGradientBrush = dc.CreateLinearGradientBrush(gradientProps, dc.CreateGradientStopCollection(whiteStops, Gamma.Linear, ExtendMode.Clamp));
            _disposer.Collect(WhiteKeyGradientBrush);
            BlackKeyGradientBrush = dc.CreateLinearGradientBrush(gradientProps, dc.CreateGradientStopCollection(blackStops, Gamma.Linear, ExtendMode.Clamp));
            _disposer.Collect(BlackKeyGradientBrush);

            GridStrokeStyle = factory.CreateStrokeStyle(new StrokeStyleProperties { DashStyle = DashStyle.Dash });
            _disposer.Collect(GridStrokeStyle);

            foreach (var pair in _currentResourcePack.ChannelColors)
            {
                var brush = dc.CreateSolidColorBrush(RendererHelper.ToVorticeColor(pair.Value));
                ChannelBrushes[pair.Key] = brush;
                _disposer.Collect(brush);
            }

            return true;
        }

        public ID2D1SolidColorBrush GetNoteBrush(NoteEventInfo note)
        {
            if (_currentResourcePack.UseChannelColors && ChannelBrushes.TryGetValue(note.Channel, out var channelBrush))
            {
                return channelBrush;
            }
            return NoteBrush ?? WhiteKeyBrush!;
        }


        public void Dispose()
        {
            _disposer.DisposeAndClear();
            foreach (var brush in ChannelBrushes.Values.ToList())
            {
                brush?.Dispose();
            }
            ChannelBrushes.Clear();
            GC.SuppressFinalize(this);
        }
    }
}
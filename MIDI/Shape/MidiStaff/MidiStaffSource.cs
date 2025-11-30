using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Manufaktura.Controls.Model;
using Manufaktura.Controls.Rendering;
using MIDI.Shape.MidiStaff.Models;
using MIDI.Shape.MidiStaff.Rendering;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Shape;

namespace MIDI.Shape.MidiStaff
{
    public class MidiStaffSource : IShapeSource
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private readonly MidiStaffParameter _parameter;
        private readonly DisposeCollector _disposer = new();
        private ID2D1SolidColorBrush? _mainBrush;
        private ID2D1SolidColorBrush? _highlightBrush;
        private ID2D1Image? _outputImage;
        private ID2D1CommandList? _commandList;
        private IDWriteFactory _dWriteFactory;
        private IDWriteTextFormat _textFormat;

        public ID2D1Image Output => _outputImage ?? throw new InvalidOperationException("Output not ready");

        public MidiStaffSource(IGraphicsDevicesAndContext devices, MidiStaffParameter parameter)
        {
            _devices = devices;
            _parameter = parameter;
            _dWriteFactory = DWrite.DWriteCreateFactory<IDWriteFactory>();

            _textFormat = _dWriteFactory.CreateTextFormat("Polihymnia", 26.0f);

            _commandList = _devices.DeviceContext.CreateCommandList();
            _commandList.Close();
            _outputImage = _commandList;
            _disposer.Collect(_commandList);

            _mainBrush = _devices.DeviceContext.CreateSolidColorBrush(Colors.White);
            _disposer.Collect(_mainBrush);
            _highlightBrush = _devices.DeviceContext.CreateSolidColorBrush(Colors.Cyan);
            _disposer.Collect(_highlightBrush);
        }

        public void Update(TimelineItemSourceDescription desc)
        {
            var fullScore = _parameter.ScoreData;
            if (fullScore == null) return;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;
            var currentTime = (double)frame / fps;

            var dc = _devices.DeviceContext;
            var oldTarget = dc.Target;

            var newCommandList = dc.CreateCommandList();
            dc.Target = newCommandList;
            dc.BeginDraw();
            dc.Clear(null);

            if (_mainBrush != null)
            {
                var c = _parameter.Color;
                _mainBrush.Color = new Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            }
            if (_highlightBrush != null)
            {
                var c = _parameter.HighlightColor;
                _highlightBrush.Color = new Color4(c.R / 255f, c.G / 255f, c.B / 255f, c.A / 255f);
            }

            var pageWidth = (double)_parameter.PageWidth.GetValue(frame, length, fps);
            var lineSpacing = (double)_parameter.LineSpacing.GetValue(frame, length, fps);
            var measuresPerLine = _parameter.MeasuresPerLine;

            var scoreToRender = new Score();

            foreach (var sourceStaff in fullScore.Staves)
            {
                var targetStaff = new Staff();
                scoreToRender.Staves.Add(targetStaff);

                var allMeasures = sourceStaff.Measures;
                int totalMeasures = allMeasures.Count;
                int currentMeasureIndex = 0;

                for (int m = 0; m < totalMeasures; m++)
                {
                    var measure = allMeasures[m];
                    bool found = false;
                    foreach (var element in measure.Elements.OfType<IMidiElement>())
                    {
                        if (currentTime >= element.StartTime && currentTime < element.EndTime)
                        {
                            currentMeasureIndex = m;
                            found = true;
                            break;
                        }
                    }
                    if (found) break;
                }

                if (currentMeasureIndex == 0 && currentTime > 0)
                {
                    var lastElement = sourceStaff.Elements.OfType<IMidiElement>().LastOrDefault();
                    if (lastElement != null && currentTime > lastElement.EndTime) currentMeasureIndex = totalMeasures;
                }

                int halfWindow = _parameter.MaxVisibleMeasures / 2;
                int rawStartMeasure = Math.Max(0, currentMeasureIndex - halfWindow);

                int startMeasure = (rawStartMeasure / measuresPerLine) * measuresPerLine;

                int endMeasure = Math.Min(totalMeasures - 1, startMeasure + _parameter.MaxVisibleMeasures - 1);

                targetStaff.Elements.AddRange(sourceStaff.Elements.OfType<Clef>());
                targetStaff.Elements.AddRange(sourceStaff.Elements.OfType<Key>());
                targetStaff.Elements.AddRange(sourceStaff.Elements.OfType<TimeSignature>());

                for (int m = startMeasure; m <= endMeasure; m++)
                {
                    var measure = allMeasures[m];
                    measure.Width = pageWidth / measuresPerLine;
                    targetStaff.Elements.AddRange(measure.Elements);
                }
            }

            var settings = new ScoreRendererSettings
            {
                PageWidth = pageWidth,
                RenderingMode = ScoreRenderingModes.SinglePage
            };

            using (var renderer = new VorticeScoreRenderer(dc, _mainBrush!, _highlightBrush!, _textFormat, settings))
            {
                renderer.CurrentTime = currentTime;

                var scale = (float)_parameter.Scale.GetValue(frame, length, fps);
                var transform = Matrix3x2.CreateScale(scale, scale);

                var oldTransform = dc.Transform;
                dc.Transform = transform * oldTransform;

                renderer.Render(scoreToRender);

                dc.Transform = oldTransform;
            }

            dc.EndDraw();

            newCommandList.Close();

            dc.Target = oldTarget;

            _disposer.RemoveAndDispose(ref _commandList);
            _commandList = newCommandList;
            _outputImage = _commandList;
            _disposer.Collect(_commandList);
        }

        public IEnumerable<VideoController> Controllers => Enumerable.Empty<VideoController>();

        public void Dispose()
        {
            _disposer.DisposeAndClear();

            _textFormat.Dispose();

            _dWriteFactory.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
using System;
using Vortice.Direct2D1;
using Vortice.Direct2D1.Effects;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace MIDI.Shape.MidiPianoRoll.Rendering
{
    public class EffectRenderer : IDisposable
    {
        private readonly IGraphicsDevicesAndContext _devices;
        private readonly DisposeCollector _disposer = new();
        private ID2D1Effect? _splashGlowEffect;
        private ID2D1BitmapRenderTarget? _splashRenderTarget;

        public ID2D1Image? Output => _splashGlowEffect?.Output;

        public EffectRenderer(IGraphicsDevicesAndContext devices)
        {
            _devices = devices;
            _splashGlowEffect = (ID2D1Effect)_devices.DeviceContext.CreateEffect(EffectGuids.GaussianBlur);
            _disposer.Collect(_splashGlowEffect);
        }

        public void CheckRenderTarget(float totalWidth, float totalHeight)
        {
            if (_splashRenderTarget == null || _splashRenderTarget.PixelSize.Width != (int)totalWidth || _splashRenderTarget.PixelSize.Height != (int)totalHeight)
            {
                if (_splashRenderTarget != null) _disposer.RemoveAndDispose(ref _splashRenderTarget);
                _splashRenderTarget = _devices.DeviceContext.CreateCompatibleRenderTarget(
                    (Size?)null,
                    new SizeI((int)totalWidth, (int)totalHeight),
                    new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                    CompatibleRenderTargetOptions.None
                );
                _disposer.Collect(_splashRenderTarget);
            }
        }

        public void BeginDrawSplash()
        {
            _splashGlowEffect?.SetInput(0, null, true);
            _splashRenderTarget?.BeginDraw();
            _splashRenderTarget?.Clear(null);
        }

        public void DrawSplashInstance(Ellipse ellipse, ID2D1SolidColorBrush brush)
        {
            _splashRenderTarget?.FillEllipse(ellipse, brush);
        }

        public void EndDrawSplash(float splashSize)
        {
            _splashRenderTarget?.EndDraw();

            if (_splashGlowEffect != null && _splashRenderTarget != null)
            {
                _splashGlowEffect.SetInput(0, _splashRenderTarget.Bitmap, true);
                _splashGlowEffect.SetValue((int)GaussianBlurProperties.StandardDeviation, splashSize / 3.0f);
            }
        }

        public void Dispose()
        {
            _disposer.DisposeAndClear();
            GC.SuppressFinalize(this);
        }
    }
}
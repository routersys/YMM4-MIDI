using MIDI.Shape.MidiPianoRoll.Effects.Default;
using MIDI.Shape.MidiPianoRoll.Models;
using MIDI.Shape.MidiPianoRoll.Rendering;
using System;
using System.Linq;
using System.Numerics;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace MIDI.Shape.MidiPianoRoll.Effects.Default
{
    internal class NoteSplashEffect : IEffect
    {
        private readonly EffectRenderer _effectRenderer;
        private NoteSplashEffectParameter? _parameter;
        private double _currentSplashSize;

        public NoteSplashEffect(IGraphicsDevicesAndContext devices)
        {
            _effectRenderer = new EffectRenderer(devices);
        }

        public void Update(TimelineItemSourceDescription desc, EffectParameterBase parameter)
        {
            if (_parameter == null || !object.ReferenceEquals(_parameter, parameter))
            {
                _parameter = parameter as NoteSplashEffectParameter;
            }
            if (_parameter == null) return;

            double progress = desc.ItemDuration.Frame > 0 ? (double)desc.ItemPosition.Frame / desc.ItemDuration.Frame : 0;
            _currentSplashSize = _parameter.NoteSplashEffectSize.GetValue(progress);

            _effectRenderer.CheckRenderTarget(desc.ScreenSize.Width, desc.ScreenSize.Height);
        }

        public void Draw(ID2D1DeviceContext dc, TimelineItemSourceDescription desc, TimeSpan midiTime, MidiPianoRollParameter globalParameter, MidiDataManager midiDataManager, Direct2DResourceProvider resourceProvider, EffectParameterBase effectParameter)
        {
            Update(desc, effectParameter);

            if (_parameter == null || resourceProvider.NoteSplashGlowBrush == null) return;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;
            var currentTime = midiTime;

            float splashSize = (float)_currentSplashSize;
            if (splashSize <= 0) return;

            var displayDurationSecs = globalParameter.DisplayDuration.GetValue(frame, length, fps);
            var displayDuration = TimeSpan.FromSeconds(displayDurationSecs);
            if (displayDuration.TotalSeconds <= 0) return;

            int minNote = (int)globalParameter.MinNote.GetValue(frame, length, fps);
            int maxNote = (int)globalParameter.MaxNote.GetValue(frame, length, fps);
            int noteRange = maxNote - minNote + 1;
            if (noteRange <= 0) noteRange = 1;

            float totalWidth = desc.ScreenSize.Width;
            float totalHeight = desc.ScreenSize.Height;
            bool invertVertical = globalParameter.InvertVertical;

            var playingNotes = midiDataManager.GetPlayingNotes(currentTime);
            if (playingNotes.Count == 0) return;

            _effectRenderer.BeginDrawSplash();
            bool hasSplash = false;

            if (globalParameter.Orientation == PianoRollOrientation.Horizontal)
            {
                float keyAreaWidth = totalWidth * (float)(globalParameter.KeySize.GetValue(frame, length, fps) / 100.0);
                float keyHeight = totalHeight / noteRange;
                if (keyHeight <= 0) return;

                float baseNoteRange = maxNote - minNote + 1;
                float baseHeight = keyHeight * baseNoteRange;
                float yOffset = (totalHeight - baseHeight) / 2f;

                foreach (int note in playingNotes)
                {
                    float noteY = invertVertical ? yOffset + (note - minNote) * keyHeight : yOffset + (maxNote - note) * keyHeight;
                    float yCenter = noteY + keyHeight / 2.0f;
                    float radius = splashSize / 2.0f;
                    _effectRenderer.DrawSplashInstance(new Ellipse(new Vector2(keyAreaWidth, yCenter), radius, radius), resourceProvider.NoteSplashGlowBrush!);
                    hasSplash = true;
                }
            }
            else
            {
                float keyAreaHeight = totalHeight * (float)(globalParameter.KeySize.GetValue(frame, length, fps) / 100.0);
                float rollAreaHeight = totalHeight - keyAreaHeight;
                float keyWidth = totalWidth / noteRange;
                if (keyWidth <= 0) return;

                float baseNoteRange = maxNote - minNote + 1;
                float baseWidth = keyWidth * baseNoteRange;
                float xOffset = (totalWidth - baseWidth) / 2f;

                foreach (int note in playingNotes)
                {
                    float noteX = invertVertical ? xOffset + (maxNote - note) * keyWidth : xOffset + (note - minNote) * keyWidth;
                    float xCenter = noteX + keyWidth / 2.0f;
                    float radius = splashSize / 2.0f;
                    _effectRenderer.DrawSplashInstance(new Ellipse(new Vector2(xCenter, rollAreaHeight), radius, radius), resourceProvider.NoteSplashGlowBrush!);
                    hasSplash = true;
                }
            }

            if (hasSplash)
            {
                _effectRenderer.EndDrawSplash(splashSize);
                if (_effectRenderer.Output != null)
                {
                    dc.DrawImage(_effectRenderer.Output);
                }
            }
        }

        public void Dispose()
        {
            _effectRenderer?.Dispose();
        }
    }
}
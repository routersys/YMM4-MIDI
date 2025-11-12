using MIDI.Shape.MidiPianoRoll.Models;
using MIDI.Shape.MidiPianoRoll.Rendering;
using System;
using System.Linq;
using System.Numerics;
using Vortice.Direct2D1;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;

namespace MIDI.Shape.MidiPianoRoll.Effects.Default
{
    internal class NoteHitEffect : IEffect
    {
        private NoteHitEffectParameter? _parameter;
        private double _currentHitDurationSecs;

        public NoteHitEffect(IGraphicsDevicesAndContext devices)
        {
        }

        public void Update(TimelineItemSourceDescription desc, EffectParameterBase parameter)
        {
            if (_parameter == null || !object.ReferenceEquals(_parameter, parameter))
            {
                _parameter = parameter as NoteHitEffectParameter;
            }
            if (_parameter == null) return;

            double progress = desc.ItemDuration.Frame > 0 ? (double)desc.ItemPosition.Frame / desc.ItemDuration.Frame : 0;
            _currentHitDurationSecs = _parameter.NoteHitEffectDuration.GetValue(progress);
        }

        public void Draw(ID2D1DeviceContext dc, TimelineItemSourceDescription desc, TimeSpan midiTime, MidiPianoRollParameter globalParameter, MidiDataManager midiDataManager, Direct2DResourceProvider resourceProvider, EffectParameterBase effectParameter)
        {
            Update(desc, effectParameter);

            if (_parameter == null || resourceProvider.NoteHitGlowBrush == null) return;

            var frame = desc.ItemPosition.Frame;
            var length = desc.ItemDuration.Frame;
            var fps = desc.FPS;
            var currentTime = midiTime;

            float hitDurationSecs = (float)_currentHitDurationSecs;
            var hitDuration = TimeSpan.FromSeconds(hitDurationSecs);
            var hitNotes = midiDataManager.Notes
                .Where(n => currentTime >= n.StartTime && currentTime < n.StartTime + hitDuration)
                .Select(n => n.NoteNumber)
                .ToHashSet();

            if (hitNotes.Count == 0) return;

            int minNote = (int)globalParameter.MinNote.GetValue(frame, length, fps);
            int maxNote = (int)globalParameter.MaxNote.GetValue(frame, length, fps);
            int noteRange = maxNote - minNote + 1;
            if (noteRange <= 0) noteRange = 1;

            float totalWidth = desc.ScreenSize.Width;
            float totalHeight = desc.ScreenSize.Height;
            bool invertVertical = globalParameter.InvertVertical;
            bool invertKeyboard = globalParameter.InvertKeyboard;

            if (globalParameter.Orientation == PianoRollOrientation.Horizontal)
            {
                float keyAreaWidth = totalWidth * (float)(globalParameter.KeySize.GetValue(frame, length, fps) / 100.0);
                float keyHeight = totalHeight / noteRange;
                if (keyHeight <= 0) return;

                float blackKeyWidthRatio = 0.6f;
                float whiteKeyWidth = keyAreaWidth;
                float blackKeyWidth = whiteKeyWidth * blackKeyWidthRatio;

                float baseNoteRange = maxNote - minNote + 1;
                float baseHeight = keyHeight * baseNoteRange;
                float yOffset = (totalHeight - baseHeight) / 2f;

                foreach (int note in hitNotes)
                {
                    float y = invertVertical ? yOffset + (note - minNote) * keyHeight : yOffset + (maxNote - note) * keyHeight;
                    if (y + keyHeight < 0 || y > totalHeight) continue;

                    int noteInOctave = note % 12;
                    bool isBlack = new[] { 1, 3, 6, 8, 10 }.Contains(noteInOctave);
                    float currentKeyWidth = isBlack ? blackKeyWidth : whiteKeyWidth;

                    float keyTop = y;
                    float keyBottom = y + keyHeight;
                    float keyLeft = invertKeyboard && isBlack ? whiteKeyWidth - blackKeyWidth : 0;
                    float keyRight = keyLeft + currentKeyWidth;

                    var keyRect = new Vortice.RawRectF(keyLeft, keyTop, keyRight, keyBottom);
                    dc.FillRectangle(keyRect, resourceProvider.NoteHitGlowBrush);
                }
            }
            else
            {
                float keyAreaHeight = totalHeight * (float)(globalParameter.KeySize.GetValue(frame, length, fps) / 100.0);
                float rollAreaHeight = totalHeight - keyAreaHeight;
                float keyWidth = totalWidth / noteRange;
                if (keyWidth <= 0) return;

                float blackKeyHeightRatio = 0.6f;
                float whiteKeyHeight = keyAreaHeight;
                float blackKeyHeight = whiteKeyHeight * blackKeyHeightRatio;

                float baseNoteRange = maxNote - minNote + 1;
                float baseWidth = keyWidth * baseNoteRange;
                float xOffset = (totalWidth - baseWidth) / 2f;

                foreach (int note in hitNotes)
                {
                    float x = invertVertical ? xOffset + (maxNote - note) * keyWidth : xOffset + (note - minNote) * keyWidth;
                    if (x + keyWidth < 0 || x > totalWidth) continue;

                    int noteInOctave = note % 12;
                    bool isBlack = new[] { 1, 3, 6, 8, 10 }.Contains(noteInOctave);
                    float currentKeyWidth = keyWidth;
                    float currentKeyHeight = isBlack ? blackKeyHeight : whiteKeyHeight;

                    float keyLeft = x;
                    float keyRight = x + currentKeyWidth;
                    float keyTop = (invertKeyboard && isBlack ? whiteKeyHeight - blackKeyHeight : 0) + rollAreaHeight;
                    float keyBottom = keyTop + currentKeyHeight;

                    var keyRect = new Vortice.RawRectF(keyLeft, keyTop, keyRight, keyBottom);
                    dc.FillRectangle(keyRect, resourceProvider.NoteHitGlowBrush);
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
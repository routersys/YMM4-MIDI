using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MIDI.Shape.MidiPianoRoll.Models;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace MIDI.Shape.MidiPianoRoll.Rendering
{
    public class KeyRenderer
    {
        private readonly MidiPianoRollParameter _parameter;
        private readonly Direct2DResourceProvider _resources;
        private readonly MidiDataManager _midiDataManager;

        public KeyRenderer(MidiPianoRollParameter parameter, Direct2DResourceProvider resources, MidiDataManager midiDataManager)
        {
            _parameter = parameter;
            _resources = resources;
            _midiDataManager = midiDataManager;
        }

        public void DrawKeysHorizontal(ID2D1DeviceContext dc, float totalWidth, float totalHeight, float keyWidth, TimeSpan currentTime, int minNote, int maxNote, float keyHeight, int frame, int length, double fps)
        {
            if (_resources.WhiteKeyGradientBrush == null || _resources.BlackKeyGradientBrush == null || _resources.PlayingKeyHighlightBrush == null ||
                _resources.KeySeparatorBrush == null || _resources.PressedKeyDarkEdgeBrush == null) return;

            if (keyHeight <= 0) return;

            var playingNoteInfos = _midiDataManager.GetPlayingNoteInfos(currentTime);
            var playingNotesLookup = playingNoteInfos.ToLookup(n => n.NoteNumber);

            bool invertVertical = _parameter.InvertVertical;
            bool invertKeyboard = _parameter.InvertKeyboard;
            bool enableHighlight = _parameter.EnableKeyHighlight;
            bool syncColor = _parameter.KeyColorSync == KeyColorSyncMode.SyncChannel;

            float blackKeyWidthRatio = 0.6f;
            float blackKeyHeightRatio = 1.0f;
            float whiteKeyWidth = keyWidth;
            float blackKeyWidth = whiteKeyWidth * blackKeyWidthRatio;
            float darkEdgeWidth = 2f;

            float baseNoteRange = maxNote - minNote + 1;
            float baseHeight = keyHeight * baseNoteRange;
            float yOffset = (totalHeight - baseHeight) / 2f;

            for (int note = 0; note <= 127; note++)
            {
                float y;
                if (invertVertical)
                {
                    y = yOffset + (note - minNote) * keyHeight;
                }
                else
                {
                    y = yOffset + (maxNote - note) * keyHeight;
                }

                if (y + keyHeight < 0 || y > totalHeight) continue;

                int noteInOctave = note % 12;
                bool isBlack = new[] { 1, 3, 6, 8, 10 }.Contains(noteInOctave);
                float currentKeyWidth = isBlack ? blackKeyWidth : whiteKeyWidth;
                float currentKeyHeight = isBlack ? (keyHeight * blackKeyHeightRatio) : keyHeight;

                float keyTop = y;
                float keyBottom = y + currentKeyHeight;
                float keyLeft = invertKeyboard && isBlack ? whiteKeyWidth - blackKeyWidth : 0;
                float keyRight = keyLeft + currentKeyWidth;
                bool isPlaying = playingNotesLookup.Contains(note);

                ID2D1Brush fillBrush = isBlack ? _resources.BlackKeyGradientBrush : _resources.WhiteKeyGradientBrush;

                _resources.WhiteKeyGradientBrush.StartPoint = new Vector2(0, keyTop);
                _resources.WhiteKeyGradientBrush.EndPoint = new Vector2(0, keyBottom);
                _resources.BlackKeyGradientBrush.StartPoint = new Vector2(0, keyTop);
                _resources.BlackKeyGradientBrush.EndPoint = new Vector2(0, keyBottom);

                var keyRect = new Vortice.RawRectF(keyLeft, keyTop, keyRight, keyBottom);

                dc.FillRectangle(keyRect, fillBrush);

                if (isPlaying)
                {
                    if (syncColor)
                    {
                        var noteInfo = playingNotesLookup[note].FirstOrDefault();
                        if (noteInfo != null)
                        {
                            var channelBrush = _resources.GetNoteBrush(noteInfo);
                            dc.FillRectangle(keyRect, channelBrush);
                        }
                    }
                    else if (enableHighlight)
                    {
                        dc.FillRectangle(keyRect, _resources.PlayingKeyHighlightBrush);
                    }
                    else
                    {
                        var darkEdgeRect = new Vortice.RawRectF(keyLeft, keyTop, keyLeft + darkEdgeWidth, keyBottom);
                        dc.FillRectangle(darkEdgeRect, _resources.PressedKeyDarkEdgeBrush);
                    }
                }

                dc.DrawRectangle(keyRect, _resources.KeySeparatorBrush, 0.5f);
            }
        }

        public void DrawKeysVertical(ID2D1DeviceContext dc, float totalWidth, float totalHeight, float keyAreaHeight, float rollAreaHeight, TimeSpan currentTime, int minNote, int maxNote, float keyWidth, int frame, int length, double fps)
        {
            if (_resources.WhiteKeyGradientBrush == null || _resources.BlackKeyGradientBrush == null || _resources.PlayingKeyHighlightBrush == null ||
                _resources.KeySeparatorBrush == null || _resources.PressedKeyDarkEdgeBrush == null) return;

            if (keyWidth <= 0) return;

            var playingNoteInfos = _midiDataManager.GetPlayingNoteInfos(currentTime);
            var playingNotesLookup = playingNoteInfos.ToLookup(n => n.NoteNumber);

            bool invertVertical = _parameter.InvertVertical;
            bool invertKeyboard = _parameter.InvertKeyboard;
            bool enableHighlight = _parameter.EnableKeyHighlight;
            bool syncColor = _parameter.KeyColorSync == KeyColorSyncMode.SyncChannel;

            float blackKeyHeightRatio = 0.6f;
            float blackKeyWidthRatio = 1.0f;
            float whiteKeyHeight = keyAreaHeight;
            float blackKeyHeight = whiteKeyHeight * blackKeyHeightRatio;
            float darkEdgeHeight = 2f;

            float baseNoteRange = maxNote - minNote + 1;
            float baseWidth = keyWidth * baseNoteRange;
            float xOffset = (totalWidth - baseWidth) / 2f;

            for (int note = 0; note <= 127; note++)
            {
                float x;
                if (invertVertical)
                {
                    x = xOffset + (maxNote - note) * keyWidth;
                }
                else
                {
                    x = xOffset + (note - minNote) * keyWidth;
                }

                if (x + keyWidth < 0 || x > totalWidth) continue;

                int noteInOctave = note % 12;
                bool isBlack = new[] { 1, 3, 6, 8, 10 }.Contains(noteInOctave);
                float currentKeyWidth = isBlack ? (keyWidth * blackKeyWidthRatio) : keyWidth;
                float currentKeyHeight = isBlack ? blackKeyHeight : whiteKeyHeight;

                float keyLeft = x;
                float keyRight = x + currentKeyWidth;
                float keyTop = (invertKeyboard && isBlack ? whiteKeyHeight - blackKeyHeight : 0) + rollAreaHeight;
                float keyBottom = keyTop + currentKeyHeight;
                bool isPlaying = playingNotesLookup.Contains(note);

                ID2D1Brush fillBrush = isBlack ? _resources.BlackKeyGradientBrush : _resources.WhiteKeyGradientBrush;

                _resources.WhiteKeyGradientBrush.StartPoint = new Vector2(keyLeft, 0);
                _resources.WhiteKeyGradientBrush.EndPoint = new Vector2(keyRight, 0);
                _resources.BlackKeyGradientBrush.StartPoint = new Vector2(keyLeft, 0);
                _resources.BlackKeyGradientBrush.EndPoint = new Vector2(keyRight, 0);

                var keyRect = new Vortice.RawRectF(keyLeft, keyTop, keyRight, keyBottom);

                dc.FillRectangle(keyRect, fillBrush);

                if (isPlaying)
                {
                    if (syncColor)
                    {
                        var noteInfo = playingNotesLookup[note].FirstOrDefault();
                        if (noteInfo != null)
                        {
                            var channelBrush = _resources.GetNoteBrush(noteInfo);
                            dc.FillRectangle(keyRect, channelBrush);
                        }
                    }
                    else if (enableHighlight)
                    {
                        dc.FillRectangle(keyRect, _resources.PlayingKeyHighlightBrush);
                    }
                    else
                    {
                        var darkEdgeRect = new Vortice.RawRectF(keyLeft, keyTop, keyRight, keyTop + darkEdgeHeight);
                        dc.FillRectangle(darkEdgeRect, _resources.PressedKeyDarkEdgeBrush);
                    }
                }

                dc.DrawRectangle(keyRect, _resources.KeySeparatorBrush, 0.5f);
            }
        }
    }
}
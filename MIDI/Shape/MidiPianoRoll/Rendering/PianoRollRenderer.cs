using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using MIDI.Shape.MidiPianoRoll.Models;
using NAudio.Midi;
using Vortice.Direct2D1;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;

namespace MIDI.Shape.MidiPianoRoll.Rendering
{
    public class PianoRollRenderer
    {
        private readonly MidiPianoRollParameter _parameter;
        private readonly Direct2DResourceProvider _resources;
        private readonly MidiDataManager _midiDataManager;

        public PianoRollRenderer(MidiPianoRollParameter parameter, Direct2DResourceProvider resources, MidiDataManager midiDataManager)
        {
            _parameter = parameter;
            _resources = resources;
            _midiDataManager = midiDataManager;
        }

        public void DrawPianoRollHorizontal(ID2D1DeviceContext dc, float totalWidth, float totalHeight, float keyAreaWidth, float rollWidth, TimeSpan currentTime, TimeSpan startTime, TimeSpan endTime, TimeSpan displayDuration, int minNote, int maxNote, float keyHeight, TimeSpan renderStartTime, TimeSpan renderEndTime, int frame, int length, double fps)
        {
            if (_resources.NoteBrush == null || _resources.LineBrush == null || _resources.GridLineBrush == null || _resources.WhiteKeyBrush == null || _resources.BlackKeyBrush == null || _resources.GridStrokeStyle == null) return;

            if (keyHeight <= 0) return;

            bool invertVertical = _parameter.InvertVertical;

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

                if (y < 0 || y >= totalHeight) continue;

                if (_parameter.ShowHorizontalLines)
                {
                    dc.DrawLine(new Vector2(keyAreaWidth, y + keyHeight), new Vector2(totalWidth, y + keyHeight), _resources.LineBrush, 0.5f);
                }
            }

            DrawVerticalGridLines(dc, keyAreaWidth, 0, rollWidth, totalHeight, currentTime, startTime, endTime, displayDuration, _midiDataManager.MidiFile, _midiDataManager.TempoMap);

            if (displayDuration.TotalSeconds <= 0) return;

            var extraDurationSecs = (renderEndTime - renderStartTime).TotalSeconds - displayDuration.TotalSeconds;
            var bufferBeforeSecs = extraDurationSecs / 2.0;
            var bufferAfterSecs = extraDurationSecs / 2.0;

            float extraWidthAfter = (float)(bufferAfterSecs / displayDuration.TotalSeconds) * rollWidth;

            dc.PushAxisAlignedClip(new Vortice.RawRectF(keyAreaWidth, 0, totalWidth + extraWidthAfter, totalHeight), AntialiasMode.PerPrimitive);

            var notes = _midiDataManager.GetNotesInRange(renderStartTime, renderEndTime);
            var noteRects = new List<(NoteEventInfo note, Vortice.RawRectF rect)>();
            foreach (var note in notes)
            {
                var noteEndTime = note.StartTime + note.Duration;

                float noteY;
                if (invertVertical)
                {
                    noteY = yOffset + (note.NoteNumber - minNote) * keyHeight;
                }
                else
                {
                    noteY = yOffset + (maxNote - note.NoteNumber) * keyHeight;
                }

                float startX = (float)((note.StartTime - startTime).TotalSeconds / displayDuration.TotalSeconds);
                float endX = (float)((noteEndTime - startTime).TotalSeconds / displayDuration.TotalSeconds);

                float noteX = keyAreaWidth + startX * rollWidth;
                float noteWidth = (endX - startX) * rollWidth;
                noteWidth = Math.Max(1.0f, noteWidth);

                ID2D1SolidColorBrush noteBrush = _resources.GetNoteBrush(note);

                var noteRect = new Vortice.RawRectF(noteX, noteY, noteX + noteWidth, noteY + keyHeight);
                noteRects.Add((note, noteRect));

                if (noteRect.Bottom < 0 || noteRect.Top > totalHeight) continue;

                dc.FillRectangle(noteRect, noteBrush);
                dc.DrawRectangle(noteRect, _resources.BlackKeyBrush, 0.5f);
            }

            dc.PopAxisAlignedClip();
        }

        public void DrawPianoRollVertical(ID2D1DeviceContext dc, float totalWidth, float totalHeight, float keyAreaHeight, float rollHeight, TimeSpan currentTime, TimeSpan startTime, TimeSpan endTime, TimeSpan displayDuration, int minNote, int maxNote, float keyWidth, TimeSpan renderStartTime, TimeSpan renderEndTime, int frame, int length, double fps)
        {
            if (_resources.NoteBrush == null || _resources.LineBrush == null || _resources.GridLineBrush == null || _resources.WhiteKeyBrush == null || _resources.BlackKeyBrush == null || _resources.GridStrokeStyle == null) return;

            if (keyWidth <= 0) return;

            bool invertVertical = _parameter.InvertVertical;

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

                if (x < 0 || x >= totalWidth) continue;

                if (_parameter.ShowHorizontalLines)
                {
                    dc.DrawLine(new Vector2(x + keyWidth, 0), new Vector2(x + keyWidth, rollHeight), _resources.LineBrush, 0.5f);
                }
            }

            DrawHorizontalGridLines(dc, 0, 0, totalWidth, rollHeight, currentTime, startTime, endTime, displayDuration, _midiDataManager.MidiFile, _midiDataManager.TempoMap);

            if (displayDuration.TotalSeconds <= 0) return;

            var extraDurationSecs = (renderEndTime - renderStartTime).TotalSeconds - displayDuration.TotalSeconds;
            var bufferBeforeSecs = extraDurationSecs / 2.0;
            var bufferAfterSecs = extraDurationSecs / 2.0;

            float extraHeightAfter = (float)(bufferAfterSecs / displayDuration.TotalSeconds) * rollHeight;

            dc.PushAxisAlignedClip(new Vortice.RawRectF(0, 0 - extraHeightAfter, totalWidth, rollHeight), AntialiasMode.PerPrimitive);

            var notes = _midiDataManager.GetNotesInRange(renderStartTime, renderEndTime);
            var noteRects = new List<(NoteEventInfo note, Vortice.RawRectF rect)>();
            foreach (var note in notes)
            {
                var noteEndTime = note.StartTime + note.Duration;

                float noteX;
                if (invertVertical)
                {
                    noteX = xOffset + (maxNote - note.NoteNumber) * keyWidth;
                }
                else
                {
                    noteX = xOffset + (note.NoteNumber - minNote) * keyWidth;
                }

                float startYPos = (float)((note.StartTime - startTime).TotalSeconds / displayDuration.TotalSeconds);
                float endYPos = (float)((noteEndTime - startTime).TotalSeconds / displayDuration.TotalSeconds);

                float noteY = (1.0f - endYPos) * rollHeight;
                float noteHeight = (endYPos - startYPos) * rollHeight;
                noteHeight = Math.Max(1.0f, noteHeight);

                ID2D1SolidColorBrush noteBrush = _resources.GetNoteBrush(note);

                var noteRect = new Vortice.RawRectF(noteX, noteY, noteX + keyWidth, noteY + noteHeight);
                noteRects.Add((note, noteRect));

                if (noteRect.Right < 0 || noteRect.Left > totalWidth) continue;

                dc.FillRectangle(noteRect, noteBrush);
                dc.DrawRectangle(noteRect, _resources.BlackKeyBrush, 0.5f);
            }

            dc.PopAxisAlignedClip();
        }


        private void DrawVerticalGridLines(ID2D1DeviceContext dc, float xOffset, float yOffset, float width, float height, TimeSpan currentTime, TimeSpan startTime, TimeSpan endTime, TimeSpan displayDuration, MidiFile? midiFile, List<TempoEvent>? tempoMap)
        {
            if (midiFile == null || tempoMap == null || _parameter.VerticalGridLine == VerticalGridLineType.None || _resources.GridLineBrush == null || _resources.GridStrokeStyle == null) return;

            double ticksPerBeat = midiFile.DeltaTicksPerQuarterNote;
            double gridTicks = GetGridTicks(ticksPerBeat);

            if (gridTicks > 0 && displayDuration.TotalSeconds > 0)
            {
                long startTick = MidiProcessor.TimeToTicks(startTime, midiFile.DeltaTicksPerQuarterNote, tempoMap);
                long endTick = MidiProcessor.TimeToTicks(endTime, midiFile.DeltaTicksPerQuarterNote, tempoMap);

                long firstVisibleTick = (long)Math.Floor(startTick / gridTicks) * (long)gridTicks;

                for (long tick = firstVisibleTick; tick <= endTick + gridTicks; tick += (long)gridTicks)
                {
                    var time = MidiProcessor.TicksToTimeSpan(tick, midiFile.DeltaTicksPerQuarterNote, tempoMap);
                    if (time >= startTime && time <= endTime)
                    {
                        float x = xOffset + (float)((time - startTime).TotalSeconds / displayDuration.TotalSeconds) * width;
                        if (x >= xOffset && x <= xOffset + width)
                        {
                            dc.DrawLine(new Vector2(x, yOffset), new Vector2(x, yOffset + height), _resources.GridLineBrush, 0.5f, _resources.GridStrokeStyle);
                        }
                    }
                }
            }
        }

        private void DrawHorizontalGridLines(ID2D1DeviceContext dc, float xOffset, float yOffset, float width, float height, TimeSpan currentTime, TimeSpan startTime, TimeSpan endTime, TimeSpan displayDuration, MidiFile? midiFile, List<TempoEvent>? tempoMap)
        {
            if (midiFile == null || tempoMap == null || _parameter.VerticalGridLine == VerticalGridLineType.None || _resources.GridLineBrush == null || _resources.GridStrokeStyle == null) return;

            double ticksPerBeat = midiFile.DeltaTicksPerQuarterNote;
            double gridTicks = GetGridTicks(ticksPerBeat);

            if (gridTicks > 0 && displayDuration.TotalSeconds > 0)
            {
                long startTick = MidiProcessor.TimeToTicks(startTime, midiFile.DeltaTicksPerQuarterNote, tempoMap);
                long endTick = MidiProcessor.TimeToTicks(endTime, midiFile.DeltaTicksPerQuarterNote, tempoMap);

                long firstVisibleTick = (long)Math.Floor(startTick / gridTicks) * (long)gridTicks;

                for (long tick = firstVisibleTick; tick <= endTick + gridTicks; tick += (long)gridTicks)
                {
                    var time = MidiProcessor.TicksToTimeSpan(tick, midiFile.DeltaTicksPerQuarterNote, tempoMap);
                    if (time >= startTime && time <= endTime)
                    {
                        float y_percent = (float)((time - startTime).TotalSeconds / displayDuration.TotalSeconds);
                        float y = yOffset + (1.0f - y_percent) * height;
                        if (y >= yOffset && y <= yOffset + height)
                        {
                            dc.DrawLine(new Vector2(xOffset, y), new Vector2(xOffset + width, y), _resources.GridLineBrush, 0.5f, _resources.GridStrokeStyle);
                        }
                    }
                }
            }
        }


        private double GetGridTicks(double ticksPerBeat)
        {
            return _parameter.VerticalGridLine switch
            {
                VerticalGridLineType.Whole => ticksPerBeat * 4,
                VerticalGridLineType.Half => ticksPerBeat * 2,
                VerticalGridLineType.Quarter => ticksPerBeat,
                VerticalGridLineType.Eighth => ticksPerBeat / 2,
                VerticalGridLineType.Sixteenth => ticksPerBeat / 4,
                VerticalGridLineType.ThirtySecond => ticksPerBeat / 8,
                _ => 0
            };
        }
    }
}
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using YukkuriMovieMaker.Plugin.FileSource;
using MeltySynth;
using NAudio.Midi;

namespace MIDI
{
    public class MidiAudioSource : IAudioFileSource
    {
        public TimeSpan Duration { get; }
        public int Hz => 44100;

        private long position = 0;
        private readonly float[] audioBuffer;

        public MidiAudioSource(string filePath)
        {
            var assemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (assemblyLocation == null)
            {
                throw new DirectoryNotFoundException("プラグインのインストール先フォルダが見つかりません。");
            }

            var sf2Path = Directory.GetFiles(assemblyLocation, "*.sf2").FirstOrDefault();

            if (sf2Path != null && File.Exists(sf2Path))
            {
                var midiFile = new MeltySynth.MidiFile(filePath);
                Duration = midiFile.Length;

                var synthesizer = new Synthesizer(sf2Path, Hz);
                var sequencer = new MidiFileSequencer(synthesizer);
                sequencer.Play(midiFile, false);

                var totalSamplesToRender = (long)(Duration.TotalSeconds * Hz) * 2;
                var audioDataList = new List<float>((int)totalSamplesToRender);

                var leftBuffer = new float[1024];
                var rightBuffer = new float[1024];

                while (audioDataList.Count < totalSamplesToRender)
                {
                    sequencer.Render(leftBuffer, rightBuffer);
                    for (int i = 0; i < leftBuffer.Length; i++)
                    {
                        audioDataList.Add(leftBuffer[i]);
                        audioDataList.Add(rightBuffer[i]);
                    }
                }

                audioBuffer = audioDataList.Take((int)totalSamplesToRender).ToArray();
            }
            else
            {
                var midiFile = new NAudio.Midi.MidiFile(filePath, false);
                var ticksPerQuarterNote = midiFile.DeltaTicksPerQuarterNote;
                var tempoMap = midiFile.Events.SelectMany(track => track)
                                              .OfType<TempoEvent>()
                                              .OrderBy(e => e.AbsoluteTime)
                                              .ToList();
                if (!tempoMap.Any() || tempoMap.First().AbsoluteTime > 0)
                {
                    tempoMap.Insert(0, new TempoEvent(500000, 0));
                }

                var noteEvents = new List<NoteEvent>();
                for (int i = 0; i < midiFile.Events.Tracks; i++)
                {
                    foreach (var midiEvent in midiFile.Events[i])
                    {
                        if (midiEvent is NoteOnEvent noteOn && noteOn.OffEvent != null)
                        {
                            noteEvents.Add(new NoteEvent(noteOn, ticksPerQuarterNote, tempoMap));
                        }
                    }
                }

                long totalTicks = noteEvents.Any() ? noteEvents.Max(e => e.EndTicks) : 0;
                Duration = TicksToTimeSpan(totalTicks, ticksPerQuarterNote, tempoMap);

                var totalSamples = (long)(Duration.TotalSeconds * Hz);
                audioBuffer = new float[totalSamples * 2];

                RenderAudioWithSineWaves(noteEvents);
                NormalizeAudio();
            }
        }

        private void RenderAudioWithSineWaves(List<NoteEvent> notes)
        {
            foreach (var note in notes)
            {
                var frequency = 440.0 * Math.Pow(2.0, (note.NoteNumber - 69.0) / 12.0);
                var amplitude = note.Velocity / 127.0f;
                var startSample = (long)(note.StartTime.TotalSeconds * Hz);
                var endSample = (long)(note.EndTime.TotalSeconds * Hz);
                var attackTime = 0.005;
                var releaseTime = 0.01;
                var attackSamples = (long)(attackTime * Hz);
                var releaseSamples = (long)(releaseTime * Hz);

                for (long i = startSample; i < endSample; i++)
                {
                    var time = (i - startSample) / (double)Hz;
                    double envelope = 1.0;
                    if (i - startSample < attackSamples) envelope = (double)(i - startSample) / attackSamples;
                    else if (endSample - i < releaseSamples) envelope = (double)(endSample - i) / releaseSamples;
                    var sampleValue = amplitude * envelope * Math.Sin(2 * Math.PI * frequency * time);
                    if (i * 2 < audioBuffer.Length)
                    {
                        audioBuffer[i * 2] += (float)sampleValue;
                        audioBuffer[i * 2 + 1] += (float)sampleValue;
                    }
                }
            }
        }

        private void NormalizeAudio()
        {
            float maxAbs = 0;
            for (int i = 0; i < audioBuffer.Length; i++)
            {
                var absVal = Math.Abs(audioBuffer[i]);
                if (absVal > maxAbs) maxAbs = absVal;
            }
            if (maxAbs > 1.0f)
            {
                var scale = 0.99f / maxAbs;
                for (int i = 0; i < audioBuffer.Length; i++) audioBuffer[i] *= scale;
            }
        }

        public int Read(float[] destBuffer, int offset, int count)
        {
            var maxCount = (int)Math.Max(0, audioBuffer.Length - position);
            count = Math.Min(count, maxCount);
            if (count > 0)
            {
                Array.Copy(audioBuffer, position, destBuffer, offset, count);
            }
            position += count;
            return count;
        }

        public void Seek(TimeSpan time)
        {
            position = Math.Min((long)(Hz * time.TotalSeconds) * 2, audioBuffer.Length);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private static TimeSpan TicksToTimeSpan(long ticks, int ticksPerQuarterNote, List<TempoEvent> tempoMap)
        {
            double seconds = 0;
            long lastTicks = 0;
            double currentTempo = 500000.0;
            foreach (var tempoEvent in tempoMap)
            {
                if (ticks < tempoEvent.AbsoluteTime) break;
                long deltaTicks = tempoEvent.AbsoluteTime - lastTicks;
                seconds += (deltaTicks / (double)ticksPerQuarterNote) * (currentTempo / 1000000.0);
                currentTempo = tempoEvent.MicrosecondsPerQuarterNote;
                lastTicks = tempoEvent.AbsoluteTime;
            }
            long remainingTicks = ticks - lastTicks;
            seconds += (remainingTicks / (double)ticksPerQuarterNote) * (currentTempo / 1000000.0);
            return TimeSpan.FromSeconds(seconds);
        }

        private class NoteEvent
        {
            public int NoteNumber { get; }
            public int Velocity { get; }
            public long StartTicks { get; }
            public long EndTicks { get; }
            public TimeSpan StartTime { get; }
            public TimeSpan EndTime { get; }

            public NoteEvent(NoteOnEvent noteOn, int ticksPerQuarterNote, List<TempoEvent> tempoMap)
            {
                NoteNumber = noteOn.NoteNumber;
                Velocity = noteOn.Velocity;
                StartTicks = noteOn.AbsoluteTime;
                EndTicks = noteOn.OffEvent.AbsoluteTime;
                StartTime = TicksToTimeSpan(StartTicks, ticksPerQuarterNote, tempoMap);
                EndTime = TicksToTimeSpan(EndTicks, ticksPerQuarterNote, tempoMap);
            }
        }
    }
}
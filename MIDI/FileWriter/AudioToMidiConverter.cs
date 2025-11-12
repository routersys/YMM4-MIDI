using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NAudio.Dsp;
using NAudio.Midi;
using MIDI.Utils;
using Complex = NAudio.Dsp.Complex;

namespace MIDI.FileWriter
{
    public class AudioToMidiConverter
    {
        private readonly int _sampleRate;
        private readonly MidiFileWriterConfigViewModel _config;
        public int TicksPerQuarterNote { get; } = 960;
        private const double Bpm = 120.0;
        private readonly double _secondsPerTick;

        private const int MAX_POLYPHONY = 12;
        private const float MIN_FREQ = 27.5f;
        private const float MAX_FREQ = 4186.0f;
        private const float NOISE_GATE_THRESHOLD = 0.001f;
        private const float HARMONIC_TOLERANCE = 1.03f;

        private class PitchCandidate
        {
            public float Frequency;
            public float Magnitude;
            public double StartTime;
            public double EndTime;
            public int MidiNote;
            public float PeakAmplitude;
            public float AvgAmplitude;
            public int FrameCount;
            public float Confidence;
        }

        public AudioToMidiConverter(int sampleRate, MidiFileWriterConfigViewModel config)
        {
            _sampleRate = sampleRate;
            _config = config;
            _secondsPerTick = 60.0 / (Bpm * TicksPerQuarterNote);
            Logger.Info($"AudioToMidiConverter initialized. SampleRate={_sampleRate}, TicksPerQuarterNote={TicksPerQuarterNote}, SecondsPerTick={_secondsPerTick:F6}", 5);
        }

        private static double[] HannWindow(int length)
        {
            double[] window = new double[length];
            for (int i = 0; i < length; i++)
            {
                window[i] = 0.5 * (1 - Math.Cos((2 * Math.PI * i) / (length - 1)));
            }
            return window;
        }

        public List<MidiEvent> ConvertToMidiEvents(float[] audioData)
        {
            Logger.Info($"ConvertToMidiEvents Starting. Audio data length: {audioData?.Length ?? 0} samples", 4);
            Stopwatch sw = Stopwatch.StartNew();

            if (audioData == null || audioData.Length == 0)
            {
                Logger.Warn("Audio data is empty, cannot generate MIDI events.", 2);
                return CreateEmptyMidiTrack();
            }

            try
            {
                var pitchCandidates = DetectPitchCandidates(audioData);
                Logger.Info($"Pitch candidate detection complete. Candidates found: {pitchCandidates.Count}, Elapsed time: {sw.ElapsedMilliseconds}ms", 4);

                var midiEvents = GeneratePolyphonicMidiEvents(pitchCandidates);
                Logger.Info($"MIDI event generation complete. Event count before Link: {midiEvents.Count}, Elapsed time: {sw.ElapsedMilliseconds}ms", 4);

                var linkedEvents = LinkNoteOnToNoteOff(midiEvents);
                Logger.Info($"LinkNoteOnToNoteOff complete. Event count after Link: {linkedEvents.Count}, Elapsed time: {sw.ElapsedMilliseconds}ms", 4);

                QuantizeEvents(linkedEvents);
                Logger.Info($"Quantization process complete. Event count after quantize: {linkedEvents.Count}, Elapsed time: {sw.ElapsedMilliseconds}ms", 4);

                sw.Stop();
                Logger.Info($"ConvertToMidiEvents Finished. Total elapsed time: {sw.ElapsedMilliseconds}ms", 4);
                return linkedEvents;
            }
            catch (Exception ex)
            {
                Logger.Error("Error in ConvertToMidiEvents", ex);
                return CreateEmptyMidiTrack();
            }
        }

        private List<MidiEvent> CreateEmptyMidiTrack()
        {
            var events = new List<MidiEvent>
            {
                new TempoEvent((int)(60000000 / Bpm), 0),
                new PatchChangeEvent(0, 1, 0),
                new MetaEvent(MetaEventType.EndTrack, 0, TicksPerQuarterNote)
            };
            return events;
        }

        private List<PitchCandidate> DetectPitchCandidates(float[] audioData)
        {
            var candidates = new List<PitchCandidate>();
            int fftSize = _config.FftSize;
            int hopSize = Math.Max(fftSize / 64, 64);

            if (audioData.Length < fftSize)
            {
                Logger.Warn($"Audio data length ({audioData.Length}) is shorter than FFT size ({fftSize}). Skipping detection.", 3);
                return candidates;
            }

            Logger.Info($"DetectPitchCandidates: FFTSize={fftSize}, HopSize={hopSize}", 5);

            var fftBuffer = new Complex[fftSize];
            var window = HannWindow(fftSize);
            var activePitches = new Dictionary<int, PitchCandidate>();

            float rmsThreshold = CalculateNoiseFloor(audioData) * 2.0f;
            float maxAmplitudeOverall = audioData.Max(Math.Abs);
            float smoothedRms = 0.0f;
            const float alpha = 0.85f;

            double minNoteDurationSeconds = Math.Max(_config.MinNoteDurationMs / 1000.0, (double)hopSize / _sampleRate);

            for (int i = 0; i <= audioData.Length - fftSize; i += hopSize)
            {
                float frameRms = CalculateRMS(audioData, i, fftSize);
                smoothedRms = alpha * frameRms + (1.0f - alpha) * smoothedRms;

                if (smoothedRms < rmsThreshold)
                {
                    EndAllActivePitches(activePitches, candidates, minNoteDurationSeconds);
                    continue;
                }

                for (int j = 0; j < fftSize; j++)
                {
                    float windowedSample = audioData[i + j] * (float)window[j];
                    fftBuffer[j].X = windowedSample;
                    fftBuffer[j].Y = 0;
                }

                FastFourierTransform.FFT(true, (int)Math.Log(fftSize, 2.0), fftBuffer);

                List<(float Frequency, float Magnitude, float Confidence)> detectedPitches;

                if (_config.PitchAlgorithm == PitchDetectionAlgorithm.Yin)
                {
                    detectedPitches = DetectPitchesWithYIN(audioData, i, fftSize, hopSize);
                }
                else
                {
                    detectedPitches = DetectPitchesWithFFT(fftBuffer, fftSize);
                }

                double currentTime = (double)i / _sampleRate;
                double frameEndTime = currentTime + (double)hopSize / _sampleRate;

                UpdateActivePitches(detectedPitches, activePitches, candidates, currentTime, frameEndTime,
                                  smoothedRms, minNoteDurationSeconds);
            }

            FinalizeActivePitches(activePitches, candidates, minNoteDurationSeconds);

            Logger.Info($"Pitch detection finished. Max Amplitude={maxAmplitudeOverall:F4}, Found {candidates.Count} candidates.", 4);
            return candidates;
        }

        private float CalculateRMS(float[] audioData, int start, int length)
        {
            float sum = 0;
            int end = Math.Min(start + length, audioData.Length);
            for (int i = start; i < end; i++)
            {
                sum += audioData[i] * audioData[i];
            }
            return (float)Math.Sqrt(sum / length);
        }

        private float CalculateNoiseFloor(float[] audioData)
        {
            const int sampleCount = 10000;
            int step = Math.Max(1, audioData.Length / sampleCount);
            var samples = new List<float>();

            for (int i = 0; i < audioData.Length; i += step)
            {
                samples.Add(Math.Abs(audioData[i]));
            }

            samples.Sort();
            int percentileIndex = (int)(samples.Count * 0.1);
            return samples[Math.Min(percentileIndex, samples.Count - 1)];
        }

        private List<(float Frequency, float Magnitude, float Confidence)> DetectPitchesWithYIN(
            float[] audioData, int startIndex, int bufferSize, int hopSize)
        {
            var results = new List<(float, float, float)>();

            try
            {
                int minPeriod = (int)(_sampleRate / MAX_FREQ);
                int maxPeriod = (int)(_sampleRate / MIN_FREQ);
                maxPeriod = Math.Min(maxPeriod, bufferSize / 2);

                if (minPeriod >= maxPeriod || startIndex + bufferSize > audioData.Length)
                    return results;

                var difference = new float[maxPeriod + 1];
                var cumulativeDifference = new float[maxPeriod + 1];

                for (int tau = minPeriod; tau <= maxPeriod; tau++)
                {
                    float sum = 0;
                    for (int i = 0; i < bufferSize - tau; i++)
                    {
                        float delta = audioData[startIndex + i] - audioData[startIndex + i + tau];
                        sum += delta * delta;
                    }
                    difference[tau] = sum;
                }

                cumulativeDifference[0] = 1.0f;
                float runningSum = 0;
                for (int tau = 1; tau <= maxPeriod; tau++)
                {
                    runningSum += difference[tau];
                    cumulativeDifference[tau] = runningSum > 0 ? difference[tau] / (runningSum / tau) : 1.0f;
                }

                float threshold = (float)(0.2 - _config.PitchSensitivity * 0.15);

                for (int tau = minPeriod; tau < maxPeriod - 1; tau++)
                {
                    if (cumulativeDifference[tau] < threshold &&
                        cumulativeDifference[tau] < cumulativeDifference[tau - 1] &&
                        cumulativeDifference[tau] < cumulativeDifference[tau + 1])
                    {
                        float betterTau = ParabolicInterpolation(cumulativeDifference, tau);
                        float frequency = _sampleRate / betterTau;

                        if (frequency >= MIN_FREQ && frequency <= MAX_FREQ)
                        {
                            float confidence = 1.0f - cumulativeDifference[tau];
                            float magnitude = CalculateRMS(audioData, startIndex, bufferSize);
                            results.Add((frequency, magnitude, confidence));

                            if (results.Count >= MAX_POLYPHONY)
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error in YIN algorithm", ex);
            }

            return results;
        }

        private float ParabolicInterpolation(float[] array, int index)
        {
            if (index < 1 || index >= array.Length - 1)
                return index;

            float s0 = array[index - 1];
            float s1 = array[index];
            float s2 = array[index + 1];

            float adjustment = 0.5f * (s0 - s2) / (s0 - 2.0f * s1 + s2);

            if (float.IsNaN(adjustment) || float.IsInfinity(adjustment))
                return index;

            return index + adjustment;
        }

        private List<(float Frequency, float Magnitude, float Confidence)> DetectPitchesWithFFT(
            Complex[] fftBuffer, int fftSize)
        {
            var results = new List<(float, float, float)>();

            try
            {
                var magnitudes = new float[fftSize / 2];
                float maxMagnitude = 0;

                for (int j = 1; j < fftSize / 2; j++)
                {
                    magnitudes[j] = (float)Math.Sqrt(fftBuffer[j].X * fftBuffer[j].X + fftBuffer[j].Y * fftBuffer[j].Y);
                    if (magnitudes[j] > maxMagnitude) maxMagnitude = magnitudes[j];
                }

                if (maxMagnitude <= 0) return results;

                float threshold = maxMagnitude * 0.1f * (1.0f - (float)_config.PitchSensitivity * 0.5f);
                int minIndex = Math.Max(1, (int)(MIN_FREQ * fftSize / _sampleRate));
                int maxIndex = Math.Min(fftSize / 2 - 1, (int)(MAX_FREQ * fftSize / _sampleRate));

                var peaks = new List<(int index, float magnitude)>();

                for (int j = minIndex + 1; j < maxIndex - 1; j++)
                {
                    if (magnitudes[j] > threshold &&
                        magnitudes[j] > magnitudes[j - 1] &&
                        magnitudes[j] > magnitudes[j + 1])
                    {
                        peaks.Add((j, magnitudes[j]));
                    }
                }

                var filteredPeaks = RemoveHarmonics(peaks, magnitudes, fftSize);

                foreach (var (index, magnitude) in filteredPeaks.OrderByDescending(p => p.magnitude).Take(MAX_POLYPHONY))
                {
                    float refinedIndex = ParabolicInterpolation(magnitudes, index);
                    float frequency = refinedIndex * _sampleRate / fftSize;

                    if (frequency >= MIN_FREQ && frequency <= MAX_FREQ)
                    {
                        float confidence = magnitude / maxMagnitude;
                        results.Add((frequency, magnitude, confidence));
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error in FFT pitch detection", ex);
            }

            return results;
        }

        private List<(int index, float magnitude)> RemoveHarmonics(
            List<(int index, float magnitude)> peaks, float[] magnitudes, int fftSize)
        {
            var filtered = new List<(int index, float magnitude)>();
            var sortedPeaks = peaks.OrderByDescending(p => p.magnitude).ToList();

            foreach (var peak in sortedPeaks)
            {
                bool isHarmonic = false;

                foreach (var fundamental in filtered)
                {
                    float ratio = (float)peak.index / fundamental.index;
                    float nearestHarmonic = (float)Math.Round(ratio);

                    if (nearestHarmonic >= 2 && Math.Abs(ratio - nearestHarmonic) < 0.05f)
                    {
                        if (peak.magnitude < fundamental.magnitude * 0.8f)
                        {
                            isHarmonic = true;
                            break;
                        }
                    }
                }

                if (!isHarmonic)
                {
                    filtered.Add(peak);
                }
            }

            return filtered;
        }

        private void UpdateActivePitches(
            List<(float Frequency, float Magnitude, float Confidence)> detectedPitches,
            Dictionary<int, PitchCandidate> activePitches,
            List<PitchCandidate> candidates,
            double currentTime,
            double frameEndTime,
            float smoothedRms,
            double minNoteDurationSeconds)
        {
            var currentFramePitches = new HashSet<int>();

            foreach (var (frequency, magnitude, confidence) in detectedPitches)
            {
                int midiNote = FrequencyToMidiNote(frequency);
                if (midiNote < 0 || midiNote > 127) continue;

                currentFramePitches.Add(midiNote);

                if (activePitches.TryGetValue(midiNote, out var existingCandidate))
                {
                    existingCandidate.EndTime = frameEndTime;
                    existingCandidate.PeakAmplitude = Math.Max(existingCandidate.PeakAmplitude, smoothedRms);
                    existingCandidate.AvgAmplitude = (existingCandidate.AvgAmplitude * existingCandidate.FrameCount + smoothedRms)
                                                    / (existingCandidate.FrameCount + 1);
                    existingCandidate.FrameCount++;
                    existingCandidate.Confidence = Math.Max(existingCandidate.Confidence, confidence);
                }
                else
                {
                    var newCandidate = new PitchCandidate
                    {
                        Frequency = frequency,
                        Magnitude = magnitude,
                        StartTime = currentTime,
                        EndTime = frameEndTime,
                        MidiNote = midiNote,
                        PeakAmplitude = smoothedRms,
                        AvgAmplitude = smoothedRms,
                        FrameCount = 1,
                        Confidence = confidence
                    };
                    activePitches.Add(midiNote, newCandidate);
                }
            }

            var endedNotes = new List<int>();
            foreach (var kvp in activePitches)
            {
                if (!currentFramePitches.Contains(kvp.Key))
                {
                    if (kvp.Value.EndTime - kvp.Value.StartTime >= minNoteDurationSeconds)
                    {
                        candidates.Add(kvp.Value);
                    }
                    endedNotes.Add(kvp.Key);
                }
            }

            foreach (var noteNum in endedNotes)
            {
                activePitches.Remove(noteNum);
            }
        }

        private void EndAllActivePitches(
            Dictionary<int, PitchCandidate> activePitches,
            List<PitchCandidate> candidates,
            double minNoteDurationSeconds)
        {
            foreach (var candidate in activePitches.Values)
            {
                if (candidate.EndTime - candidate.StartTime >= minNoteDurationSeconds)
                {
                    candidates.Add(candidate);
                }
            }
            activePitches.Clear();
        }

        private void FinalizeActivePitches(
            Dictionary<int, PitchCandidate> activePitches,
            List<PitchCandidate> candidates,
            double minNoteDurationSeconds)
        {
            foreach (var candidate in activePitches.Values)
            {
                if (candidate.EndTime - candidate.StartTime >= minNoteDurationSeconds)
                {
                    candidates.Add(candidate);
                }
            }
        }

        private List<MidiEvent> GeneratePolyphonicMidiEvents(List<PitchCandidate> candidates)
        {
            var midiEvents = new List<MidiEvent>
            {
                new TempoEvent((int)(60000000 / Bpm), 0),
                new PatchChangeEvent(0, 1, 0),
                new ControlChangeEvent(0, 1, MidiController.MainVolume, 127),
                new ControlChangeEvent(0, 1, MidiController.Expression, 127)
            };

            if (candidates.Count == 0)
            {
                Logger.Warn("No pitch candidates found.", 3);
                return midiEvents;
            }

            float maxAvgAmplitude = candidates.Max(c => c.AvgAmplitude);
            if (maxAvgAmplitude <= 0) maxAvgAmplitude = 1.0f;

            Logger.Info($"Generating MIDI events. Max Avg Amplitude = {maxAvgAmplitude:F4}", 5);

            foreach (var candidate in candidates)
            {
                if (candidate.Confidence < 0.3f) continue;

                int midiNote = candidate.MidiNote;
                double normalizedAmplitude = Math.Clamp(candidate.AvgAmplitude / maxAvgAmplitude, 0.0, 1.0);

                double velocityFactor = Math.Pow(normalizedAmplitude, 0.7 - _config.VelocitySensitivity * 0.3);
                int velocity = (int)Math.Clamp(20 + velocityFactor * 107, 1, 127);

                long startTick = Math.Max(0, (long)Math.Round(candidate.StartTime / _secondsPerTick));
                long endTick = Math.Max(startTick + 1, (long)Math.Round(candidate.EndTime / _secondsPerTick));
                long durationTick = Math.Max(1, endTick - startTick);

                midiEvents.Add(new NoteOnEvent(startTick, 1, midiNote, velocity, 0));
                midiEvents.Add(new NoteEvent(endTick, 1, MidiCommandCode.NoteOff, midiNote, 0));
            }

            return midiEvents;
        }

        private List<MidiEvent> LinkNoteOnToNoteOff(List<MidiEvent> events)
        {
            var linkedEvents = new List<MidiEvent>();
            var noteOnEvents = events.OfType<NoteOnEvent>().Where(n => n.Velocity > 0).OrderBy(n => n.AbsoluteTime).ToList();
            var noteOffEvents = events.OfType<NoteEvent>().Where(n => n.CommandCode == MidiCommandCode.NoteOff).OrderBy(n => n.AbsoluteTime).ToList();
            var otherEvents = events.Where(e => !(e is NoteOnEvent || (e is NoteEvent ne && ne.CommandCode == MidiCommandCode.NoteOff))).ToList();

            linkedEvents.AddRange(otherEvents);

            var activeNoteOns = new Dictionary<(int Channel, int NoteNumber), NoteOnEvent>();
            const long minDurationTicks = 1;
            int notesLinked = 0;

            var allEventsSorted = noteOnEvents.Cast<MidiEvent>()
                .Concat(noteOffEvents)
                .OrderBy(e => e.AbsoluteTime)
                .ThenBy(e => e is NoteEvent ? 1 : 0)
                .ToList();

            foreach (var ev in allEventsSorted)
            {
                if (ev is NoteOnEvent noteOn)
                {
                    var key = (noteOn.Channel, noteOn.NoteNumber);
                    if (activeNoteOns.TryGetValue(key, out var existingNoteOn))
                    {
                        if (existingNoteOn.OffEvent == null)
                        {
                            long duration = Math.Max(minDurationTicks, noteOn.AbsoluteTime - existingNoteOn.AbsoluteTime);
                            existingNoteOn.OffEvent = new NoteEvent(existingNoteOn.AbsoluteTime + duration,
                                existingNoteOn.Channel, MidiCommandCode.NoteOff, existingNoteOn.NoteNumber, 0);
                            existingNoteOn.NoteLength = (int)duration;
                            linkedEvents.Add(existingNoteOn);
                            linkedEvents.Add(existingNoteOn.OffEvent);
                            notesLinked++;
                        }
                    }
                    activeNoteOns[key] = noteOn;
                }
                else if (ev is NoteEvent noteOff)
                {
                    var key = (noteOff.Channel, noteOff.NoteNumber);
                    if (activeNoteOns.TryGetValue(key, out var correspondingNoteOn))
                    {
                        long duration = noteOff.AbsoluteTime - correspondingNoteOn.AbsoluteTime;
                        if (duration >= minDurationTicks)
                        {
                            correspondingNoteOn.OffEvent = noteOff;
                            correspondingNoteOn.NoteLength = (int)duration;
                            linkedEvents.Add(correspondingNoteOn);
                            linkedEvents.Add(noteOff);
                            notesLinked++;
                            activeNoteOns.Remove(key);
                        }
                        else
                        {
                            activeNoteOns.Remove(key);
                        }
                    }
                }
            }

            foreach (var orphanNoteOn in activeNoteOns.Values)
            {
                orphanNoteOn.OffEvent = new NoteEvent(orphanNoteOn.AbsoluteTime + minDurationTicks,
                    orphanNoteOn.Channel, MidiCommandCode.NoteOff, orphanNoteOn.NoteNumber, 0);
                orphanNoteOn.NoteLength = (int)minDurationTicks;
                linkedEvents.Add(orphanNoteOn);
                linkedEvents.Add(orphanNoteOn.OffEvent);
                notesLinked++;
            }

            linkedEvents = linkedEvents.OrderBy(e => e.AbsoluteTime).ToList();
            Logger.Info($"LinkNoteOnToNoteOff: Linked {notesLinked} notes.", 5);

            return linkedEvents;
        }

        private int FrequencyToMidiNote(float freq)
        {
            if (freq <= 0) return -1;
            double note = 12.0 * Math.Log2(freq / 440.0) + 69.0;
            return (int)Math.Clamp(Math.Round(note), 0, 127);
        }

        private void QuantizeEvents(List<MidiEvent> midiEvents)
        {
            if (_config.QuantizeValue == QuantizeDuration.None)
            {
                EnsureEndTrackEvent(midiEvents);
                return;
            }

            double ticksPerBeat = TicksPerQuarterNote;
            double quantizeGridTicks = _config.QuantizeValue switch
            {
                QuantizeDuration.Whole => ticksPerBeat * 4,
                QuantizeDuration.Half => ticksPerBeat * 2,
                QuantizeDuration.Quarter => ticksPerBeat,
                QuantizeDuration.Eighth => ticksPerBeat / 2,
                QuantizeDuration.Sixteenth => ticksPerBeat / 4,
                QuantizeDuration.ThirtySecond => ticksPerBeat / 8,
                _ => ticksPerBeat / 4
            };

            quantizeGridTicks = Math.Max(1, quantizeGridTicks);
            const long minDurationTicks = 1;

            var noteOnEvents = midiEvents.OfType<NoteOnEvent>().Where(n => n.OffEvent != null).ToList();
            var otherEvents = midiEvents.Where(e => !(e is NoteOnEvent || (e is NoteEvent ne && ne.CommandCode == MidiCommandCode.NoteOff))).ToList();

            foreach (var noteOn in noteOnEvents)
            {
                long originalStart = noteOn.AbsoluteTime;
                long originalEnd = noteOn.OffEvent!.AbsoluteTime;

                long quantizedStart = (long)Math.Round(originalStart / quantizeGridTicks) * (long)quantizeGridTicks;
                long quantizedEnd = (long)Math.Round(originalEnd / quantizeGridTicks) * (long)quantizeGridTicks;

                long newDuration = Math.Max(minDurationTicks, quantizedEnd - quantizedStart);

                noteOn.AbsoluteTime = quantizedStart;
                noteOn.NoteLength = (int)newDuration;
                noteOn.OffEvent.AbsoluteTime = quantizedStart + newDuration;
            }

            midiEvents.Clear();
            midiEvents.AddRange(otherEvents);

            foreach (var noteOn in noteOnEvents)
            {
                if (noteOn.NoteLength > 0)
                {
                    midiEvents.Add(noteOn);
                    if (noteOn.OffEvent != null)
                    {
                        midiEvents.Add(noteOn.OffEvent);
                    }
                }
            }

            foreach (var ev in otherEvents)
            {
                if (!(ev is MetaEvent me && me.MetaEventType == MetaEventType.EndTrack))
                {
                    long originalTime = ev.AbsoluteTime;
                    long quantizedTime = (long)Math.Round(originalTime / quantizeGridTicks) * (long)quantizeGridTicks;
                    ev.AbsoluteTime = quantizedTime;
                }
            }

            midiEvents.Sort((a, b) => a.AbsoluteTime.CompareTo(b.AbsoluteTime));
            EnsureEndTrackEvent(midiEvents);
        }

        private void EnsureEndTrackEvent(List<MidiEvent> midiEvents)
        {
            var endTrack = midiEvents.OfType<MetaEvent>().FirstOrDefault(me => me.MetaEventType == MetaEventType.EndTrack);
            long lastEventTime = midiEvents.Where(e => e != endTrack).DefaultIfEmpty().Max(e => e?.AbsoluteTime ?? 0);

            if (endTrack != null)
            {
                midiEvents.Remove(endTrack);
            }

            midiEvents.Add(new MetaEvent(MetaEventType.EndTrack, 0, Math.Max(lastEventTime + TicksPerQuarterNote, TicksPerQuarterNote)));
        }
    }
}
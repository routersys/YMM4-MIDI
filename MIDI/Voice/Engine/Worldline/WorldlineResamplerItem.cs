using MIDI.Voice.SUSL.Parsing.AST;
using System;
using System.Collections.Generic;
using NAudio.Wave;
using MIDI.Utils;

namespace MIDI.Voice.Engine.Worldline
{
    internal class WorldlineResamplerItem
    {
        public WorldlineApi.SynthRequest Request;
        public UtauOto Oto { get; }
        public NoteCommandNode Note { get; }

        public double[] Sample { get; }
        public byte[]? FrqData { get; private set; }
        public double[] Pitches { get; }

        public double SkipOverMs { get; }
        public double PositionMs { get; }
        public double DurationMs { get; }
        public double FadeInMs { get; }
        public double FadeOutMs { get; }
        public bool Direct { get; }

        public WorldlineResamplerItem(NoteCommandNode note, UtauVoicebank voicebank, SuslEventConverter.TimingContext timing)
        {
            Note = note;

            string primaryAlias;
            if (note.LyricPhoneme.Phoneme != null)
            {
                primaryAlias = note.LyricPhoneme.Phoneme;
            }
            else
            {
                primaryAlias = note.LyricPhoneme.Lyric ?? "a";
            }

            var noteNum = WorldlineUtils.FreqToTone(timing.GetFrequency(note.Pitch));

            Oto = voicebank.GetOto(primaryAlias, (int)Math.Round(noteNum)) ?? throw new Exception($"Oto not found for alias: {primaryAlias}");

            try
            {
                var otoFrq = voicebank.GetFrq(Oto);
                FrqData = otoFrq.Data;
            }
            catch (Exception)
            {
                FrqData = null;
            }

            int sampleFs = 44100;
            try
            {
                using (var reader = new AudioFileReader(Oto.File))
                {
                    sampleFs = reader.WaveFormat.SampleRate;
                }
            }
            catch (Exception)
            {
            }

            Sample = voicebank.GetSamplesAsDouble(Oto.File);

            double tempo = timing.GetCurrentTempo(note.AbsoluteTick);
            int timebase = timing.Timebase;

            Request = new WorldlineApi.SynthRequest();
            Request.sample_fs = sampleFs;
            Request.tone = (int)Math.Round(noteNum);
            Request.con_vel = 100.0;

            Request.volume = 100.0;
            Request.modulation = 0.0;
            Request.tempo = tempo;

            double pitchLeadingMs = Oto.Preutter;
            SkipOverMs = pitchLeadingMs;

            int startTick = note.AbsoluteTick;
            int durationTicks = timing.GetTickDuration(note.Length);

            if (startTick < 0) startTick = 0;
            if (durationTicks < 0) durationTicks = 0;
            const int MAX_SAFE_TICKS = 10000000;
            if (durationTicks > MAX_SAFE_TICKS) durationTicks = MAX_SAFE_TICKS;

            PositionMs = timing.GetMsForTick(startTick);

            DurationMs = timing.GetMsDuration(startTick, durationTicks);

            double requiredLengthMs = DurationMs;

            Request.offset = Oto.Offset;
            Request.consonant = Oto.Consonant;
            Request.cut_off = Oto.Cutoff;

            double minRequiredLengthMs = Math.Max(0.0, Request.offset) + Math.Max(0.0, Request.consonant);

            if (requiredLengthMs < minRequiredLengthMs)
            {
                requiredLengthMs = minRequiredLengthMs;
            }

            if (requiredLengthMs < 1.0)
            {
                requiredLengthMs = 1.0;
            }

            Request.required_length = requiredLengthMs;

            var (itemF0, _, _, _, _) = timing.GetCurves(startTick, durationTicks, Request.tone);
            Pitches = itemF0;
            Request.pitch_bend_length = Pitches.Length;

            Request.flag_g = 0;
            Request.flag_O = 0;
            Request.flag_P = 86;
            Request.flag_Mt = 0;
            Request.flag_Mb = 0;
            Request.flag_Mv = 100;

            if (note.Parameters != null)
            {
                foreach (var param in note.Parameters.Expressions)
                {
                    switch (param.Name.ToUpper())
                    {
                        case "VEL": Request.con_vel = param.Value; break;
                        case "VOL": Request.volume = Math.Min(100.0, Math.Max(0.0, param.Value)); break;
                        case "MOD": Request.modulation = param.Value; break;
                        case "G": Request.flag_g = (int)param.Value; break;
                        case "O": Request.flag_O = (int)param.Value; break;
                        case "P": Request.flag_P = (int)param.Value; break;
                    }
                }
            }

            if (Request.modulation == 0.0)
            {
                Request.modulation = 0.01;
            }

            FadeInMs = Math.Min(50, DurationMs * 0.1);
            FadeOutMs = Math.Min(50, DurationMs * 0.1);
        }
    }
}
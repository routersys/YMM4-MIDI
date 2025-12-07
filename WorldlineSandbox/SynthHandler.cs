using System.Runtime.InteropServices;

namespace WorldlineHost
{
    internal static class SynthHandler
    {
        public static SynthResponse Synthesize(SynthRequestData request)
        {
            var sampleHandles = new List<GCHandle>();
            var frqHandles = new List<GCHandle?>();
            var pitchHandles = new List<GCHandle?>();

            try
            {
                using (var phraseSynth = new WorldlineApi.PhraseSynth(request.Items.Count))
                {
                    for (int i = 0; i < request.Items.Count; i++)
                    {
                        var item = request.Items[i];

                        var sampleHandle = GCHandle.Alloc(item.Sample, GCHandleType.Pinned);
                        sampleHandles.Add(sampleHandle);

                        GCHandle? frqHandle = null;
                        if (item.FrqData != null && item.FrqData.Length > 0)
                        {
                            frqHandle = GCHandle.Alloc(item.FrqData, GCHandleType.Pinned);
                        }
                        frqHandles.Add(frqHandle);

                        GCHandle? pitchHandle = null;
                        if (item.Pitches != null && item.Pitches.Length > 0)
                        {
                            pitchHandle = GCHandle.Alloc(item.Pitches, GCHandleType.Pinned);
                        }
                        pitchHandles.Add(pitchHandle);

                        var nativeRequest = new WorldlineApi.SynthRequest
                        {
                            sample_fs = item.SampleFs,
                            sample_length = item.Sample.Length,
                            frq_length = item.FrqData?.Length ?? 0,
                            tone = item.Tone,
                            con_vel = item.ConVel,
                            offset = item.Offset,
                            required_length = item.RequiredLength,
                            consonant = item.Consonant,
                            cut_off = item.CutOff,
                            volume = item.Volume,
                            modulation = item.Modulation,
                            tempo = item.Tempo,
                            pitch_bend_length = item.Pitches?.Length ?? 0,
                            flag_g = item.FlagG,
                            flag_O = item.FlagO,
                            flag_P = item.FlagP,
                            flag_Mt = item.FlagMt,
                            flag_Mb = item.FlagMb,
                            flag_Mv = item.FlagMv,
                            sample = IntPtr.Zero,
                            frq = IntPtr.Zero,
                            pitch_bend = IntPtr.Zero
                        };

                        phraseSynth.AddRequest(ref nativeRequest,
                            item.PosMs, item.SkipMs, item.LengthMs,
                            item.FadeInMs, item.FadeOutMs,
                            i, sampleHandle, frqHandle, pitchHandle);
                    }

                    phraseSynth.SetCurves(request.F0, request.Gender, request.Tension, request.Breathiness, request.Voicing);

                    float[] audioData = phraseSynth.Synth();

                    return new SynthResponse { Success = true, AudioData = audioData };
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Synthesis failed.", ex);
                return new SynthResponse { Success = false, ErrorMessage = ex.ToString() };
            }
            finally
            {
                foreach (var handle in sampleHandles)
                {
                    if (handle.IsAllocated) handle.Free();
                }
                foreach (var handle in frqHandles)
                {
                    if (handle.HasValue && handle.Value.IsAllocated) handle.Value.Free();
                }
                foreach (var handle in pitchHandles)
                {
                    if (handle.HasValue && handle.Value.IsAllocated) handle.Value.Free();
                }
            }
        }
    }
}
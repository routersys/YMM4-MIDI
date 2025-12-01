using MIDI.AudioEffect.EQUALIZER.Models;
using MIDI.Configuration.Models;
using System;
using System.Buffers;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using YukkuriMovieMaker.Player.Audio;
using YukkuriMovieMaker.Player.Audio.Effects;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;

namespace MIDI.AudioEffect.EQUALIZER
{
    internal class EqualizerProcessor : AudioEffectProcessorBase
    {
        readonly EqualizerAudioEffect item;
        IFilter[] filtersL;
        IFilter[] filtersR;
        readonly ReadOnlyObservableCollection<EQBand> bands;
        readonly EqualizerAlgorithm algorithm;

        private readonly OverSampler overSamplerL;
        private readonly OverSampler overSamplerR;

        public override int Hz => Input?.Hz ?? 0;
        public override long Duration => Input?.Duration ?? 0;

        public EqualizerProcessor(EqualizerAudioEffect item, TimeSpan duration)
        {
            this.item = item;
            this.algorithm = EqualizerSettings.Default.Algorithm;
            bands = new(item.Bands);
            int count = bands.Count;
            filtersL = new IFilter[count];
            filtersR = new IFilter[count];

            for (int i = 0; i < count; i++)
            {
                if (algorithm == EqualizerAlgorithm.TPT_SVF)
                {
                    filtersL[i] = new TptSvfFilter();
                    filtersR[i] = new TptSvfFilter();
                }
                else
                {
                    filtersL[i] = new BiquadFilter();
                    filtersR[i] = new BiquadFilter();
                }
            }
            overSamplerL = new OverSampler();
            overSamplerR = new OverSampler();
        }

        protected override unsafe int read(float[] destBuffer, int offset, int count)
        {
            if (Input is null) return 0;
            int readCount = Input.Read(destBuffer, offset, count);

            bool useHighQuality = EqualizerSettings.Default.HighQualityMode;
            int bandCount = bands.Count;

            if (filtersL.Length != bandCount)
            {
                Array.Resize(ref filtersL, bandCount);
                Array.Resize(ref filtersR, bandCount);
                for (int k = 0; k < bandCount; k++)
                {
                    if (filtersL[k] == null) filtersL[k] = algorithm == EqualizerAlgorithm.TPT_SVF ? new TptSvfFilter() : new BiquadFilter();
                    if (filtersR[k] == null) filtersR[k] = algorithm == EqualizerAlgorithm.TPT_SVF ? new TptSvfFilter() : new BiquadFilter();
                }
            }

            int frames = readCount / 2;
            long startFrame = Position / 2;
            long totalFrames = Duration / 2;
            int hz = Hz;

            float[] bufL = ArrayPool<float>.Shared.Rent(frames);
            float[] bufR = ArrayPool<float>.Shared.Rent(frames);

            fixed (float* pDest = destBuffer)
            fixed (float* pBufL = bufL)
            fixed (float* pBufR = bufR)
            {
                float* pd = pDest + offset;
                for (int i = 0; i < frames; i++)
                {
                    pBufL[i] = pd[i * 2];
                    pBufR[i] = pd[i * 2 + 1];
                }

                IntPtr ptrL = (IntPtr)pBufL;
                IntPtr ptrR = (IntPtr)pBufR;

                Parallel.Invoke(
                    () => ProcessChannel((float*)ptrL, frames, startFrame, totalFrames, hz, useHighQuality, filtersL, bands, overSamplerL, 0),
                    () => ProcessChannel((float*)ptrR, frames, startFrame, totalFrames, hz, useHighQuality, filtersR, bands, overSamplerR, 1)
                );

                for (int i = 0; i < frames; i++)
                {
                    pd[i * 2] = pBufL[i];
                    pd[i * 2 + 1] = pBufR[i];
                }
            }

            ArrayPool<float>.Shared.Return(bufL);
            ArrayPool<float>.Shared.Return(bufR);

            return readCount;
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        private unsafe void ProcessChannel(float* buffer, int count, long startFrame, long totalFrames, int hz, bool useHighQuality, IFilter[] filters, ReadOnlyObservableCollection<EQBand> bands, OverSampler overSampler, int channelIndex)
        {
            int bandCount = bands.Count;

            float* freqArr = stackalloc float[bandCount];
            float* gainArr = stackalloc float[bandCount];
            float* qArr = stackalloc float[bandCount];
            bool* enabledArr = stackalloc bool[bandCount];
            StereoMode* modeArr = stackalloc StereoMode[bandCount];
            MIDI.AudioEffect.EQUALIZER.Models.FilterType* typeArr = stackalloc MIDI.AudioEffect.EQUALIZER.Models.FilterType[bandCount];

            for (int k = 0; k < bandCount; k++)
            {
                var b = bands[k];
                enabledArr[k] = b.IsEnabled;
                modeArr[k] = b.StereoMode;
                typeArr[k] = b.Type;
            }

            for (int i = 0; i < count; i++)
            {
                float sample = buffer[i];
                long currentFrame = startFrame + i;

                if (useHighQuality)
                {
                    overSampler.Upsample(sample, out float up1, out float up2);
                    int osHz = hz * 2;

                    for (int j = 0; j < bandCount; j += 4)
                    {
                        if (j + 4 <= bandCount && Avx.IsSupported)
                        {
                            var band0 = bands[j];
                            var band1 = bands[j + 1];
                            var band2 = bands[j + 2];
                            var band3 = bands[j + 3];

                            var freqVec = Vector128.Create(
                                (float)band0.Frequency.GetValue(currentFrame, totalFrames, hz),
                                (float)band1.Frequency.GetValue(currentFrame, totalFrames, hz),
                                (float)band2.Frequency.GetValue(currentFrame, totalFrames, hz),
                                (float)band3.Frequency.GetValue(currentFrame, totalFrames, hz));

                            var gainVec = Vector128.Create(
                                (float)band0.Gain.GetValue(currentFrame, totalFrames, hz),
                                (float)band1.Gain.GetValue(currentFrame, totalFrames, hz),
                                (float)band2.Gain.GetValue(currentFrame, totalFrames, hz),
                                (float)band3.Gain.GetValue(currentFrame, totalFrames, hz));

                            var qVec = Vector128.Create(
                                (float)band0.Q.GetValue(currentFrame, totalFrames, hz),
                                (float)band1.Q.GetValue(currentFrame, totalFrames, hz),
                                (float)band2.Q.GetValue(currentFrame, totalFrames, hz),
                                (float)band3.Q.GetValue(currentFrame, totalFrames, hz));

                            StoreVec(freqVec, freqArr + j);
                            StoreVec(gainVec, gainArr + j);
                            StoreVec(qVec, qArr + j);
                        }
                        else
                        {
                            for (int k = j; k < Math.Min(j + 4, bandCount); k++)
                            {
                                var band = bands[k];
                                freqArr[k] = (float)band.Frequency.GetValue(currentFrame, totalFrames, hz);
                                gainArr[k] = (float)band.Gain.GetValue(currentFrame, totalFrames, hz);
                                qArr[k] = (float)band.Q.GetValue(currentFrame, totalFrames, hz);
                            }
                        }
                    }

                    for (int j = 0; j < bandCount; j++)
                    {
                        if (enabledArr[j])
                        {
                            bool process = modeArr[j] == StereoMode.Stereo || (modeArr[j] == StereoMode.Left && channelIndex == 0) || (modeArr[j] == StereoMode.Right && channelIndex == 1);
                            if (process)
                            {
                                filters[j].SetCoefficients(typeArr[j], osHz, freqArr[j], gainArr[j], qArr[j]);
                                up1 = filters[j].Process(up1);
                                up2 = filters[j].Process(up2);
                            }
                        }
                    }
                    sample = overSampler.Downsample(up1, up2);
                }
                else
                {
                    for (int j = 0; j < bandCount; j += 4)
                    {
                        if (j + 4 <= bandCount && Avx.IsSupported)
                        {
                            var band0 = bands[j];
                            var band1 = bands[j + 1];
                            var band2 = bands[j + 2];
                            var band3 = bands[j + 3];

                            var freqVec = Vector128.Create(
                                (float)band0.Frequency.GetValue(currentFrame, totalFrames, hz),
                                (float)band1.Frequency.GetValue(currentFrame, totalFrames, hz),
                                (float)band2.Frequency.GetValue(currentFrame, totalFrames, hz),
                                (float)band3.Frequency.GetValue(currentFrame, totalFrames, hz));

                            var gainVec = Vector128.Create(
                                (float)band0.Gain.GetValue(currentFrame, totalFrames, hz),
                                (float)band1.Gain.GetValue(currentFrame, totalFrames, hz),
                                (float)band2.Gain.GetValue(currentFrame, totalFrames, hz),
                                (float)band3.Gain.GetValue(currentFrame, totalFrames, hz));

                            var qVec = Vector128.Create(
                                (float)band0.Q.GetValue(currentFrame, totalFrames, hz),
                                (float)band1.Q.GetValue(currentFrame, totalFrames, hz),
                                (float)band2.Q.GetValue(currentFrame, totalFrames, hz),
                                (float)band3.Q.GetValue(currentFrame, totalFrames, hz));

                            StoreVec(freqVec, freqArr + j);
                            StoreVec(gainVec, gainArr + j);
                            StoreVec(qVec, qArr + j);
                        }
                        else
                        {
                            for (int k = j; k < Math.Min(j + 4, bandCount); k++)
                            {
                                var band = bands[k];
                                freqArr[k] = (float)band.Frequency.GetValue(currentFrame, totalFrames, hz);
                                gainArr[k] = (float)band.Gain.GetValue(currentFrame, totalFrames, hz);
                                qArr[k] = (float)band.Q.GetValue(currentFrame, totalFrames, hz);
                            }
                        }
                    }

                    for (int j = 0; j < bandCount; j++)
                    {
                        if (enabledArr[j])
                        {
                            bool process = modeArr[j] == StereoMode.Stereo || (modeArr[j] == StereoMode.Left && channelIndex == 0) || (modeArr[j] == StereoMode.Right && channelIndex == 1);
                            if (process)
                            {
                                filters[j].SetCoefficients(typeArr[j], hz, freqArr[j], gainArr[j], qArr[j]);
                                sample = filters[j].Process(sample);
                            }
                        }
                    }
                }
                buffer[i] = sample;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private unsafe void StoreVec(Vector128<float> vec, float* ptr)
        {
            Sse.Store(ptr, vec);
        }

        protected override void seek(long position)
        {
            Input?.Seek(position);
            if (filtersL != null) foreach (var f in filtersL) f?.Reset();
            if (filtersR != null) foreach (var f in filtersR) f?.Reset();
            overSamplerL.Reset();
            overSamplerR.Reset();
        }
    }

    internal static class FastMath
    {
        private static readonly float[] SinTable;
        private static readonly float[] TanTable;
        private const int TableSize = 65536;
        private const float TwoPi = (float)(2.0 * Math.PI);
        private const float InvTwoPi = 1.0f / TwoPi;
        private const float HalfPi = (float)(Math.PI / 2.0);

        static FastMath()
        {
            SinTable = new float[TableSize + 1];
            TanTable = new float[TableSize + 1];
            for (int i = 0; i <= TableSize; i++)
            {
                SinTable[i] = (float)Math.Sin(TwoPi * i / TableSize);
                double tanVal = Math.Tan(HalfPi * i / TableSize);
                TanTable[i] = (float)Math.Min(tanVal, 1000.0);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Sin(float x)
        {
            float indexVal = x * InvTwoPi * TableSize;
            int index = (int)indexVal;
            float frac = indexVal - index;
            index &= TableSize - 1;
            return SinTable[index] + frac * (SinTable[index + 1] - SinTable[index]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Cos(float x)
        {
            return Sin(x + HalfPi);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float TanHalf(float x)
        {
            float normalized = x / (float)Math.PI;
            if (normalized >= 0.999f) return TanTable[TableSize];
            float indexVal = normalized * TableSize;
            int index = (int)indexVal;
            float frac = indexVal - index;
            if (index >= TableSize) index = TableSize - 1;
            return TanTable[index] + frac * (TanTable[index + 1] - TanTable[index]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Pow10(float x)
        {
            return (float)Math.Pow(10, x);
        }
    }

    internal interface IFilter
    {
        void Reset();
        void SetCoefficients(MIDI.AudioEffect.EQUALIZER.Models.FilterType type, float sampleRate, float freq, float gainDb, float q);
        float Process(float input);
    }

    internal class OverSampler
    {
        private float last_in;
        private float last_out;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Upsample(float input, out float out1, out float out2)
        {
            out1 = (last_in + input) * 0.5f;
            out2 = input;
            last_in = input;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Downsample(float input1, float input2)
        {
            float output = (last_out + input1) * 0.5f;
            last_out = input2;
            return output;
        }
        public void Reset()
        {
            last_in = 0;
            last_out = 0;
        }
    }

    internal class BiquadFilter : IFilter
    {
        private float a1, a2, b0, b1, b2;
        private float x1, x2, y1, y2;

        public void Reset() { x1 = x2 = y1 = y2 = 0; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCoefficients(MIDI.AudioEffect.EQUALIZER.Models.FilterType type, float sampleRate, float freq, float gainDb, float q)
        {
            if (freq <= 20) freq = 20;
            if (q <= 0) q = 0.01f;
            if (freq >= sampleRate / 2) freq = sampleRate / 2 - 1;

            float omega = (float)(2.0 * Math.PI * freq / sampleRate);
            float sinOmega = FastMath.Sin(omega);
            float cosOmega = FastMath.Cos(omega);
            float alpha = sinOmega / (2.0f * q);
            float aVal = FastMath.Pow10(gainDb / 40.0f);
            float a0 = 1;

            switch (type)
            {
                case MIDI.AudioEffect.EQUALIZER.Models.FilterType.Peak:
                    b0 = 1.0f + alpha * aVal;
                    b1 = -2.0f * cosOmega;
                    b2 = 1.0f - alpha * aVal;
                    a0 = 1.0f + alpha / aVal;
                    a1 = -2.0f * cosOmega;
                    a2 = 1.0f - alpha / aVal;
                    break;
                case MIDI.AudioEffect.EQUALIZER.Models.FilterType.LowShelf:
                    float sqrtA = (float)Math.Sqrt(aVal);
                    b0 = aVal * ((aVal + 1.0f) - (aVal - 1.0f) * cosOmega + 2.0f * sqrtA * alpha);
                    b1 = 2.0f * aVal * ((aVal - 1.0f) - (aVal + 1.0f) * cosOmega);
                    b2 = aVal * ((aVal + 1.0f) - (aVal - 1.0f) * cosOmega - 2.0f * sqrtA * alpha);
                    a0 = (aVal + 1.0f) + (aVal - 1.0f) * cosOmega + 2.0f * sqrtA * alpha;
                    a1 = -2.0f * ((aVal - 1.0f) + (aVal + 1.0f) * cosOmega);
                    a2 = (aVal + 1.0f) + (aVal - 1.0f) * cosOmega - 2.0f * sqrtA * alpha;
                    break;
                case MIDI.AudioEffect.EQUALIZER.Models.FilterType.HighShelf:
                    float sqrtA2 = (float)Math.Sqrt(aVal);
                    b0 = aVal * ((aVal + 1.0f) + (aVal - 1.0f) * cosOmega + 2.0f * sqrtA2 * alpha);
                    b1 = -2.0f * aVal * ((aVal - 1.0f) + (aVal + 1.0f) * cosOmega);
                    b2 = aVal * ((aVal + 1.0f) + (aVal - 1.0f) * cosOmega - 2.0f * sqrtA2 * alpha);
                    a0 = (aVal + 1.0f) - (aVal - 1.0f) * cosOmega + 2.0f * sqrtA2 * alpha;
                    a1 = 2.0f * ((aVal - 1.0f) - (aVal + 1.0f) * cosOmega);
                    a2 = (aVal + 1.0f) + (aVal - 1.0f) * cosOmega - 2.0f * sqrtA2 * alpha;
                    break;
            }
            float invA0 = 1.0f / a0;
            this.b0 = b0 * invA0; this.b1 = b1 * invA0; this.b2 = b2 * invA0;
            this.a1 = a1 * invA0; this.a2 = a2 * invA0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Process(float input)
        {
            float output = b0 * input + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = input; y2 = y1; y1 = output;
            return output;
        }
    }

    internal class TptSvfFilter : IFilter
    {
        private float s1, s2;
        private float g, twoR_plus_g, den;
        private float kHP, kBP, kLP;

        public void Reset() { s1 = 0; s2 = 0; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetCoefficients(MIDI.AudioEffect.EQUALIZER.Models.FilterType type, float sampleRate, float freq, float gainDb, float q)
        {
            if (freq <= 20) freq = 20;
            if (freq >= sampleRate / 2) freq = sampleRate / 2 - 1;

            g = FastMath.TanHalf((float)(Math.PI * freq / sampleRate));
            float invQ = 1.0f / q;
            den = 1.0f / (1.0f + invQ * g + g * g);
            twoR_plus_g = invQ + g;

            float linearGain = FastMath.Pow10(gainDb / 20.0f);
            float mix = linearGain - 1.0f;

            kHP = 0; kBP = 0; kLP = 0;

            switch (type)
            {
                case MIDI.AudioEffect.EQUALIZER.Models.FilterType.Peak:
                    kBP = mix;
                    break;
                case MIDI.AudioEffect.EQUALIZER.Models.FilterType.LowShelf:
                    kLP = mix;
                    break;
                case MIDI.AudioEffect.EQUALIZER.Models.FilterType.HighShelf:
                    kHP = mix;
                    break;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Process(float input)
        {
            float hp = (input - twoR_plus_g * s1 - s2) * den;
            float bp = g * hp + s1;
            float lp = g * bp + s2;

            s1 = g * hp + bp;
            s2 = g * bp + lp;

            return input + kHP * hp + kBP * bp + kLP * lp;
        }
    }
}
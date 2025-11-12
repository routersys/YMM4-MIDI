using ComputeSharp;

namespace MIDI
{
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct WaveformGenerationShader : IComputeShader
    {
        private readonly ReadWriteBuffer<float> buffer;
        private readonly ReadOnlyBuffer<GpuNoteData> notes;
        private readonly int sampleRate;
        private readonly float fmModulatorFrequency;
        private readonly float fmModulationIndex;
        private readonly Bool enableBandlimited;
        private readonly int offset;
        private readonly int crossfadeSamples;

        public WaveformGenerationShader(ReadWriteBuffer<float> buffer, ReadOnlyBuffer<GpuNoteData> notes, int sampleRate, int offset,
                                        float fmModulatorFrequency, float fmModulationIndex, bool enableBandlimited, int crossfadeSamples)
        {
            this.buffer = buffer;
            this.notes = notes;
            this.sampleRate = sampleRate;
            this.offset = offset;
            this.fmModulatorFrequency = fmModulatorFrequency;
            this.fmModulationIndex = fmModulationIndex;
            this.enableBandlimited = enableBandlimited;
            this.crossfadeSamples = crossfadeSamples;
        }

        private float GetEnvelope(int sample, int startSample, int endSample, float attack, float decay, float sustain, float release)
        {
            int attackSamples = (int)(attack * sampleRate);
            int decaySamples = (int)(decay * sampleRate);
            int releaseSamples = (int)(release * sampleRate);
            int totalSamples = endSample - startSample;
            int sampleInNote = sample - startSample;

            if (sampleInNote < 0) return 0;

            if (sampleInNote < attackSamples)
            {
                if (attackSamples == 0) return 1.0f;
                return sampleInNote / (float)attackSamples;
            }
            if (sampleInNote < attackSamples + decaySamples)
            {
                if (decaySamples == 0) return sustain;
                float decayProgress = (sampleInNote - attackSamples) / (float)decaySamples;
                return 1.0f - (1.0f - sustain) * decayProgress;
            }
            if (sampleInNote < totalSamples - releaseSamples)
            {
                return sustain;
            }
            if (sampleInNote < totalSamples)
            {
                if (releaseSamples == 0) return 0.0f;
                float releaseProgress = (sampleInNote - (totalSamples - releaseSamples)) / (float)releaseSamples;
                return sustain * (1.0f - releaseProgress);
            }
            return 0;
        }

        private float PolyBlep(float phase, float phaseIncrement)
        {
            if (phase < phaseIncrement)
            {
                phase /= phaseIncrement;
                return phase + phase - phase * phase - 1.0f;
            }
            if (phase > 1.0f - phaseIncrement)
            {
                phase = (phase - 1.0f) / phaseIncrement;
                return phase * phase + phase + phase + 1.0f;
            }
            return 0.0f;
        }

        private float GetNoise(int seed)
        {
            return Hlsl.Frac(Hlsl.Sin(seed * 12.9898f) * 43758.5453f) * 2.0f - 1.0f;
        }

        private float GetWave(int waveType, float frequency, float time, float phaseIncrement, int sampleIndex)
        {
            float phase = frequency * time;
            float fullPhase = 2 * 3.14159265f * phase;
            float sqVal;
            float sawVal;

            if (enableBandlimited)
            {
                phase = Hlsl.Frac(phase);
                switch (waveType)
                {
                    case 0: return Hlsl.Sin(fullPhase);
                    case 1:
                        sqVal = phase < 0.5f ? 1.0f : -1.0f;
                        sqVal += PolyBlep(phase, phaseIncrement);
                        sqVal -= PolyBlep(Hlsl.Frac(phase + 0.5f), phaseIncrement);
                        return sqVal;
                    case 2:
                        sawVal = 2.0f * phase - 1.0f;
                        sawVal -= PolyBlep(phase, phaseIncrement);
                        return sawVal;
                    case 3:
                        return 4.0f * Hlsl.Abs(phase - 0.5f) - 2.0f;
                    case 4: return (Hlsl.Sin(fullPhase) + 0.5f * Hlsl.Sin(2 * fullPhase) + 0.25f * Hlsl.Sin(3 * fullPhase)) / 1.75f;
                    case 5: return GetNoise(sampleIndex);
                    default: return Hlsl.Sin(fullPhase);
                }
            }

            switch (waveType)
            {
                case 0: return Hlsl.Sin(fullPhase);
                case 1: return Hlsl.Sign(Hlsl.Sin(fullPhase));
                case 2: return 2.0f * Hlsl.Frac(phase) - 1.0f;
                case 3: return 4.0f * Hlsl.Abs(Hlsl.Frac(phase) - 0.5f) - 1.0f;
                case 4: return (Hlsl.Sin(fullPhase) + 0.5f * Hlsl.Sin(2 * fullPhase) + 0.25f * Hlsl.Sin(3 * fullPhase));
                case 5: return GetNoise(sampleIndex);
                default: return Hlsl.Sin(fullPhase);
            }
        }

        public void Execute()
        {
            int sampleIndex = ThreadIds.X + this.offset;
            float leftValue = 0;
            float rightValue = 0;

            for (int i = 0; i < notes.Length; i++)
            {
                GpuNoteData note = notes[i];
                if (sampleIndex >= note.StartSample && sampleIndex < note.EndSample)
                {
                    float time = (sampleIndex - note.StartSample) / (float)sampleRate;
                    float envelope = GetEnvelope(sampleIndex, note.StartSample, note.EndSample, note.Attack, note.Decay, note.Sustain, note.Release);
                    float phaseIncrement = note.Frequency / sampleRate;
                    float wave = GetWave(note.WaveType, note.Frequency, time, phaseIncrement, sampleIndex);

                    if (crossfadeSamples > 0)
                    {
                        int samplesIntoNote = sampleIndex - note.StartSample;
                        int samplesToEnd = note.EndSample - sampleIndex;
                        float fadeMultiplier = 1.0f;

                        if (samplesIntoNote < crossfadeSamples)
                        {
                            fadeMultiplier = (float)samplesIntoNote / crossfadeSamples;
                        }
                        else if (samplesToEnd < crossfadeSamples)
                        {
                            fadeMultiplier = (float)samplesToEnd / crossfadeSamples;
                        }
                        envelope *= fadeMultiplier;
                    }

                    float finalValue = note.BaseAmplitude * envelope * wave;

                    leftValue += finalValue * note.PanLeft;
                    rightValue += finalValue * note.PanRight;
                }
            }

            int leftIndex = sampleIndex * 2;
            int rightIndex = leftIndex + 1;

            if (leftIndex < buffer.Length) buffer[leftIndex] += leftValue;
            if (rightIndex < buffer.Length) buffer[rightIndex] += rightValue;
        }
    }
}
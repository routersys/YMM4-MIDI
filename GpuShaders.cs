using ComputeSharp;

namespace MIDI
{
    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct ConvolutionShader : IComputeShader
    {
        private readonly ReadWriteBuffer<float> audioBuffer;
        private readonly ReadOnlyBuffer<float> impulseResponse;
        private readonly int irLength;

        public ConvolutionShader(ReadWriteBuffer<float> audioBuffer, ReadOnlyBuffer<float> impulseResponse, int irLength)
        {
            this.audioBuffer = audioBuffer;
            this.impulseResponse = impulseResponse;
            this.irLength = irLength;
        }

        public void Execute()
        {
            int i = ThreadIds.X;
            float result = 0;
            for (int j = 0; j < irLength; j++)
            {
                int sampleIndex = i - j;
                if (sampleIndex >= 0)
                {
                    result += audioBuffer[sampleIndex] * impulseResponse[j];
                }
            }
            audioBuffer[i] = Hlsl.Clamp(result, -1.0f, 1.0f);
        }
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct ToComplexShader : IComputeShader
    {
        private readonly ReadOnlyBuffer<float> input;
        private readonly ReadWriteBuffer<Float2> output;

        public ToComplexShader(ReadOnlyBuffer<float> input, ReadWriteBuffer<Float2> output)
        {
            this.input = input;
            this.output = output;
        }

        public void Execute()
        {
            int i = ThreadIds.X;
            output[i] = new Float2(input[i], 0);
        }
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct ToRealShader : IComputeShader
    {
        private readonly ReadWriteBuffer<Float2> input;
        private readonly ReadWriteBuffer<float> output;
        private readonly int n;

        public ToRealShader(ReadWriteBuffer<Float2> input, ReadWriteBuffer<float> output, int n)
        {
            this.input = input;
            this.output = output;
            this.n = n;
        }

        public void Execute()
        {
            int i = ThreadIds.X;
            output[i] = input[i].X / n;
        }
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct BitReverseShader : IComputeShader
    {
        private readonly ReadWriteBuffer<Float2> buffer;
        private readonly int n;

        public BitReverseShader(ReadWriteBuffer<Float2> buffer, int n)
        {
            this.buffer = buffer;
            this.n = n;
        }

        public void Execute()
        {
            int i = ThreadIds.X;
            int j = 0;
            int temp = i;
            for (int bit = 0; bit < Hlsl.Log2(n); bit++)
            {
                j = (j << 1) | (temp & 1);
                temp >>= 1;
            }

            if (i < j)
            {
                Float2 tempVal = buffer[i];
                buffer[i] = buffer[j];
                buffer[j] = tempVal;
            }
        }
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct FftShader : IComputeShader
    {
        private readonly ReadWriteBuffer<Float2> buffer;
        private readonly int n;
        private readonly int size;
        private readonly bool inverse;

        public FftShader(ReadWriteBuffer<Float2> buffer, int n, int size, bool inverse)
        {
            this.buffer = buffer;
            this.n = n;
            this.size = size;
            this.inverse = inverse;
        }

        public void Execute()
        {
            int i = ThreadIds.X;
            int halfSize = size / 2;
            int index = i % halfSize;
            int block = (i / halfSize) * size;
            int evenIndex = block + index;
            int oddIndex = evenIndex + halfSize;

            float angle = (inverse ? 2.0f : -2.0f) * 3.1415926535f * index / size;
            Float2 twiddle = new Float2(Hlsl.Cos(angle), Hlsl.Sin(angle));
            Float2 odd = buffer[oddIndex];

            Float2 temp = new Float2(
                odd.X * twiddle.X - odd.Y * twiddle.Y,
                odd.X * twiddle.Y + odd.Y * twiddle.X
            );

            Float2 even = buffer[evenIndex];
            buffer[evenIndex] = even + temp;
            buffer[oddIndex] = even - temp;
        }
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct ApplyEqShader : IComputeShader
    {
        private readonly ReadWriteBuffer<Float2> buffer;
        private readonly float sampleRate;
        private readonly float bassGain;
        private readonly float midGain;
        private readonly float trebleGain;

        public ApplyEqShader(ReadWriteBuffer<Float2> buffer, float sampleRate, float bassGain, float midGain, float trebleGain)
        {
            this.buffer = buffer;
            this.sampleRate = sampleRate;
            this.bassGain = bassGain;
            this.midGain = midGain;
            this.trebleGain = trebleGain;
        }

        public void Execute()
        {
            int i = ThreadIds.X;
            float freq = i * sampleRate / buffer.Length;
            float gain = 1.0f;

            if (freq < 250.0f) gain = bassGain;
            else if (freq < 4000.0f) gain = midGain;
            else gain = trebleGain;

            buffer[i] *= gain;
        }
    }

    [ThreadGroupSize(DefaultThreadGroupSizes.X)]
    [GeneratedComputeShaderDescriptor]
    public readonly partial struct EffectsChainShader : IComputeShader
    {
        private readonly ReadWriteBuffer<float> buffer;
        private readonly int bufferLength;
        private readonly int sampleRate;
        private readonly int offset;

        private readonly bool enableReverb;
        private readonly float reverbDelay;
        private readonly float reverbDecay;

        private readonly bool enableCompression;
        private readonly float compressionThreshold;
        private readonly float compressionRatio;

        private readonly bool enableLimiter;
        private readonly float limiterThreshold;

        private readonly bool enableDcOffsetRemoval;

        public EffectsChainShader(ReadWriteBuffer<float> buffer, int bufferLength, int sampleRate, int offset,
                                  bool enableReverb, float reverbDelay, float reverbDecay,
                                  bool enableCompression, float compressionThreshold, float compressionRatio,
                                  bool enableLimiter, float limiterThreshold,
                                  bool enableDcOffsetRemoval)
        {
            this.buffer = buffer;
            this.bufferLength = bufferLength;
            this.sampleRate = sampleRate;
            this.offset = offset;
            this.enableReverb = enableReverb;
            this.reverbDelay = reverbDelay;
            this.reverbDecay = reverbDecay;
            this.enableCompression = enableCompression;
            this.compressionThreshold = compressionThreshold;
            this.compressionRatio = compressionRatio;
            this.enableLimiter = enableLimiter;
            this.limiterThreshold = limiterThreshold;
            this.enableDcOffsetRemoval = enableDcOffsetRemoval;
        }

        public void Execute()
        {
            int i = ThreadIds.X + this.offset;
            if (i >= bufferLength) return;

            float sample = buffer[i];

            if (enableDcOffsetRemoval && i > 0)
            {
                sample = sample - buffer[i - 1];
            }

            if (enableCompression)
            {
                float absSample = Hlsl.Abs(sample);
                if (absSample > compressionThreshold)
                {
                    float excess = absSample - compressionThreshold;
                    float compressedExcess = excess / compressionRatio;
                    float gain = (compressionThreshold + compressedExcess) / absSample;
                    sample *= gain;
                }
            }

            if (enableReverb)
            {
                int delaySamples = (int)(reverbDelay * sampleRate / 1000);
                if (i >= delaySamples)
                {
                    sample += buffer[i - delaySamples] * reverbDecay;
                }
            }

            if (enableLimiter)
            {
                sample = Hlsl.Clamp(sample, -limiterThreshold, limiterThreshold);
            }

            buffer[i] = sample;
        }
    }
}
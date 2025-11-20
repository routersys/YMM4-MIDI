using System;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.Algorithms
{
    public class PolyphaseResampler4x
    {
        private const int Factor = 4;
        private const int Taps = 24;
        private readonly float[] coeffs;
        private readonly float[] inputBuffer;
        private int inputIndex;

        public PolyphaseResampler4x()
        {
            coeffs = new float[]
            {
                 0.000000f, -0.000235f, -0.001062f, -0.002384f, -0.002837f, 0.000000f, 0.006876f, 0.015791f,
                 0.020899f,  0.015635f,  0.000000f, -0.022200f, -0.041414f, -0.045943f, -0.029758f, 0.000000f,
                 0.034574f,  0.060601f,  0.066624f,  0.046429f,  0.000000f, -0.062309f, -0.119201f, -0.147709f,
                 0.850000f, -0.147709f, -0.119201f, -0.062309f,  0.000000f,  0.046429f,  0.066624f,  0.060601f,
                 0.034574f,  0.000000f, -0.029758f, -0.045943f, -0.041414f, -0.022200f,  0.000000f,  0.015635f,
                 0.020899f,  0.015791f,  0.006876f,  0.000000f, -0.002837f, -0.002384f, -0.001062f, -0.000235f
            };

            inputBuffer = new float[Taps];
        }

        public void Reset()
        {
            Array.Clear(inputBuffer, 0, inputBuffer.Length);
            inputIndex = 0;
        }

        public float Process(float input, Func<float, float> saturationFunc)
        {
            inputBuffer[inputIndex] = input;
            int ptr = inputIndex;
            inputIndex = (inputIndex + 1) % Taps;

            float outputAccumulator = 0;

            for (int phase = 0; phase < Factor; phase++)
            {
                double upsampled = 0;
                int coeffPtr = phase;
                int bufPtr = ptr;

                for (int t = 0; t < Taps / Factor; t++)
                {
                    if (coeffPtr < coeffs.Length)
                    {
                        upsampled += inputBuffer[bufPtr] * coeffs[coeffPtr];
                    }

                    bufPtr--;
                    if (bufPtr < 0) bufPtr += Taps;

                    coeffPtr += Factor;
                }

                float saturated = saturationFunc((float)(upsampled * Factor));
                outputAccumulator += saturated;
            }

            return outputAccumulator / Factor;
        }
    }
}
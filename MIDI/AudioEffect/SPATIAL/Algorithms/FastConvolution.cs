using System;
using NAudio.Dsp;

namespace MIDI.AudioEffect.SPATIAL.Algorithms
{
    public class FastConvolution : IConvolutionAlgorithm
    {
        private readonly int irLen;
        private readonly int blockLen;
        private readonly int fftSize;
        public int OutputSize => fftSize;

        private readonly Complex[] irFft;
        private readonly Complex[] blockFft;
        private readonly Complex[] convFft;
        private readonly float[] overlap;
        private readonly float[] output;
        private readonly int fftLog;

        public FastConvolution(float[] impulseResponse, int blockSize)
        {
            irLen = impulseResponse.Length;
            blockLen = blockSize;
            fftSize = 1;
            while (fftSize < irLen + blockLen - 1)
                fftSize *= 2;

            fftLog = (int)Math.Log(fftSize, 2);

            irFft = new Complex[fftSize];
            blockFft = new Complex[fftSize];
            convFft = new Complex[fftSize];
            overlap = new float[Math.Max(0, irLen - 1)];
            output = new float[fftSize];

            var irPadded = new float[fftSize];
            Array.Copy(impulseResponse, irPadded, irLen);

            for (int i = 0; i < fftSize; i++)
                irFft[i] = new Complex { X = irPadded[i], Y = 0 };

            FastFourierTransform.FFT(true, fftLog, irFft);
        }

        public void Reset()
        {
            Array.Clear(overlap, 0, overlap.Length);
        }

        public void Process(float[] input, float[] outputBuffer)
        {
            if (input.Length != blockLen)
                throw new ArgumentException("Input buffer size must match block size.");

            for (int i = 0; i < blockLen; i++)
                blockFft[i] = new Complex { X = input[i], Y = 0 };
            for (int i = blockLen; i < fftSize; i++)
                blockFft[i] = new Complex { X = 0, Y = 0 };

            FastFourierTransform.FFT(true, fftLog, blockFft);

            for (int i = 0; i < fftSize; i++)
            {
                float real = blockFft[i].X * irFft[i].X - blockFft[i].Y * irFft[i].Y;
                float imag = blockFft[i].X * irFft[i].Y + blockFft[i].Y * irFft[i].X;
                convFft[i] = new Complex { X = real, Y = imag };
            }

            FastFourierTransform.FFT(false, fftLog, convFft);

            for (int i = 0; i < fftSize; i++)
                output[i] = convFft[i].X / fftSize;

            int overlapLen = irLen - 1;
            if (overlapLen > 0)
            {
                for (int i = 0; i < overlapLen; i++)
                    output[i] += overlap[i];

                Array.Copy(output, blockLen, overlap, 0, overlapLen);
                if (fftSize - blockLen > overlapLen)
                {
                    Array.Clear(overlap, overlapLen, fftSize - blockLen - overlapLen);
                }
            }

            Array.Copy(output, 0, outputBuffer, 0, fftSize);
        }
    }
}
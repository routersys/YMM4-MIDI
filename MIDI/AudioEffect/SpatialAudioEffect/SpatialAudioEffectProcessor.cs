using System;
using YukkuriMovieMaker.Player.Audio.Effects;
using MIDI.AudioEffect.SpatialAudioEffect.Services;
using MIDI.AudioEffect.SpatialAudioEffect.Algorithms;

namespace MIDI.AudioEffect.SpatialAudioEffect
{
    public class SpatialAudioEffectProcessor : AudioEffectProcessorBase
    {
        private readonly SpatialAudioEffect item;
        private readonly TimeSpan duration;

        public override int Hz => Input?.Hz ?? 0;
        public override long Duration => (long)(duration.TotalSeconds * Input?.Hz ?? 0) * 2;

        private float[] inputBuffer = new float[4096];
        private float[] monoBuffer = new float[2048];

        private IConvolutionAlgorithm? convLeft = null;
        private IConvolutionAlgorithm? convRight = null;
        private IrLoaderService? loaderService = null;

        private string loadedIrLeft = "";
        private string loadedIrRight = "";
        private int lastHz = 0;

        private int inputBufferCount = 0;
        private int outputBufferCount = 0;
        private int outputBufferPos = 0;
        private float[] outputBufferLeft;
        private float[] outputBufferRight;

        private const int BlockSize = 1024;
        private float[] blockBuffer = new float[BlockSize];

        public SpatialAudioEffectProcessor(SpatialAudioEffect item, TimeSpan duration)
        {
            this.item = item;
            this.duration = duration;

            outputBufferLeft = new float[BlockSize * 2];
            outputBufferRight = new float[BlockSize * 2];
        }

        private void InitializeServices()
        {
            if (loaderService == null || lastHz != Hz)
            {
                loaderService = new IrLoaderService(Hz);
                lastHz = Hz;

                convLeft = null;
                convRight = null;
                loadedIrLeft = "";
                loadedIrRight = "";
            }
        }

        private void LoadImpulseResponse()
        {
            if (loaderService == null || Hz == 0) return;

            bool leftChanged = loadedIrLeft != item.IrFileLeft;
            bool rightChanged = loadedIrRight != item.IrFileRight;

            if (leftChanged || rightChanged)
            {
                (float[]? irL, float[]? irR) = loaderService.LoadIrPair(item.IrFileLeft, item.IrFileRight);

                convLeft = (irL != null) ? new FastConvolution(irL, BlockSize) : null;
                convRight = (irR != null) ? new FastConvolution(irR, BlockSize) : null;

                loadedIrLeft = item.IrFileLeft;
                loadedIrRight = item.IrFileRight;

                if (irL != null && irR != null)
                {
                    if (outputBufferLeft.Length < convLeft!.OutputSize)
                        outputBufferLeft = new float[convLeft.OutputSize];
                    if (outputBufferRight.Length < convRight!.OutputSize)
                        outputBufferRight = new float[convRight.OutputSize];
                }
            }
        }

        protected override void seek(long position)
        {
            Input?.Seek(position);
            convLeft?.Reset();
            convRight?.Reset();
            inputBufferCount = 0;
            outputBufferCount = 0;
            outputBufferPos = 0;
        }

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input == null) return 0;

            InitializeServices();
            LoadImpulseResponse();

            if (convLeft == null || convRight == null)
            {
                return Input.Read(destBuffer, offset, count);
            }

            int samplesWritten = 0;
            long currentFrame = Position / 2;
            long totalFrame = Duration / 2;
            int sampleRate = Hz;

            float gain = (float)Math.Pow(10, item.Gain.GetValue(currentFrame, totalFrame, sampleRate) / 20.0);

            while (samplesWritten < count)
            {
                if (outputBufferPos < outputBufferCount)
                {
                    int toWrite = Math.Min(count - samplesWritten, outputBufferCount - outputBufferPos);
                    for (int i = 0; i < toWrite; i += 2)
                    {
                        int monoIndex = (outputBufferPos + i) / 2;
                        if (monoIndex >= outputBufferLeft.Length) break;
                        destBuffer[offset + samplesWritten + i] = outputBufferLeft[monoIndex] * gain;
                        destBuffer[offset + samplesWritten + i + 1] = outputBufferRight[monoIndex] * gain;
                    }
                    outputBufferPos += toWrite;
                    samplesWritten += toWrite;
                }
                else
                {
                    int samplesNeeded = BlockSize - inputBufferCount;
                    int samplesToRead = samplesNeeded * 2;

                    if (inputBuffer.Length < samplesToRead)
                        inputBuffer = new float[samplesToRead];

                    int samplesRead = Input.Read(inputBuffer, 0, samplesToRead);
                    if (samplesRead == 0 && inputBufferCount == 0)
                        break;

                    int monoSamples = samplesRead / 2;
                    if (monoBuffer.Length < monoSamples)
                        monoBuffer = new float[monoSamples];

                    for (int i = 0; i < monoSamples; i++)
                    {
                        monoBuffer[i] = (inputBuffer[i * 2] + inputBuffer[i * 2 + 1]) * 0.5f;
                    }

                    int samplesToCopy = Math.Min(monoSamples, samplesNeeded);
                    Array.Copy(monoBuffer, 0, blockBuffer, inputBufferCount, samplesToCopy);
                    inputBufferCount += samplesToCopy;

                    if (inputBufferCount == BlockSize || (samplesRead == 0 && inputBufferCount > 0))
                    {
                        if (inputBufferCount < BlockSize)
                        {
                            Array.Clear(blockBuffer, inputBufferCount, BlockSize - inputBufferCount);
                        }

                        if (outputBufferLeft.Length < convLeft.OutputSize)
                            outputBufferLeft = new float[convLeft.OutputSize];
                        if (outputBufferRight.Length < convRight.OutputSize)
                            outputBufferRight = new float[convRight.OutputSize];

                        convLeft.Process(blockBuffer, outputBufferLeft);
                        convRight.Process(blockBuffer, outputBufferRight);

                        outputBufferCount = (convLeft.OutputSize) * 2;
                        outputBufferPos = 0;
                        inputBufferCount = 0;

                        if (samplesRead == 0)
                            break;
                    }
                }

                gain = (float)Math.Pow(10, item.Gain.GetValue(Position / 2 + samplesWritten / 2, totalFrame, sampleRate) / 20.0);
            }

            return samplesWritten;
        }
    }
}
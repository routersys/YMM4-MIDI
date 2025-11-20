using System;
using System.Collections.Generic;
using YukkuriMovieMaker.Player.Audio.Effects;
using MIDI.AudioEffect.SPATIAL.Services;
using MIDI.AudioEffect.SPATIAL.Algorithms;

namespace MIDI.AudioEffect.SPATIAL
{
    public class SpatialAudioEffectProcessor : AudioEffectProcessorBase
    {
        private readonly SpatialAudioEffect item;
        private readonly TimeSpan duration;
        private readonly object lockObject = new object();

        public override int Hz => Input?.Hz ?? 0;
        public override long Duration => (long)(duration.TotalSeconds * Input?.Hz ?? 0) * 2;

        private const int BlockSize = 1024;

        private IrLoaderService? loaderService;
        private IConvolutionAlgorithm? convLeft;
        private IConvolutionAlgorithm? convRight;
        private string loadedIrLeft = "";
        private string loadedIrRight = "";
        private int lastHz = 0;

        private readonly Queue<float> outputQueue = new Queue<float>();

        private float[] inputReadBuffer = new float[BlockSize * 2];
        private float[] dryBlockL = new float[BlockSize];
        private float[] dryBlockR = new float[BlockSize];
        private float[] wetBlockL = new float[BlockSize];
        private float[] wetBlockR = new float[BlockSize];

        private float[] convInputBuffer = new float[BlockSize];
        private float[] convOutputBufferL;
        private float[] convOutputBufferR;

        public SpatialAudioEffectProcessor(SpatialAudioEffect item, TimeSpan duration)
        {
            this.item = item;
            this.duration = duration;

            convOutputBufferL = new float[BlockSize * 2];
            convOutputBufferR = new float[BlockSize * 2];
        }

        private void InitializeServices()
        {
            if (loaderService == null || lastHz != Hz)
            {
                loaderService = new IrLoaderService(Hz);
                lastHz = Hz;
                ResetState();
            }
        }

        private void ResetState()
        {
            convLeft = null;
            convRight = null;
            loadedIrLeft = "";
            loadedIrRight = "";
            outputQueue.Clear();
            Array.Clear(inputReadBuffer, 0, inputReadBuffer.Length);

            convOutputBufferL = new float[BlockSize * 2];
            convOutputBufferR = new float[BlockSize * 2];
        }

        private void UpdateImpulseResponse()
        {
            if (loaderService == null || Hz == 0) return;

            if (loadedIrLeft != item.IrFileLeft || loadedIrRight != item.IrFileRight)
            {
                var (irL, irR) = loaderService.LoadIrPair(item.IrFileLeft, item.IrFileRight);

                convLeft = (irL != null) ? new FastConvolution(irL, BlockSize) : null;
                convRight = (irR != null) ? new FastConvolution(irR, BlockSize) : null;

                loadedIrLeft = item.IrFileLeft;
                loadedIrRight = item.IrFileRight;

                if (convLeft != null && convOutputBufferL.Length < convLeft.OutputSize)
                    convOutputBufferL = new float[convLeft.OutputSize];

                if (convRight != null && convOutputBufferR.Length < convRight.OutputSize)
                    convOutputBufferR = new float[convRight.OutputSize];
            }
        }

        protected override void seek(long position)
        {
            lock (lockObject)
            {
                Input?.Seek(position);
                convLeft?.Reset();
                convRight?.Reset();
                outputQueue.Clear();
            }
        }

        protected override int read(float[] destBuffer, int offset, int count)
        {
            if (Input == null) return 0;

            lock (lockObject)
            {
                InitializeServices();
                UpdateImpulseResponse();

                int samplesRequired = count;
                int samplesWritten = 0;

                while (outputQueue.Count < samplesRequired)
                {
                    ProcessNextBlock();

                    if (outputQueue.Count == 0) break;
                }

                int available = Math.Min(count, outputQueue.Count);
                for (int i = 0; i < available; i++)
                {
                    destBuffer[offset + i] = outputQueue.Dequeue();
                }
                samplesWritten = available;

                MeasureLevels(destBuffer, offset, samplesWritten);

                return samplesWritten;
            }
        }

        private void ProcessNextBlock()
        {
            if (Input == null) return;

            int samplesRead = Input.Read(inputReadBuffer, 0, BlockSize * 2);
            if (samplesRead == 0) return;

            int framesRead = samplesRead / 2;
            if (framesRead < BlockSize)
            {
                Array.Clear(inputReadBuffer, samplesRead, (BlockSize * 2) - samplesRead);
            }

            for (int i = 0; i < BlockSize; i++)
            {
                dryBlockL[i] = inputReadBuffer[i * 2];
                dryBlockR[i] = inputReadBuffer[i * 2 + 1];

                convInputBuffer[i] = (dryBlockL[i] + dryBlockR[i]) * 0.5f;
            }

            if (convLeft != null && convRight != null)
            {
                convLeft.Process(convInputBuffer, convOutputBufferL);
                convRight.Process(convInputBuffer, convOutputBufferR);

                Array.Copy(convOutputBufferL, 0, wetBlockL, 0, BlockSize);
                Array.Copy(convOutputBufferR, 0, wetBlockR, 0, BlockSize);
            }
            else
            {
                Array.Copy(dryBlockL, wetBlockL, BlockSize);
                Array.Copy(dryBlockR, wetBlockR, BlockSize);
            }

            MixAndEnqueue(framesRead);
        }

        private void MixAndEnqueue(int validFrames)
        {
            double mix = Math.Clamp(item.Mix / 100.0, 0.0, 1.0);
            double gainDb = item.Gain;
            double wetGain = Math.Pow(10.0, gainDb / 20.0);

            double dryLevel = 1.0 - mix;
            double wetLevel = mix * wetGain;

            for (int i = 0; i < validFrames; i++)
            {
                float outL = (float)(dryBlockL[i] * dryLevel + wetBlockL[i] * wetLevel);
                float outR = (float)(dryBlockR[i] * dryLevel + wetBlockR[i] * wetLevel);

                outputQueue.Enqueue(outL);
                outputQueue.Enqueue(outR);
            }
        }

        private void MeasureLevels(float[] buffer, int offset, int count)
        {
            float outputMax = 0;

            for (int i = 0; i < count; i++)
            {
                float val = Math.Abs(buffer[offset + i]);
                if (val > outputMax) outputMax = val;
            }

            item.OutputLevel = (outputMax <= 0) ? -60.0 : Math.Max(-60.0, 20.0 * Math.Log10(outputMax));

            MeasureInputLevelInBlock();
        }

        private void MeasureInputLevelInBlock()
        {
            float max = 0;
            for (int i = 0; i < BlockSize; i++)
            {
                max = Math.Max(max, Math.Abs(dryBlockL[i]));
                max = Math.Max(max, Math.Abs(dryBlockR[i]));
            }
            item.InputLevel = (max <= 0) ? -60.0 : Math.Max(-60.0, 20.0 * Math.Log10(max));
        }
    }
}
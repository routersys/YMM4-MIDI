using System;

namespace MIDI.AudioEffect.DELAY.Algorithms
{
    public class SimpleDelayProcessor : IDelayProcessor
    {
        private readonly float[] buffer;
        private readonly int sampleRate;
        private int writePosition;
        private int delaySamples;
        private double feedback;
        private double delayTimeMs;

        public SimpleDelayProcessor(int sampleRate, double maxDelayMs)
        {
            this.sampleRate = sampleRate;
            int bufferSize = (int)(maxDelayMs / 1000.0 * sampleRate) + 1;
            buffer = new float[bufferSize];
            Reset();
        }

        public double DelayTimeMs
        {
            get => delayTimeMs;
            set
            {
                delayTimeMs = value;
                delaySamples = (int)(delayTimeMs / 1000.0 * sampleRate);
                if (delaySamples > buffer.Length - 1) delaySamples = buffer.Length - 1;
                if (delaySamples < 0) delaySamples = 0;
            }
        }

        public double Feedback
        {
            get => feedback;
            set
            {
                feedback = Math.Clamp(value, 0, 0.99);
            }
        }

        public void Reset()
        {
            Array.Clear(buffer, 0, buffer.Length);
            writePosition = 0;
            delaySamples = 0;
            feedback = 0;
            delayTimeMs = 0;
        }

        public float Process(float input)
        {
            int readPosition = (writePosition - delaySamples + buffer.Length) % buffer.Length;
            float delayedSample = buffer[readPosition];

            float output = delayedSample;

            buffer[writePosition] = input + (float)(delayedSample * feedback);

            writePosition = (writePosition + 1) % buffer.Length;

            return output;
        }
    }
}
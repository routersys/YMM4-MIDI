using System;
using System.Collections.Generic;
using System.Linq;

namespace MIDI.Core.Audio
{
    public class AudioMeter
    {
        public double PeakLeft { get; private set; }
        public double PeakRight { get; private set; }
        public double RmsLeft { get; private set; }
        public double RmsRight { get; private set; }
        public double VuLeft { get; private set; }
        public double VuRight { get; private set; }

        public double MomentaryLoudness { get; private set; }
        public double ShortTermLoudness { get; private set; }
        public double IntegratedLoudness { get; private set; }

        private double peakDecayFactor = 0.999;
        private double vuAttackCoeff;
        private double vuReleaseCoeff;
        private double vuLeftValue;
        private double vuRightValue;

        private readonly EbuR128LoudnessMeter loudnessMeter;

        public AudioMeter(int sampleRate)
        {
            SetVuCoefficients(300);
            loudnessMeter = new EbuR128LoudnessMeter(sampleRate);
        }

        private void SetVuCoefficients(double responseTimeMs)
        {
            vuAttackCoeff = Math.Exp(-1.0 / (0.001 * responseTimeMs * 44100));
            vuReleaseCoeff = Math.Exp(-1.0 / (0.001 * responseTimeMs * 44100));
        }

        public void Process(ReadOnlySpan<float> buffer)
        {
            if (buffer.Length == 0)
            {
                PeakLeft *= peakDecayFactor;
                PeakRight *= peakDecayFactor;
                RmsLeft = 0;
                RmsRight = 0;
                vuLeftValue *= vuReleaseCoeff;
                vuRightValue *= vuReleaseCoeff;
                VuLeft = vuLeftValue;
                VuRight = vuRightValue;
                loudnessMeter.Process(buffer);
                UpdateLoudness();
                return;
            }

            double peakL = 0;
            double peakR = 0;
            double sumSquaresL = 0;
            double sumSquaresR = 0;
            int samples = buffer.Length / 2;

            for (int i = 0; i < samples; i++)
            {
                float sampleL = buffer[i * 2];
                float sampleR = buffer[i * 2 + 1];

                double absL = Math.Abs(sampleL);
                if (absL > peakL) peakL = absL;

                double absR = Math.Abs(sampleR);
                if (absR > peakR) peakR = absR;

                sumSquaresL += sampleL * sampleL;
                sumSquaresR += sampleR * sampleR;
            }

            PeakLeft = Math.Max(peakL, PeakLeft * peakDecayFactor);
            PeakRight = Math.Max(peakR, PeakRight * peakDecayFactor);

            RmsLeft = Math.Sqrt(sumSquaresL / samples);
            RmsRight = Math.Sqrt(sumSquaresR / samples);

            double rmsLeftDb = RmsLeft > 0 ? 20 * Math.Log10(RmsLeft) : -144.0;
            if (rmsLeftDb > vuLeftValue)
                vuLeftValue = vuLeftValue * vuAttackCoeff + (1 - vuAttackCoeff) * rmsLeftDb;
            else
                vuLeftValue = vuLeftValue * vuReleaseCoeff + (1 - vuReleaseCoeff) * rmsLeftDb;

            double rmsRightDb = RmsRight > 0 ? 20 * Math.Log10(RmsRight) : -144.0;
            if (rmsRightDb > vuRightValue)
                vuRightValue = vuRightValue * vuAttackCoeff + (1 - vuAttackCoeff) * rmsRightDb;
            else
                vuRightValue = vuRightValue * vuReleaseCoeff + (1 - vuReleaseCoeff) * rmsRightDb;

            VuLeft = vuLeftValue;
            VuRight = vuRightValue;

            loudnessMeter.Process(buffer);
            UpdateLoudness();
        }

        private void UpdateLoudness()
        {
            MomentaryLoudness = loudnessMeter.MomentaryLoudness;
            ShortTermLoudness = loudnessMeter.ShortTermLoudness;
            IntegratedLoudness = loudnessMeter.IntegratedLoudness;
        }

        public void ResetLoudness()
        {
            loudnessMeter.Reset();
        }

        private class EbuR128LoudnessMeter
        {
            private readonly int sampleRate;
            private readonly BiquadFilter preFilter;
            private readonly BiquadFilter highShelfFilter;

            private readonly List<double> momentaryBlockEnergies = new List<double>();
            private readonly List<double> shortTermBlockEnergies = new List<double>();
            private readonly List<double> integratedGatedEnergies = new List<double>();

            private int momentaryBlockSize;
            private int shortTermBlockSize;
            private int currentSampleIndex = 0;

            public double MomentaryLoudness { get; private set; } = -70.0;
            public double ShortTermLoudness { get; private set; } = -70.0;
            public double IntegratedLoudness { get; private set; } = -70.0;

            public EbuR128LoudnessMeter(int sampleRate)
            {
                this.sampleRate = sampleRate;

                preFilter = new BiquadFilter(sampleRate);
                preFilter.SetHighPass(38.0f, 0.5f);

                highShelfFilter = new BiquadFilter(sampleRate);
                highShelfFilter.SetHighShelf(1500.0f, 0.707f, 4.0f);

                momentaryBlockSize = (int)(0.4 * sampleRate);
                shortTermBlockSize = (int)(3.0 * sampleRate);
            }

            public void Reset()
            {
                momentaryBlockEnergies.Clear();
                shortTermBlockEnergies.Clear();
                integratedGatedEnergies.Clear();
                MomentaryLoudness = -70.0;
                ShortTermLoudness = -70.0;
                IntegratedLoudness = -70.0;
                currentSampleIndex = 0;
            }

            public void Process(ReadOnlySpan<float> buffer)
            {
                int samples = buffer.Length / 2;
                for (int i = 0; i < samples; i++)
                {
                    float left = buffer[i * 2];
                    float right = buffer[i * 2 + 1];

                    preFilter.Process(ref left, ref right);
                    highShelfFilter.Process(ref left, ref right);

                    double energy = left * left + right * right;

                    momentaryBlockEnergies.Add(energy);
                    shortTermBlockEnergies.Add(energy);

                    currentSampleIndex++;
                }

                if (momentaryBlockEnergies.Count > momentaryBlockSize)
                {
                    momentaryBlockEnergies.RemoveRange(0, momentaryBlockEnergies.Count - momentaryBlockSize);
                }
                if (shortTermBlockEnergies.Count > shortTermBlockSize)
                {
                    shortTermBlockEnergies.RemoveRange(0, shortTermBlockEnergies.Count - shortTermBlockSize);
                }

                CalculateLoudness();
            }

            private void CalculateLoudness()
            {
                MomentaryLoudness = CalculateGatedLoudness(momentaryBlockEnergies, -70.0);
                ShortTermLoudness = CalculateGatedLoudness(shortTermBlockEnergies, -70.0);

                var shortTermEnergy = shortTermBlockEnergies.Count > 0 ? shortTermBlockEnergies.Average() : 0;
                if (shortTermEnergy > 0 && ShortTermLoudness > -70.0)
                {
                    integratedGatedEnergies.Add(shortTermEnergy);
                }

                IntegratedLoudness = CalculateGatedLoudness(integratedGatedEnergies, -70.0, true);
            }

            private double CalculateGatedLoudness(List<double> energies, double absoluteThreshold, bool useRelativeGate = false)
            {
                if (energies.Count == 0) return -70.0;

                var gatedEnergies = energies.Where(e => -0.691 + 10 * Math.Log10(e) > absoluteThreshold).ToList();

                if (useRelativeGate && gatedEnergies.Any())
                {
                    double relativeThreshold = -0.691 + 10 * Math.Log10(gatedEnergies.Average()) - 10.0;
                    gatedEnergies = gatedEnergies.Where(e => -0.691 + 10 * Math.Log10(e) > relativeThreshold).ToList();
                }

                if (gatedEnergies.Count == 0) return -70.0;

                double meanEnergy = gatedEnergies.Average();
                return -0.691 + 10 * Math.Log10(meanEnergy);
            }
        }

        private class BiquadFilter
        {
            private readonly int sampleRate;
            private double a0, a1, a2, b1, b2;
            private float x1L, x2L, y1L, y2L;
            private float x1R, x2R, y1R, y2R;

            public BiquadFilter(int sr) { this.sampleRate = sr; }

            public void SetHighPass(float freq, float q)
            {
                double w0 = 2 * Math.PI * freq / sampleRate;
                double cosw0 = Math.Cos(w0);
                double alpha = Math.Sin(w0) / (2 * q);
                double b0_ = (1 + cosw0) / 2;
                double b1_ = -(1 + cosw0);
                double b2_ = (1 + cosw0) / 2;
                double a0_ = 1 + alpha;
                double a1_ = -2 * cosw0;
                double a2_ = 1 - alpha;
                a0 = b0_ / a0_; a1 = b1_ / a0_; a2 = b2_ / a0_;
                b1 = a1_ / a0_; b2 = a2_ / a0_;
            }

            public void SetHighShelf(float freq, float q, float gainDb)
            {
                double w0 = 2 * Math.PI * freq / sampleRate;
                double cosw0 = Math.Cos(w0);
                double alpha = Math.Sin(w0) / (2 * q);
                double A = Math.Pow(10, gainDb / 40);
                double b0_ = A * ((A + 1) + (A - 1) * cosw0 + 2 * Math.Sqrt(A) * alpha);
                double b1_ = -2 * A * ((A - 1) + (A + 1) * cosw0);
                double b2_ = A * ((A + 1) + (A - 1) * cosw0 - 2 * Math.Sqrt(A) * alpha);
                double a0_ = (A + 1) - (A - 1) * cosw0 + 2 * Math.Sqrt(A) * alpha;
                double a1_ = 2 * ((A - 1) - (A + 1) * cosw0);
                double a2_ = (A + 1) - (A - 1) * cosw0 - 2 * Math.Sqrt(A) * alpha;
                a0 = b0_ / a0_; a1 = b1_ / a0_; a2 = b2_ / a0_;
                b1 = a1_ / a0_; b2 = a2_ / a0_;
            }

            public void Process(ref float left, ref float right)
            {
                float outL = (float)(a0 * left + a1 * x1L + a2 * x2L - b1 * y1L - b2 * y2L);
                x2L = x1L; x1L = left; y2L = y1L; y1L = outL;
                left = outL;

                float outR = (float)(a0 * right + a1 * x1R + a2 * x2R - b1 * y1R - b2 * y2R);
                x2R = x1R; x1R = right; y2R = y1R; y1R = outR;
                right = outR;
            }
        }
    }
}
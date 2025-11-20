using System;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.Algorithms
{
    public class SaturatorUnit
    {
        private readonly PolyphaseResampler4x resampler;
        private readonly SimpleDcBlocker dcBlocker;
        private double drive;
        private double levelDb;
        private double levelLinear;
        private float driveCoef;

        public SaturatorUnit()
        {
            resampler = new PolyphaseResampler4x();
            dcBlocker = new SimpleDcBlocker();
            Drive = 0;
            LevelDb = 0;
        }

        public void Reset()
        {
            resampler.Reset();
            dcBlocker.Reset();
        }

        public double Drive
        {
            get => drive;
            set
            {
                drive = Math.Max(0, value);
                driveCoef = (float)(drive / 5.0);
            }
        }

        public double LevelDb
        {
            get => levelDb;
            set
            {
                levelDb = value;
                levelLinear = Math.Pow(10.0, levelDb / 20.0);
            }
        }

        public float Process(float input)
        {
            if (drive < 0.1)
            {
                return (float)(input * levelLinear);
            }

            float processed = resampler.Process(input, ApplySaturation);
            processed = dcBlocker.Process(processed);
            return (float)(processed * levelLinear);
        }

        private float ApplySaturation(float x)
        {
            float xDriven = x * (1.0f + driveCoef);

            double sat = Math.Tanh(xDriven);

            return (float)sat;
        }
    }
}
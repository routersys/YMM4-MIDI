using System;

namespace MIDI.AudioEffect.MULTIBAND_SATURATOR.Algorithms
{
    public class SaturatorUnit
    {
        private double drive;
        private double levelDb;
        private double levelLinear;

        public double Drive
        {
            get => drive;
            set => drive = Math.Max(0, value);
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

        public SaturatorUnit()
        {
            Drive = 0;
            LevelDb = 0;
        }

        public float Process(float input)
        {
            float dry = input;
            float driven = (float)(dry * (1.0 + drive * 0.5));

            float saturated;
            if (drive > 0)
            {
                saturated = (float)Math.Tanh(driven);

                if (Math.Abs(driven) > 2.0)
                {
                    saturated = (float)(saturated * 0.9 + Math.Sin(driven) * 0.1);
                }
            }
            else
            {
                saturated = dry;
            }

            return (float)(saturated * levelLinear);
        }
    }
}
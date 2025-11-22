using System;

namespace MIDI.AudioEffect.AMP_SIMULATOR.Algorithms
{
    public class DynamicsProcessor
    {
        private double gateEnv;
        private double gateGain;
        private bool gateOpen;

        private double sagEnv;

        private double limitEnv;

        public void Reset()
        {
            gateEnv = 0.0;
            gateGain = 0.0;
            gateOpen = false;
            sagEnv = 0.0;
            limitEnv = 0.0;
        }

        public double ProcessGate(double input, double sampleRate)
        {
            double abs = Math.Abs(input);
            double att = 1.0 - Math.Exp(-1.0 / (0.002 * sampleRate));
            double rel = 1.0 - Math.Exp(-1.0 / (0.100 * sampleRate));

            if (abs > gateEnv) gateEnv += (abs - gateEnv) * att;
            else gateEnv += (abs - gateEnv) * rel;

            if (gateEnv > 0.0006) gateOpen = true;
            else if (gateEnv < 0.0002) gateOpen = false;

            double target = gateOpen ? 1.0 : 0.0;
            gateGain += (target - gateGain) * 0.005;

            return input * gateGain;
        }

        public double ProcessSag(double input, double amount, double sampleRate)
        {
            double abs = Math.Abs(input);
            sagEnv += (abs - sagEnv) * 0.005;

            double attenuation = 1.0 / (1.0 + sagEnv * amount * 2.0);
            return input * attenuation;
        }

        public double ProcessLimiter(double input)
        {
            double peak = Math.Abs(input);
            if (peak > 1.0)
            {
                double gain = 1.0 / peak;
                if (gain < limitEnv) limitEnv = gain;
                else limitEnv += (gain - limitEnv) * 0.01;
            }
            else
            {
                limitEnv += (1.0 - limitEnv) * 0.001;
            }

            return input * limitEnv;
        }
    }
}
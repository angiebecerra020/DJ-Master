using System;

namespace DJMaster
{
    // RBJ cookbook biquad, transposed direct form II. One instance per audio channel.
    public sealed class Biquad
    {
        double b0 = 1, b1, b2, a1, a2;
        double z1, z2;

        public void SetLowpass(double sampleRate, double freq, double q)
        {
            Prepare(sampleRate, freq, q, out double cos, out double alpha);
            double a0 = 1 + alpha;
            b0 = (1 - cos) / 2 / a0; b1 = (1 - cos) / a0; b2 = b0;
            a1 = -2 * cos / a0; a2 = (1 - alpha) / a0;
        }

        public void SetHighpass(double sampleRate, double freq, double q)
        {
            Prepare(sampleRate, freq, q, out double cos, out double alpha);
            double a0 = 1 + alpha;
            b0 = (1 + cos) / 2 / a0; b1 = -(1 + cos) / a0; b2 = b0;
            a1 = -2 * cos / a0; a2 = (1 - alpha) / a0;
        }

        static void Prepare(double sampleRate, double freq, double q, out double cos, out double alpha)
        {
            freq = Math.Clamp(freq, 10, sampleRate * 0.45);
            double w = 2 * Math.PI * freq / sampleRate;
            cos = Math.Cos(w);
            alpha = Math.Sin(w) / (2 * q);
        }

        public float Process(float x)
        {
            double y = b0 * x + z1;
            z1 = b1 * x - a1 * y + z2;
            z2 = b2 * x - a2 * y;
            return (float)y;
        }

        public void Reset() { z1 = z2 = 0; }
    }
}

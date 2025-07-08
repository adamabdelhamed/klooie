using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
static class Biquad
{
    public struct State { public float z1, z2; }

    public static float Process(ref State s, float b0, float b1, float b2,
                                               float a1, float a2, float x)
    {
        float y = b0 * x + s.z1;
        s.z1 = b1 * x - a1 * y + s.z2;
        s.z2 = b2 * x - a2 * y;
        return y;
    }

    // bilinear transform helpers (SR = SoundProvider.SampleRate)
    static readonly float SR = SoundProvider.SampleRate;

    public static void DesignPeak(float fc, float q, float gainDb,
        out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float A = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * fc / SR;
        float alpha = MathF.Sin(w0) / (2f * q);

        float b0t = 1f + alpha * A;
        float b1t = -2f * MathF.Cos(w0);
        float b2t = 1f - alpha * A;
        float a0t = 1f + alpha / A;
        float a1t = -2f * MathF.Cos(w0);
        float a2t = 1f - alpha / A;

        b0 = b0t / a0t; b1 = b1t / a0t; b2 = b2t / a0t;
        a1 = a1t / a0t; a2 = a2t / a0t;
    }

    public static void DesignLowShelf(float fc, float gainDb,
        out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float A = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * fc / SR;
        float cosW = MathF.Cos(w0);
        float sinW = MathF.Sin(w0);
        float sqrtA = MathF.Sqrt(A);
        float beta = sinW * MathF.Sqrt((A + 1f / A) * (1f / 0.7071f - 1f) + 2f);

        float b0t = A * ((A + 1) - (A - 1) * cosW + beta);
        float b1t = 2f * A * ((A - 1) - (A + 1) * cosW);
        float b2t = A * ((A + 1) - (A - 1) * cosW - beta);
        float a0t = (A + 1) + (A - 1) * cosW + beta;
        float a1t = -2f * ((A - 1) + (A + 1) * cosW);
        float a2t = (A + 1) + (A - 1) * cosW - beta;

        b0 = b0t / a0t; b1 = b1t / a0t; b2 = b2t / a0t;
        a1 = a1t / a0t; a2 = a2t / a0t;
    }

    public static void DesignHighShelf(float fc, float gainDb,
        out float b0, out float b1, out float b2, out float a1, out float a2)
    {
        float A = MathF.Pow(10f, gainDb / 40f);
        float w0 = 2f * MathF.PI * fc / SR;
        float cosW = MathF.Cos(w0);
        float sinW = MathF.Sin(w0);
        float beta = sinW * MathF.Sqrt((A + 1f / A) * (1f / 0.7071f - 1f) + 2f);

        float b0t = A * ((A + 1) + (A - 1) * cosW + beta);
        float b1t = -2f * A * ((A - 1) + (A + 1) * cosW);
        float b2t = A * ((A + 1) + (A - 1) * cosW - beta);
        float a0t = (A + 1) - (A - 1) * cosW + beta;
        float a1t = 2f * ((A - 1) - (A + 1) * cosW);
        float a2t = (A + 1) - (A - 1) * cosW - beta;

        b0 = b0t / a0t; b1 = b1t / a0t; b2 = b2t / a0t;
        a1 = a1t / a0t; a2 = a2t / a0t;
    }
}

using System;

namespace klooie;

public sealed class AggroDistortionEffect : Recyclable, IEffect
{
    /* -------- parameters (public API) ------------------------------------ */
    private float preGain;          // how hard we hit stage 1
    private float stageRatio;       // gain drop per stage
    private float bias;             // asym-bias (0 = symm)
    private float makeup;           // output gain compensation

    /* -------- internal state --------------------------------------------- */
    private float z1, z2, z3;       // poly-phase accumulators
    private float lp;               // anti-alias LPF

    /* -------- pooling ----------------------------------------------------- */
    private static readonly LazyPool<AggroDistortionEffect> _pool =
        new(() => new AggroDistortionEffect());

    private AggroDistortionEffect() { }

    public static AggroDistortionEffect Create(
        float drive = 12f,       // hotter than before
        float stageRatio = 0.8f, // keep stages fairly hot
        float bias = 0.12f)      // subtle even-harmonics
    {
        var fx = _pool.Value.Rent();
        fx.preGain = drive;
        fx.stageRatio = stageRatio;
        fx.bias = bias;
        fx.makeup = 1f / MathF.Tanh(drive * 0.7f); // auto-level
        fx.Reset();
        return fx;
    }

    public IEffect Clone() => Create(preGain, stageRatio, bias);

    /* pre-compiled constants ---------------------------------------------- */
    private const int oversample = 4;
    private readonly float lpAlpha = 1f - MathF.Exp(-2f * MathF.PI * 7000f / SoundProvider.SampleRate);

    public float Process(float input, int frame, float time)
    {
        /* poly-phase 4× oversample ---------------------------------------- */
        float s = input, outSum = 0f;
        for (int p = 0; p < oversample; p++)
        {
            /* cheap linear upsample */
            float interp = (p switch
            {
                0 => z1,
                1 => 0.66f * z1 + 0.33f * s,
                2 => 0.33f * z1 + 0.66f * s,
                _ => s
            });

            z1 = s;

            outSum += Distort(interp);
        }

        /* down-sample & low-pass ------------------------------------------ */
        float down = outSum / oversample;
        lp += lpAlpha * (down - lp);
        return lp * makeup;
    }

    /* 3-stage soft-clip with bias ----------------------------------------- */
    private float Distort(float x)
    {
        float g1 = preGain;
        float g2 = g1 * stageRatio;
        float g3 = g2 * stageRatio;

        float y1 = MathF.Tanh(x * g1 + bias);
        float y2 = MathF.Tanh(y1 * g2 + bias);
        float y3 = MathF.Tanh(y2 * g3);
        return y3;
    }

    private void Reset()
    {
        z1 = z2 = z3 = lp = 0f;
    }

    protected override void OnReturn()
    {
        Reset();
        preGain = stageRatio = bias = makeup = 0f;
        base.OnReturn();
    }
}

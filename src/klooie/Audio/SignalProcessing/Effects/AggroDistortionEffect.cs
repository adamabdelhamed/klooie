using System;

namespace klooie;

[SynthDescription("""
Multi-stage distortion designed for saturated guitar tones.  Oversampling is
used to reduce aliasing so the result stays aggressive without unwanted
high‑frequency artifacts.
""")]
[SynthCategory("Distortion")]
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

    private Func<float, float> velocityCurve = EffectContext.EaseLinear;
    private float velocityScale = 1f;

    /* -------- pooling ----------------------------------------------------- */
    private static readonly LazyPool<AggroDistortionEffect> _pool =
        new(() => new AggroDistortionEffect());

    private AggroDistortionEffect() { }

    [SynthDescription("""
Settings used when constructing an AggroDistortionEffect.  These values
control how hard the signal is driven and how each stage behaves.
""")]
    public struct Settings
    {
        [SynthDescription("""
Pre-gain applied before the distortion stages.  Higher
values push the effect harder.
""")]
        public float Drive;

        [SynthDescription("""
Relative gain drop applied to each successive
distortion stage.
""")]
        public float StageRatio;

        [SynthDescription("""
Offset that introduces asymmetry into the clipping
curve.
""")]
        public float Bias;

        [SynthDescription("""
Function mapping note velocity to a gain
multiplier.
""")]
        public Func<float, float>? VelocityCurve;

        [SynthDescription("""
Overall multiplier applied after evaluating the
velocity curve.
""")]
        public float VelocityScale;
    }

    public static AggroDistortionEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.preGain = settings.Drive;
        fx.stageRatio = settings.StageRatio;
        fx.bias = settings.Bias;
        fx.makeup = 1f / MathF.Tanh(settings.Drive * 0.7f); // auto-level
        fx.Reset();
        fx.velocityCurve = settings.VelocityCurve ?? EffectContext.EaseLinear;
        fx.velocityScale = settings.VelocityScale;
        return fx;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            Drive = preGain,
            StageRatio = stageRatio,
            Bias = bias,
            VelocityCurve = velocityCurve,
            VelocityScale = velocityScale
        };
        return Create(in settings);
    }

    /* pre-compiled constants ---------------------------------------------- */
    private const int oversample = 4;
    private readonly float lpAlpha = 1f - MathF.Exp(-2f * MathF.PI * 7000f / SoundProvider.SampleRate);

    public float Process(in EffectContext ctx)
    {
        /* poly-phase 4× oversample ---------------------------------------- */
        float s = ctx.Input, outSum = 0f;
        float velFactor = 1f + velocityScale * (velocityCurve(ctx.VelocityNorm) - 1f);
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

            outSum += Distort(interp, velFactor);
        }

        /* down-sample & low-pass ------------------------------------------ */
        float down = outSum / oversample;
        lp += lpAlpha * (down - lp);
        return lp * makeup;
    }

    /* 3-stage soft-clip with bias ----------------------------------------- */
    private float Distort(float x, float velFactor)
    {
        float g1 = preGain * velFactor;
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
        velocityCurve = EffectContext.EaseLinear;
        velocityScale = 1f;
        base.OnReturn();
    }
}

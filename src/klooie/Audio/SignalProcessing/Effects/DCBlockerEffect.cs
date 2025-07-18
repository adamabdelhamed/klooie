using System;

namespace klooie;

/// <summary>
/// First-order DC-block / 15 Hz high-pass.
/// Removes sub-audible offsets that become “pops” after heavy drive.
/// </summary>
public sealed class DCBlockerEffect : Recyclable, IEffect
{
    private const float fCut = 15f;          // cutoff Hz
    private float a;                         // filter coefficient
    private float xPrev;                     // x[n-1]
    private float yPrev;                     // y[n-1]
    private bool velocityAffectsOutput;
    private Func<float, float> velocityCurve = EffectContext.EaseLinear;

    private static readonly LazyPool<DCBlockerEffect> _pool =
        new(() => new DCBlockerEffect());
    private DCBlockerEffect() { }

    public static DCBlockerEffect Create(bool velocityAffectsOutput = false,
        Func<float, float>? velocityCurve = null)
    {
        var fx = _pool.Value.Rent();
        float sr = SoundProvider.SampleRate;
        fx.a = (float)Math.Exp(-2.0 * Math.PI * fCut / sr);
        fx.xPrev = fx.yPrev = 0f;
        fx.velocityAffectsOutput = velocityAffectsOutput;
        fx.velocityCurve = velocityCurve ?? EffectContext.EaseLinear;
        return fx;
    }

    public IEffect Clone() => Create(velocityAffectsOutput, velocityCurve);

    public float Process(in EffectContext ctx)
    {
        float x = ctx.Input;
        // y[n] = x[n] - x[n-1] + a * y[n-1]
        float y = x - xPrev + a * yPrev;
        xPrev = x;
        yPrev = y;
        float outVal = y;
        if (velocityAffectsOutput)
            outVal *= velocityCurve(ctx.VelocityNorm);
        return outVal;
    }

    protected override void OnReturn()
    {
        a = xPrev = yPrev = 0f;
        velocityAffectsOutput = false;
        velocityCurve = EffectContext.EaseLinear;
        base.OnReturn();
    }
}

using System;

namespace klooie;

/// <summary>
/// First-order DC-block / 15 Hz high-pass.
/// Removes sub-audible offsets that become “pops” after heavy drive.
/// </summary>
[SynthDocumentation("""
Very low high-pass filter (around 15 Hz) that removes DC offsets.
Use this to eliminate sub-audible shifts that can cause pops after heavy
processing.
""")]
[SynthCategory("Filter")]
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

    [SynthDocumentation("""
Configuration options for the DC blocker including optional velocity
sensitivity.
""")]
    public struct Settings
    {
        [SynthDocumentation("""
When true, the output level is scaled by the
incoming note velocity.
""")]
        public bool VelocityAffectsOutput;

        [SynthDocumentation("""
Function applied to velocity when computing the
output scale.
""")]
        public Func<float, float>? VelocityCurve;
    }

    public static DCBlockerEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        float sr = SoundProvider.SampleRate;
        fx.a = (float)Math.Exp(-2.0 * Math.PI * fCut / sr);
        fx.xPrev = fx.yPrev = 0f;
        fx.velocityAffectsOutput = settings.VelocityAffectsOutput;
        fx.velocityCurve = settings.VelocityCurve ?? EffectContext.EaseLinear;
        return fx;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            VelocityAffectsOutput = velocityAffectsOutput,
            VelocityCurve = velocityCurve
        };
        return Create(in settings);
    }

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

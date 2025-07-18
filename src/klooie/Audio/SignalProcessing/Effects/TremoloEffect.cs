using System;

namespace klooie;
[SynthDescription("""
Amplitude tremolo modulation.
""")]
[SynthCategory("Modulation")]
public class TremoloEffect : Recyclable, IEffect
{
    private float depth;
    private float rateHz;
    private float phase;
    private bool velocityAffectsDepth;
    private Func<float, float> depthVelocityCurve = EffectContext.EaseLinear;

    private static readonly LazyPool<TremoloEffect> _pool = new(() => new TremoloEffect());
    protected TremoloEffect() { }

    [SynthDescription("""
    Parameters for TremoloEffect.
    """)]
    public struct Settings
    {
        [SynthDescription("""Depth of modulation (0-1).""")]
        public float Depth;

        [SynthDescription("""LFO rate in Hz.""")]
        public float RateHz;

        [SynthDescription("""When true, velocity scales the modulation depth.""")]
        public bool VelocityAffectsDepth;

        [SynthDescription("""Curve for velocity-based depth scaling.""")]
        public Func<float, float>? DepthVelocityCurve;
    }

    public static TremoloEffect Create(in Settings settings)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(settings.Depth, settings.RateHz);
        ret.velocityAffectsDepth = settings.VelocityAffectsDepth;
        ret.depthVelocityCurve = settings.DepthVelocityCurve ?? EffectContext.EaseLinear;
        return ret;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            Depth = depth,
            RateHz = rateHz,
            VelocityAffectsDepth = velocityAffectsDepth,
            DepthVelocityCurve = depthVelocityCurve
        };
        return Create(in settings);
    }

    protected void Construct(float depth, float rateHz)
    {
        this.depth = Math.Clamp(depth, 0f, 1f);
        this.rateHz = rateHz;
        this.phase = 0f;
    }

    public float Process(in EffectContext ctx)
    {
        float d = depth;
        if (velocityAffectsDepth)
            d *= depthVelocityCurve(ctx.VelocityNorm);
        float mod = 1f - d + d * (0.5f * (MathF.Sin(phase) + 1f));
        float output = ctx.Input * mod;
        phase += 2f * MathF.PI * rateHz / SoundProvider.SampleRate;
        if (phase > 2f * MathF.PI) phase -= 2f * MathF.PI;
        return output;
    }

    protected override void OnReturn()
    {
        depth = 0f;
        rateHz = 0f;
        phase = 0f;
        velocityAffectsDepth = false;
        depthVelocityCurve = EffectContext.EaseLinear;
        base.OnReturn();
    }
}

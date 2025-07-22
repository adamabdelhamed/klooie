using System;

namespace klooie;
[SynthDocumentation("""
Periodic volume modulation (tremolo) using a low-frequency oscillator.
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

    [SynthDocumentation("""
Settings for tremolo depth, rate and optional velocity-based scaling.
""")]
    public struct Settings
    {
        [SynthDocumentation("""
Amount of volume modulation from 0 (none) to 1
(full).
""")]
        public float Depth;

        [SynthDocumentation("""
Frequency of the modulation LFO in hertz.
""")]
        public float RateHz;

        [SynthDocumentation("""
If true, note velocity changes the modulation
depth.
""")]
        public bool VelocityAffectsDepth;

        [SynthDocumentation("""
Function mapping velocity to a depth multiplier.
""")]
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

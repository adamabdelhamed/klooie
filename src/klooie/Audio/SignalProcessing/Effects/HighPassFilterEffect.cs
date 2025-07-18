using System;

namespace klooie;
[SynthDescription("""
Basic high-pass filter for removing low frequencies below the chosen cutoff.
""")]
[SynthCategory("Filter")]
public class HighPassFilterEffect : Recyclable, IEffect
{
    private float prevInput;
    private float prevOutput;
    private float alpha;
    private float cutoffHz;
    private float mix = 1f;
    private bool velocityAffectsMix;
    private Func<float, float> mixVelocityCurve = EffectContext.EaseLinear;

    private static readonly LazyPool<HighPassFilterEffect> _pool = new(() => new HighPassFilterEffect());
    protected HighPassFilterEffect() { }

    [SynthDescription("""
Settings describing the cutoff frequency and how the mix reacts to
note velocity.
""")]
    public struct Settings
    {
        [SynthDescription("""
Frequency above which audio passes through,
measured in hertz.
""")]
        public float CutoffHz;

        [SynthDescription("""
Amount of filtered signal mixed with the original
(0 = dry, 1 = fully filtered).
""")]
        public float Mix;

        [SynthDescription("""
When true, harder played notes increase the mix of
the filtered signal.
""")]
        public bool VelocityAffectsMix;

        [SynthDescription("""
Function mapping normalized velocity to a
mix multiplier.
""")]
        public Func<float, float>? MixVelocityCurve;
    }

    public static HighPassFilterEffect Create(in Settings settings)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(settings.CutoffHz);
        ret.mix = settings.Mix;
        ret.velocityAffectsMix = settings.VelocityAffectsMix;
        ret.mixVelocityCurve = settings.MixVelocityCurve ?? EffectContext.EaseLinear;
        return ret;
    }

    protected void Construct(float cutoffHz)
    {
        float dt = 1f / SoundProvider.SampleRate;
        float rc = 1f / (2f * MathF.PI * cutoffHz);
        alpha = rc / (rc + dt);
        prevInput = 0f;
        prevOutput = 0f;
        this.cutoffHz = cutoffHz;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            CutoffHz = cutoffHz,
            Mix = mix,
            VelocityAffectsMix = velocityAffectsMix,
            MixVelocityCurve = mixVelocityCurve
        };
        return Create(in settings);
    }

    public float Process(in EffectContext ctx)
    {
        float input = ctx.Input;
        float output = alpha * (prevOutput + input - prevInput);
        prevInput = input;
        prevOutput = output;
        float mixAmt = mix;
        if (velocityAffectsMix)
            mixAmt *= mixVelocityCurve(ctx.VelocityNorm);
        return input * (1 - mixAmt) + output * mixAmt;
    }

    protected override void OnReturn()
    {
        prevInput = 0f;
        prevOutput = 0f;
        alpha = 0f;
        mix = 1f;
        velocityAffectsMix = false;
        mixVelocityCurve = EffectContext.EaseLinear;
        base.OnReturn();
    }
}

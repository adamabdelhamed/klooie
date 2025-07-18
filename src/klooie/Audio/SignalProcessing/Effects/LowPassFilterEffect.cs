using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
[SynthDescription("""
First-order low-pass filter.
""")]
[SynthCategory("Filter")]
class LowPassFilterEffect : Recyclable, IEffect
{
    private float alpha;
    private float state;
    private float cutoffHz;
    private float mix = 1f;
    private bool velocityAffectsMix;
    private Func<float, float> mixVelocityCurve = EffectContext.EaseLinear;

    private static readonly LazyPool<LowPassFilterEffect> _pool = new(() => new LowPassFilterEffect());

    private LowPassFilterEffect() { }

    [SynthDescription("""
    Parameters for LowPassFilterEffect.
    """)]
    public struct Settings
    {
        [SynthDescription("""Cutoff frequency in Hz.""")]
        public float CutoffHz;

        [SynthDescription("""Blend between dry and filtered signal.""")]
        public float Mix;

        [SynthDescription("""When true, velocity scales the mix amount.""")]
        public bool VelocityAffectsMix;

        [SynthDescription("""Curve for velocity-based mix scaling.""")]
        public Func<float, float>? MixVelocityCurve;
    }

    public static LowPassFilterEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.Construct(settings.CutoffHz);
        fx.mix = settings.Mix;
        fx.velocityAffectsMix = settings.VelocityAffectsMix;
        fx.mixVelocityCurve = settings.MixVelocityCurve ?? EffectContext.EaseLinear;
        return fx;
    }

    protected void Construct(float cutoffHz)
    {
        this.cutoffHz = cutoffHz;
        float dt = 1f / SoundProvider.SampleRate;
        float rc = 1f / (2f * MathF.PI * cutoffHz);
        alpha = dt / (rc + dt);
        state = 0f;
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
        state += alpha * (input - state);
        float wet = state;
        float mixAmt = mix;
        if (velocityAffectsMix)
            mixAmt *= mixVelocityCurve(ctx.VelocityNorm);
        return input * (1 - mixAmt) + wet * mixAmt;
    }

    protected override void OnReturn()
    {
        state = 0f;
        alpha = 0f;
        cutoffHz = 0f;
        mix = 1f;
        velocityAffectsMix = false;
        mixVelocityCurve = EffectContext.EaseLinear;
        base.OnReturn();
    }
}

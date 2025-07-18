using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
[SynthDescription("""
Simple low-pass filter that rolls off frequencies above the cutoff.
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
    Settings describing the cutoff frequency and how strongly the filtered
    signal is mixed in.
    """)]
    public struct Settings
    {
        [SynthDescription("""Cutoff frequency in hertz above which the signal is
        attenuated.""")]
        public float CutoffHz;

        [SynthDescription("""Mix level between the original and filtered signal
        (0 = dry, 1 = filtered).""")]
        public float Mix;

        [SynthDescription("""If true, note velocity changes how much of the
        filtered signal is heard.""")]
        public bool VelocityAffectsMix;

        [SynthDescription("""Function converting velocity into a mix
        multiplier.""")]
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

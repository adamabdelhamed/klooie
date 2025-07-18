using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
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

    public static LowPassFilterEffect Create(float cutoffHz = 200f,
        float mix = 1f,
        bool velocityAffectsMix = true,
        Func<float, float>? mixVelocityCurve = null)
    {
        var fx = _pool.Value.Rent();
        fx.Construct(cutoffHz);
        fx.mix = mix;
        fx.velocityAffectsMix = velocityAffectsMix;
        fx.mixVelocityCurve = mixVelocityCurve ?? EffectContext.EaseLinear;
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

    public IEffect Clone() => LowPassFilterEffect.Create(cutoffHz, mix, velocityAffectsMix, mixVelocityCurve);

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

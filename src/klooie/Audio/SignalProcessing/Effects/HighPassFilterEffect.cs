using System;

namespace klooie;
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

    public static HighPassFilterEffect Create(float cutoffHz = 200f,
        float mix = 1f,
        bool velocityAffectsMix = true,
        Func<float, float>? mixVelocityCurve = null)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(cutoffHz);
        ret.mix = mix;
        ret.velocityAffectsMix = velocityAffectsMix;
        ret.mixVelocityCurve = mixVelocityCurve ?? EffectContext.EaseLinear;
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

    public IEffect Clone() => HighPassFilterEffect.Create(cutoffHz, mix, velocityAffectsMix, mixVelocityCurve);

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

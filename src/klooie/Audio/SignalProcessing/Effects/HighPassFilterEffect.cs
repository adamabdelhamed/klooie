using System;

namespace klooie;
public class HighPassFilterEffect : Recyclable, IEffect
{
    private float prevInput;
    private float prevOutput;
    private float alpha;
    private float cutoffHz;

    private static readonly LazyPool<HighPassFilterEffect> _pool = new(() => new HighPassFilterEffect());
    protected HighPassFilterEffect() { }

    public static HighPassFilterEffect Create(float cutoffHz = 200f)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(cutoffHz);
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

    public IEffect Clone() => HighPassFilterEffect.Create(cutoffHz);

    public float Process(in EffectContext ctx)
    {
        float input = ctx.Input;
        float output = alpha * (prevOutput + input - prevInput);
        prevInput = input;
        prevOutput = output;
        return output;
    }

    protected override void OnReturn()
    {
        prevInput = 0f;
        prevOutput = 0f;
        alpha = 0f;
        base.OnReturn();
    }
}

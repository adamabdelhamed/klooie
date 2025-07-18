using System;

namespace klooie;
public class TremoloEffect : Recyclable, IEffect
{
    private float depth;
    private float rateHz;
    private float phase;

    private static readonly LazyPool<TremoloEffect> _pool = new(() => new TremoloEffect());
    protected TremoloEffect() { }

    public static TremoloEffect Create(float depth = 0.5f, float rateHz = 5f)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(depth, rateHz);
        return ret;
    }

    public IEffect Clone() => Create(depth, rateHz);

    protected void Construct(float depth, float rateHz)
    {
        this.depth = Math.Clamp(depth, 0f, 1f);
        this.rateHz = rateHz;
        this.phase = 0f;
    }

    public float Process(in EffectContext ctx)
    {
        float mod = 1f - depth + depth * (0.5f * (MathF.Sin(phase) + 1f));
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
        base.OnReturn();
    }
}

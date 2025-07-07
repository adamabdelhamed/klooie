using System;

namespace klooie;
public class TremoloEffect : Recyclable, IEffect
{
    private float depth;
    private float rateHz;
    private float phase;
    private float sampleRate;

    private static readonly LazyPool<TremoloEffect> _pool = new(() => new TremoloEffect());
    protected TremoloEffect() { }

    public static TremoloEffect Create(float depth = 0.5f, float rateHz = 5f, int sampleRate = SoundProvider.SampleRate)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(depth, rateHz, sampleRate);
        return ret;
    }

    protected void Construct(float depth, float rateHz, int sampleRate)
    {
        this.depth = Math.Clamp(depth, 0f, 1f);
        this.rateHz = rateHz;
        this.phase = 0f;
        this.sampleRate = sampleRate;
    }

    public float Process(float input, int frameIndex)
    {
        float mod = 1f - depth + depth * (0.5f * (MathF.Sin(phase) + 1f));
        float output = input * mod;
        phase += 2f * MathF.PI * rateHz / sampleRate;
        if (phase > 2f * MathF.PI) phase -= 2f * MathF.PI;
        return output;
    }

    protected override void OnReturn()
    {
        depth = 0f;
        rateHz = 0f;
        phase = 0f;
        sampleRate = 0f;
        base.OnReturn();
    }
}

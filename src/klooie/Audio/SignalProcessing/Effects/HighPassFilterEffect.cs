using System;

namespace klooie;
public class HighPassFilterEffect : Recyclable, IEffect
{
    private float prevInput;
    private float prevOutput;
    private float alpha;

    private static readonly LazyPool<HighPassFilterEffect> _pool = new(() => new HighPassFilterEffect());
    protected HighPassFilterEffect() { }

    public static HighPassFilterEffect Create(float cutoffHz = 200f, int sampleRate = SoundProvider.SampleRate)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(cutoffHz, sampleRate);
        return ret;
    }

    protected void Construct(float cutoffHz, int sampleRate)
    {
        float dt = 1f / sampleRate;
        float rc = 1f / (2f * MathF.PI * cutoffHz);
        alpha = rc / (rc + dt);
        prevInput = 0f;
        prevOutput = 0f;
    }

    public float Process(float input, int frameIndex)
    {
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

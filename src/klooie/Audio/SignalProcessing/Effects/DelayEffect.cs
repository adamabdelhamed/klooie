using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class DelayEffect : Recyclable, IEffect
{
    private float[] buffer;
    private int pos;
    private float feedback, mix;

    private static LazyPool<DelayEffect> _pool = new(() => new DelayEffect()); // Default 1 second delay at 44100Hz
    protected DelayEffect() { }
    public static DelayEffect Create(int delaySamples, float feedback = 0.3f, float mix = 0.4f)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(delaySamples, feedback, mix);
        return ret;
    }
    protected void Construct(int delaySamples, float feedback = 0.3f, float mix = 0.4f)
    {
        buffer = new float[delaySamples];
        this.feedback = feedback;
        this.mix = mix;
        pos = 0;
    }

    public float Process(float input, int frameIndex)
    {
        float delayed = buffer[pos];
        float output = (1 - mix) * input + mix * delayed;
        buffer[pos] = input + delayed * feedback;
        pos = (pos + 1) % buffer.Length;
        return output;
    }

    protected override void OnReturn()
    {
        Array.Clear(buffer, 0, buffer.Length);
        pos = 0;
        base.OnReturn();
    }
}

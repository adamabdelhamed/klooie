using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

class AllPassFilter : Recyclable
{
    private float[] buffer;
    private int pos;
    private float feedback;

    protected static LazyPool<AllPassFilter> _pool = new(() => new AllPassFilter());
    protected AllPassFilter() { }

    public static AllPassFilter Create(int delaySamples, float feedback)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(delaySamples, feedback);
        return ret;
    }

    protected void Construct(int delaySamples, float feedback)
    {
        buffer = new float[delaySamples];
        this.feedback = feedback;
        pos = 0;
    }

    public float Process(float input)
    {
        float bufOut = buffer[pos];
        float output = -input + bufOut;
        buffer[pos] = input + bufOut * feedback;
        pos = (pos + 1) % buffer.Length;
        return output;
    }

    protected override void OnReturn()
    {
        buffer = null;
        pos = 0;
    }
}


class CombFilter : Recyclable
{
    private float[] buffer;
    private int pos;
    private float feedback;

    private static LazyPool<CombFilter> _pool = new(() => new CombFilter());

    protected CombFilter() { }

    public static CombFilter Create(int delaySamples, float feedback)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(delaySamples, feedback);
        return ret;
    }

    protected void Construct(int delaySamples, float feedback)
    {
        buffer = new float[delaySamples];
        this.feedback = feedback;
        pos = 0;
    }

    public float Process(float input)
    {
        float output = buffer[pos];
        buffer[pos] = input + output * feedback;
        pos = (pos + 1) % buffer.Length;
        return output;
    }

    protected override void OnReturn()
    {
        buffer = buffer = null;
        pos = 0;
    }
}

public class ReverbEffect : Recyclable, IEffect
{
    private CombFilter[] combs;
    private AllPassFilter[] allpasses;
    private float feedback, diffusion, wet, dry;

    // Some classic reverb delay times (in samples, for 44.1kHz sample rate)
    private static readonly int[] combDelays = { 1557, 1617, 1491, 1422 };
    private static readonly int[] allpassDelays = { 225, 556 };

    private static LazyPool<ReverbEffect> _pool = new(() => new ReverbEffect());
    protected ReverbEffect() { }
    public static ReverbEffect Create(float feedback = 0.78f, float diffusion = 0.5f, float wet = 0.3f, float dry = 0.7f)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(feedback, diffusion, wet, dry);
        return ret;
    }

    protected void Construct(float feedback = 0.78f, float diffusion = 0.5f, float wet = 0.3f, float dry = 0.7f)
    {
        this.feedback = feedback;
        this.diffusion = diffusion;
        // Allocate combs
        combs = new CombFilter[combDelays.Length];
        for (int i = 0; i < combs.Length; i++)
            combs[i] = CombFilter.Create(combDelays[i], feedback);

        // Allocate allpasses
        allpasses = new AllPassFilter[allpassDelays.Length];
        for (int i = 0; i < allpasses.Length; i++)
            allpasses[i] = AllPassFilter.Create(allpassDelays[i], diffusion);

        this.wet = wet;
        this.dry = dry;
    }

    public IEffect Clone() => Create(feedback, diffusion, wet, dry);

    public float Process(float input, int frameIndex, float time)
    {
        // Mix combs in parallel
        float combOut = 0f;
        for (int i = 0; i < combs.Length; i++)
            combOut += combs[i].Process(input);
        combOut /= combs.Length;

        // Pass through allpasses in series
        float apOut = combOut;
        for (int i = 0; i < allpasses.Length; i++)
            apOut = allpasses[i].Process(apOut);

        // Mix wet/dry
        return dry * input + wet * apOut;
    }


    protected override void OnReturn()
    {
        for (int i = 0; i < combs.Length; i++)
        {
            CombFilter? c = combs[i];
            c.Dispose();
        }
        combs = null;
        for (int i1 = 0; i1 < allpasses.Length; i1++)
        {
            AllPassFilter? a = allpasses[i1];
            a.Dispose();
        }
        allpasses = null;
        base.OnReturn();
    }
}

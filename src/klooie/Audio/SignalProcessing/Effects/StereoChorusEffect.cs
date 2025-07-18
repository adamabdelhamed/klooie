using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class StereoChorusEffect : Recyclable, IEffect
{
    private float[] bufferL, bufferR;
    private int pos;
    private int delaySamples, depthSamples;
    private float rateHz, mix;
    private float phase;
    private int delayMs, depthMs;

    private static LazyPool<StereoChorusEffect> _pool = new(() => new StereoChorusEffect());
    protected StereoChorusEffect() { }
    public static StereoChorusEffect Create(int delayMs = 20, int depthMs = 6, float rateHz = 0.4f, float mix = 0.3f)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(delayMs, depthMs, rateHz, mix);
        return ret;
    }

    public IEffect Clone() => Create(delayMs, depthMs, rateHz, mix);

    protected void Construct(int delayMs = 20, int depthMs = 6, float rateHz = 0.4f, float mix = 0.3f)
    {
        this.delayMs = delayMs;
        this.depthMs = depthMs;
        delaySamples = (int)(delayMs * SoundProvider.SampleRate / 1000);
        depthSamples = (int)(depthMs * SoundProvider.SampleRate / 1000);
        bufferL = new float[delaySamples + depthSamples + 2];
        bufferR = new float[delaySamples + depthSamples + 2];
        this.rateHz = rateHz;
        this.mix = mix;
        pos = 0;
        phase = 0f;
    }

    public float Process(in EffectContext ctx)
    {
        // Mono version: pan modulation and width not shown; can be extended
        float mod = (float)Math.Sin(phase) * depthSamples;
        int readIndex = (int)((pos - delaySamples + mod + bufferL.Length) % bufferL.Length);

        float delayed = bufferL[readIndex];
        bufferL[pos] = ctx.Input;

        pos = (pos + 1) % bufferL.Length;
        phase += 2 * MathF.PI * rateHz / SoundProvider.SampleRate;
        if (phase > 2 * MathF.PI) phase -= 2 * MathF.PI;

        return (1 - mix) * ctx.Input + mix * delayed;
    }


    protected override void OnReturn() 
    { 
        bufferL = null;
        bufferR = null;
        pos = 0;
        phase = 0f; 
        base.OnReturn();
    }
}

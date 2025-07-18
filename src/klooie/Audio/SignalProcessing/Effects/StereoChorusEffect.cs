using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
[SynthDescription("""
Stereo chorus effect with delay modulation.
""")]
[SynthCategory("Modulation")]
public class StereoChorusEffect : Recyclable, IEffect
{
    private float[] bufferL, bufferR;
    private int pos;
    private int delaySamples, depthSamples;
    private float rateHz, mix;
    private bool velocityAffectsMix;
    private Func<float, float> mixVelocityCurve = EffectContext.EaseLinear;
    private float phase;
    private int delayMs, depthMs;

    private static LazyPool<StereoChorusEffect> _pool = new(() => new StereoChorusEffect());
    protected StereoChorusEffect() { }
    [SynthDescription("""
    Parameters for StereoChorusEffect.
    """)]
    public struct Settings
    {
        [SynthDescription("""Base delay in milliseconds.""")]
        public int DelayMs;

        [SynthDescription("""Modulation depth in milliseconds.""")]
        public int DepthMs;

        [SynthDescription("""LFO rate in Hz.""")]
        public float RateHz;

        [SynthDescription("""Blend between dry and modulated signal.""")]
        public float Mix;

        [SynthDescription("""Whether velocity scales the mix amount.""")]
        public bool VelocityAffectsMix;

        [SynthDescription("""Curve for velocity-based mix scaling.""")]
        public Func<float, float>? MixVelocityCurve;
    }

    public static StereoChorusEffect Create(in Settings settings)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(settings.DelayMs, settings.DepthMs, settings.RateHz, settings.Mix);
        ret.velocityAffectsMix = settings.VelocityAffectsMix;
        ret.mixVelocityCurve = settings.MixVelocityCurve ?? EffectContext.EaseLinear;
        return ret;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            DelayMs = delayMs,
            DepthMs = depthMs,
            RateHz = rateHz,
            Mix = mix,
            VelocityAffectsMix = velocityAffectsMix,
            MixVelocityCurve = mixVelocityCurve
        };
        return Create(in settings);
    }

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

        float mixAmt = mix;
        if (velocityAffectsMix)
            mixAmt *= mixVelocityCurve(ctx.VelocityNorm);
        return (1 - mixAmt) * ctx.Input + mixAmt * delayed;
    }


    protected override void OnReturn() 
    { 
        bufferL = null;
        bufferR = null;
        pos = 0;
        phase = 0f;
        velocityAffectsMix = false;
        mixVelocityCurve = EffectContext.EaseLinear;
        base.OnReturn();
    }
}

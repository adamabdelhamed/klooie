using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
[SynthDescription("""
Classic stereo chorus that modulates a short delay time to produce a wide,
swirling sound.
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
Settings defining the base delay time, modulation depth and how the mix
responds to note velocity.
""")]
    public struct Settings
    {
        [SynthDescription("""
Initial delay in milliseconds before modulation.
""")]
        public int DelayMs;

        [SynthDescription("""
Amount the delay is modulated in milliseconds.
""")]
        public int DepthMs;

        [SynthDescription("""
Speed of the modulation LFO in hertz.
""")]
        public float RateHz;

        [SynthDescription("""
Mix between the original and modulated signals
(0 = dry, 1 = fully wet).
""")]
        public float Mix;

        [SynthDescription("""
If true, note velocity affects the wet/dry mix.
""")]
        public bool VelocityAffectsMix;

        [SynthDescription("""
Function converting velocity to a mix multiplier.
""")]
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

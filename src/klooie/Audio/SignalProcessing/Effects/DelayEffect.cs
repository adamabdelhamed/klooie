using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
[SynthDescription("""
Implements a simple delay line that plays back the input after a fixed number
of samples.  The feedback setting routes some of the delayed signal back in so
you can create repeating echoes.
""")]
[SynthCategory("Delay")]
public class DelayEffect : Recyclable, IEffect
{
    private float[] buffer;
    private int pos;
    private float feedback, mix;
    private bool velocityAffectsMix;
    private Func<float, float> mixVelocityCurve = EffectContext.EaseLinear;

    private static LazyPool<DelayEffect> _pool = new(() => new DelayEffect()); // Default 1 second delay at 44100Hz
    protected DelayEffect() { }

    [SynthDescription("""
    Settings for constructing a DelayEffect.  DelaySamples is specified in
    audio samples while the other values range between 0 and 1.
    """)]
    public struct Settings
    {
        [SynthDescription("""Length of the delay buffer measured in samples.""")]
        public int DelaySamples;

        [SynthDescription("""Fraction of the delayed output that is fed back for
        repeated echoes (0 = none, 1 = infinite).""")]
        public float Feedback;

        [SynthDescription("""Mix between the original signal and the delayed
        signal. 0 gives only the dry signal, 1 gives only delay.""")]
        public float Mix;

        [SynthDescription("""When true, harder played notes increase the mix
        amount.""")]
        public bool VelocityAffectsMix;

        [SynthDescription("""Function that converts normalized velocity into a
        multiplier applied to the mix.""")]
        public Func<float, float>? MixVelocityCurve;
    }

    public static DelayEffect Create(in Settings settings)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(settings.DelaySamples, settings.Feedback, settings.Mix);
        ret.velocityAffectsMix = settings.VelocityAffectsMix;
        ret.mixVelocityCurve = settings.MixVelocityCurve ?? EffectContext.EaseLinear;
        return ret;
    }
    protected void Construct(int delaySamples, float feedback = 0.3f, float mix = 0.4f)
    {
        buffer = new float[delaySamples];
        this.feedback = feedback;
        this.mix = mix;
        pos = 0;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            DelaySamples = buffer.Length,
            Feedback = feedback,
            Mix = mix,
            VelocityAffectsMix = velocityAffectsMix,
            MixVelocityCurve = mixVelocityCurve
        };
        return Create(in settings);
    }

    public float Process(in EffectContext ctx)
    {
        float input = ctx.Input;
        float delayed = buffer[pos];
        float mixAmount = mix;
        if (velocityAffectsMix)
            mixAmount *= mixVelocityCurve(ctx.VelocityNorm);
        float output = (1 - mixAmount) * input + mixAmount * delayed;
        buffer[pos] = input + delayed * feedback;
        pos = (pos + 1) % buffer.Length;
        return output;
    }

    protected override void OnReturn()
    {
        Array.Clear(buffer, 0, buffer.Length);
        pos = 0;
        velocityAffectsMix = false;
        mixVelocityCurve = EffectContext.EaseLinear;
        base.OnReturn();
    }
}

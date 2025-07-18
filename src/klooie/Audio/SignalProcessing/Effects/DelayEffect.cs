using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
[SynthDescription("""
Creates a basic delay line that echoes the input after a
specified number of samples. Feedback controls how much of
the delayed signal is fed back for repeating echoes.
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
    Parameters used when creating a DelayEffect instance.
    All values are in sample or normalized units.
    """)]
    public struct Settings
    {
        [SynthDescription("""Number of samples to delay the signal.""")]
        public int DelaySamples;

        [SynthDescription("""Amount of feedback returned to the delay line (0-1).""")]
        public float Feedback;

        [SynthDescription("""Blend between dry and delayed signal (0-1).""")]
        public float Mix;

        [SynthDescription("""When true, note velocity scales the mix amount.""")]
        public bool VelocityAffectsMix;

        [SynthDescription("""Curve that maps normalized velocity to a mix multiplier.""")]
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

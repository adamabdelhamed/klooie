using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

// FadeInEffect: Multiplies input by [0,1] fade-in envelope over the given duration
[SynthDescription("""
Gradually raises the signal from silence over the given duration.  Useful for
smoothing in the start of a note or phrase.
""")]
[SynthCategory("Utility")]
public class FadeInEffect : Recyclable, IEffect
{
    private float fadeDuration;
    private bool finished;
    private Func<float, float> velocityCurve = EffectContext.EaseLinear;
    private float velocityScale = 1f;

    private static readonly LazyPool<FadeInEffect> _pool = new(() => new FadeInEffect());

    private FadeInEffect() { }

    [SynthDescription("""
Settings controlling the duration of the fade‑in and optional velocity
modulation.
""")]
    public struct Settings
    {
        [SynthDescription("""
How long the fade‑in lasts, measured in seconds.
""")]
        public float DurationSeconds;

        [SynthDescription("""
Function that maps note velocity to an additional
gain multiplier.
""")]
        public Func<float, float>? VelocityCurve;

        [SynthDescription("""
Multiplier applied after evaluating the velocity
curve.
""")]
        public float VelocityScale;
    }

    public static FadeInEffect Create(in Settings settings)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(settings.DurationSeconds);
        ret.velocityCurve = settings.VelocityCurve ?? EffectContext.EaseLinear;
        ret.velocityScale = settings.VelocityScale;
        return ret;
    }

    protected void Construct(float durationSeconds)
    {
        fadeDuration = durationSeconds;
        finished = false;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            DurationSeconds = fadeDuration,
            VelocityCurve = velocityCurve,
            VelocityScale = velocityScale
        };
        return Create(in settings);
    }

    public float Process(in EffectContext ctx)
    {
        float input = ctx.Input;
        float time = ctx.Time;
        if (finished || fadeDuration <= 0)
            return input;

        float gain = MathF.Min(1.0f, time / fadeDuration);
        gain *= velocityCurve(ctx.VelocityNorm) * velocityScale;
        if (gain >= 1.0f)
            finished = true;

        return input * gain;
    }

    protected override void OnReturn()
    {
        fadeDuration = 0;
        finished = false;
        velocityCurve = EffectContext.EaseLinear;
        velocityScale = 1f;
        base.OnReturn();
    }
}
[SynthDescription("""
Gradually lowers the signal to silence starting at the specified time.
Use this to smooth out the end of sustained notes.
""")]
[SynthCategory("Utility")]
public class FadeOutEffect : Recyclable, IEffect
{
    private float fadeDuration;
    private bool finished;
    private float fadeStartTime;
    private Func<float, float> velocityCurve = EffectContext.EaseLinear;
    private float velocityScale = 1f;

    private static readonly LazyPool<FadeOutEffect> _pool = new(() => new FadeOutEffect());

    private FadeOutEffect() { }

    /// <summary>
    /// durationSeconds: how long the fade should last
    /// fadeStartTime: time (in seconds) when fade should *start* (default = 0 to fade from the beginning)
    /// </summary>
[SynthDescription("""
Settings controlling when the fade begins, how long it lasts and optional
velocity scaling.
""")]
public struct Settings
{
    [SynthDescription("""
Length of the fade‑out in seconds.
""")]
    public float DurationSeconds;

    [SynthDescription("""
Time in seconds when the fade‑out begins.
""")]
    public float FadeStartTime;

    [SynthDescription("""
Function mapping note velocity to a gain factor
applied during the fade.
""")]
    public Func<float, float>? VelocityCurve;

    [SynthDescription("""
Multiplier applied to the result of the velocity
curve.
""")]
    public float VelocityScale;
}

    public static FadeOutEffect Create(in Settings settings)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(settings.DurationSeconds, settings.FadeStartTime);
        ret.velocityCurve = settings.VelocityCurve ?? EffectContext.EaseLinear;
        ret.velocityScale = settings.VelocityScale;
        return ret;
    }

    protected void Construct(float durationSeconds, float fadeStartTime)
    {
        fadeDuration = durationSeconds;
        this.fadeStartTime = fadeStartTime;
        finished = false;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            DurationSeconds = fadeDuration,
            FadeStartTime = fadeStartTime,
            VelocityCurve = velocityCurve,
            VelocityScale = velocityScale
        };
        return Create(in settings);
    }

    public float Process(in EffectContext ctx)
    {
        float input = ctx.Input;
        float time = ctx.Time;
        if (finished || fadeDuration <= 0)
            return input;

        if (time < fadeStartTime)
            return input;

        float gain = 1.0f - MathF.Min(1.0f, (time - fadeStartTime) / fadeDuration);
        gain *= velocityCurve(ctx.VelocityNorm) * velocityScale;
        if (gain <= 0f)
        {
            finished = true;
            return 0f;
        }

        return input * gain;
    }

    protected override void OnReturn()
    {
        fadeDuration = 0;
        fadeStartTime = 0;
        finished = false;
        velocityCurve = EffectContext.EaseLinear;
        velocityScale = 1f;
        base.OnReturn();
    }
}



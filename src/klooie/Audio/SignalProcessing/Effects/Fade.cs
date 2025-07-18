using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

// FadeInEffect: Multiplies input by [0,1] fade-in envelope over the given duration
public class FadeInEffect : Recyclable, IEffect
{
    private float fadeDuration;
    private bool finished;
    private Func<float, float> velocityCurve = EffectContext.EaseLinear;
    private float velocityScale = 1f;

    private static readonly LazyPool<FadeInEffect> _pool = new(() => new FadeInEffect());

    private FadeInEffect() { }

    public static FadeInEffect Create(float durationSeconds,
        Func<float, float>? velocityCurve = null,
        float velocityScale = 1f)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(durationSeconds);
        ret.velocityCurve = velocityCurve ?? EffectContext.EaseLinear;
        ret.velocityScale = velocityScale;
        return ret;
    }

    protected void Construct(float durationSeconds)
    {
        fadeDuration = durationSeconds;
        finished = false;
    }

    public IEffect Clone() => Create(fadeDuration, velocityCurve, velocityScale);

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
    public static FadeOutEffect Create(float durationSeconds, float fadeStartTime = 0,
        Func<float, float>? velocityCurve = null,
        float velocityScale = 1f)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(durationSeconds, fadeStartTime);
        ret.velocityCurve = velocityCurve ?? EffectContext.EaseLinear;
        ret.velocityScale = velocityScale;
        return ret;
    }

    protected void Construct(float durationSeconds, float fadeStartTime)
    {
        fadeDuration = durationSeconds;
        this.fadeStartTime = fadeStartTime;
        finished = false;
    }

    public IEffect Clone() => Create(fadeDuration, fadeStartTime, velocityCurve, velocityScale);

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



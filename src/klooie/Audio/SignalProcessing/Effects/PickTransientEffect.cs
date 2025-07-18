using System;

namespace klooie;

/// <summary>
/// Injects a decaying broadband noise burst at note-on.
/// Duration & level track the patch’s <c>TransientDurationSeconds</c>.
/// Place very early in the chain (before distortion) for realism.
/// </summary>
[SynthDescription("""
Inserts a brief burst of noise when a note begins.  This mimics the pick or
pluck sound heard on real instruments.
""")]
[SynthCategory("Dynamics")]
public sealed class PickTransientEffect : Recyclable, IEffect
{
    private float duration;
    private float gain;
    private float timeSinceOn;
    private bool active;
    private Random rng;

    private static readonly LazyPool<PickTransientEffect> _pool =
        new(() => new PickTransientEffect());
    private PickTransientEffect() { rng = new Random(); }

    [SynthDescription("""
Settings controlling how long the noise burst lasts and how loud it is.
""")]
    public struct Settings
    {
        [SynthDescription("""
Length of the noise burst in seconds.
""")]
        public float Duration;

        [SynthDescription("""
Volume of the transient noise.
""")]
        public float Gain;
    }

    public static PickTransientEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.duration = settings.Duration;
        fx.gain = settings.Gain;
        fx.timeSinceOn = 0f;
        fx.active = true;
        return fx;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            Duration = duration,
            Gain = gain
        };
        return Create(in settings);
    }

    public float Process(in EffectContext ctx)
    {
        float x = ctx.Input;
        if (!active) return x;

        timeSinceOn += 1f / SoundProvider.SampleRate;
        float env = 1f - timeSinceOn / duration;
        if (env <= 0f) { active = false; return x; }

        /* white-ish noise -------------------------------------------------- */
        float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
        return x + noise * gain * env;
    }

    protected override void OnReturn()
    {
        duration = gain = timeSinceOn = 0f;
        active = false;
        base.OnReturn();
    }
}

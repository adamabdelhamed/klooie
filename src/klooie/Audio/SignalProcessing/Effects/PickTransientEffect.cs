using System;

namespace klooie;

/// <summary>
/// Injects a decaying broadband noise burst at note-on.
/// Duration & level track the patch’s <c>TransientDurationSeconds</c>.
/// Place very early in the chain (before distortion) for realism.
/// </summary>
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

    public static PickTransientEffect Create(float duration = .005f, float gain = .6f)
    {
        var fx = _pool.Value.Rent();
        fx.duration = duration;
        fx.gain = gain;
        fx.timeSinceOn = 0f;
        fx.active = true;
        return fx;
    }

    public IEffect Clone() => Create(duration, gain);

    public float Process(float x, int frame, float time)
    {
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

using System;

namespace klooie;

/// <summary>
/// Simple peak/RMS hybrid compressor – great for taming post-amp transients
/// without crushing pick attack.
/// </summary>
public sealed class CompressorEffect : Recyclable, IEffect
{
    /* --- tunables ------------------------------------------------------- */
    private float threshold;   // linear (0-1); e.g. 0.6 ≅ -4 dBFS
    private float ratio;       // 2–10
    private float attack;      // 0-1, higher = faster
    private float release;     // 0-1, higher = faster

    /* --- state ---------------------------------------------------------- */
    private float envelope;

    /* --- pooling -------------------------------------------------------- */
    private static readonly LazyPool<CompressorEffect> _pool = new(() => new CompressorEffect());
    private CompressorEffect() { }

    public static CompressorEffect Create(
        float threshold = 0.6f,
        float ratio = 4f,
        float attack = 0.01f,
        float release = 0.005f)
    {
        var fx = _pool.Value.Rent();
        fx.threshold = threshold;
        fx.ratio = ratio;
        fx.attack = attack;
        fx.release = release;
        fx.envelope = 0f;
        return fx;
    }

    public IEffect Clone() => Create(threshold, ratio, attack, release);

    public float Process(float input, int frameIndex, float time)
    {
        float level = MathF.Abs(input);

        /* envelope follower ------------------------------------------------*/
        envelope += (level > envelope)
            ? (level - envelope) * attack
            : (level - envelope) * release;

        /* gain computer ----------------------------------------------------*/
        float gain = 1f;
        if (envelope > threshold && envelope > 1e-9f)
        {
            float over = envelope - threshold;
            float compressed = threshold + over / ratio;   // soft-knee-ish
            gain = compressed / envelope;
        }

        return input * gain;
    }

    protected override void OnReturn()
    {
        threshold = ratio = attack = release = envelope = 0f;
        base.OnReturn();
    }
}

using System;

namespace klooie;

/// <summary>
/// Simple peak/RMS hybrid compressor – great for taming post-amp transients
/// without crushing pick attack.
/// </summary>
[SynthDescription("""
Peak/RMS hybrid compressor for controlling dynamic range.
""")]
[SynthCategory("Dynamics")]
public sealed class CompressorEffect : Recyclable, IEffect
{
    /* --- tunables ------------------------------------------------------- */
    private float threshold;   // linear (0-1); e.g. 0.6 ≅ -4 dBFS
    private float ratio;       // 2–10
    private float attack;      // 0-1, higher = faster
    private float release;     // 0-1, higher = faster

    /* --- state ---------------------------------------------------------- */
    private float envelope;

    private Func<float, float> velocityCurve = EffectContext.EaseLinear;
    private float velocityScale = 1f;

    /* --- pooling -------------------------------------------------------- */
    private static readonly LazyPool<CompressorEffect> _pool = new(() => new CompressorEffect());
    private CompressorEffect() { }

    [SynthDescription("""
    Parameters for CompressorEffect.
    """)]
    public struct Settings
    {
        [SynthDescription("""Threshold level before compression occurs.""")]
        public float Threshold;

        [SynthDescription("""Compression ratio.""")]
        public float Ratio;

        [SynthDescription("""Attack time coefficient.""")]
        public float Attack;

        [SynthDescription("""Release time coefficient.""")]
        public float Release;

        [SynthDescription("""Velocity-to-gain curve.""")]
        public Func<float, float>? VelocityCurve;

        [SynthDescription("""Scale factor for velocity curve.""")]
        public float VelocityScale;
    }

    public static CompressorEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.threshold = settings.Threshold;
        fx.ratio = settings.Ratio;
        fx.attack = settings.Attack;
        fx.release = settings.Release;
        fx.envelope = 0f;
        fx.velocityCurve = settings.VelocityCurve ?? EffectContext.EaseLinear;
        fx.velocityScale = settings.VelocityScale;
        return fx;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            Threshold = threshold,
            Ratio = ratio,
            Attack = attack,
            Release = release,
            VelocityCurve = velocityCurve,
            VelocityScale = velocityScale
        };
        return Create(in settings);
    }

    public float Process(in EffectContext ctx)
    {
        float input = ctx.Input;
        float velFactor = velocityCurve(ctx.VelocityNorm) * velocityScale;
        float thresholdMod = threshold * velFactor;
        float level = MathF.Abs(input);

        /* envelope follower ------------------------------------------------*/
        envelope += (level > envelope)
            ? (level - envelope) * attack
            : (level - envelope) * release;

        /* gain computer ----------------------------------------------------*/
        float gain = 1f;
        if (envelope > thresholdMod && envelope > 1e-9f)
        {
            float over = envelope - thresholdMod;
            float compressed = thresholdMod + over / ratio;   // soft-knee-ish
            gain = compressed / envelope;
        }

        return input * gain;
    }

    protected override void OnReturn()
    {
        threshold = ratio = attack = release = envelope = 0f;
        velocityCurve = EffectContext.EaseLinear;
        velocityScale = 1f;
        base.OnReturn();
    }
}

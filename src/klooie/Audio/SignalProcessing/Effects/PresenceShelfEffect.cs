using System;

namespace klooie;

/// <summary>
/// Resonant low-pass (≈3.3 kHz, Q≈3) *into* a high-shelf booster.
/// Emulates the electrical resonance of passive guitar pickups
/// and the presence control on high-gain amps.
/// </summary>
[SynthDescription("""
Combines a resonant low‑pass with a high‑shelf boost to mimic the presence
control found on many guitar amplifiers.
""")]
[SynthCategory("Filter")]
public sealed class PresenceShelfEffect : Recyclable, IEffect
{
    /* ---- parameters ---------------------------------------------------- */
    private float shelfGain;        // linear (1 = flat, 2 = +6 dB)
    private const float fRes = 3300f;      // resonance centre Hz
    private const float qRes = 3.0f;       // resonance Q
    private const float fShelf = 4800f;     // shelf knee  Hz

    /* ---- state ---------------------------------------------------------- */
    private float resY1, resY2;     // biquad delay
    private float lp;               // 1-pole LP for shelf split
    private Func<float, float> velocityCurve = EffectContext.EaseLinear;
    private float velocityScale = 1f;

    /* ---- coefficients --------------------------------------------------- */
    private float b0, b1, b2, a1, a2; // biquad (resonant LP)
    private float alphaShelf;

    private static readonly LazyPool<PresenceShelfEffect> _pool =
        new(() => new PresenceShelfEffect());

    private PresenceShelfEffect() { }

    [SynthDescription("""
    Settings controlling the amount of high-frequency boost and optional
    velocity-based scaling.
    """)]
    public struct Settings
    {
        [SynthDescription("""Boost applied to the high frequencies in decibels.""")]
        public float PresenceDb;

        [SynthDescription("""Function used to scale the boost based on note
        velocity.""")]
        public Func<float, float>? VelocityCurve;

        [SynthDescription("""Additional multiplier applied to the velocity
        curve's output.""")]
        public float VelocityScale;
    }

    public static PresenceShelfEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.shelfGain = MathF.Pow(10f, settings.PresenceDb / 20f);
        fx.Configure();
        fx.resY1 = fx.resY2 = fx.lp = 0f;
        fx.velocityCurve = settings.VelocityCurve ?? EffectContext.EaseLinear;
        fx.velocityScale = settings.VelocityScale;
        return fx;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            PresenceDb = 20f * MathF.Log10(shelfGain),
            VelocityCurve = velocityCurve,
            VelocityScale = velocityScale
        };
        return Create(in settings);
    }

    /* ----- core ---------------------------------------------------------- */
    public float Process(in EffectContext ctx)
    {
        /* resonant LP ----------------------------------------------------- */
        float x = ctx.Input;
        float y = b0 * x + b1 * resY1 + b2 * resY2 - a1 * resY1 - a2 * resY2;
        resY2 = resY1;
        resY1 = y;

        /* presence shelf --------------------------------------------------- */
        lp += alphaShelf * (y - lp);
        float high = y - lp;             // >4.8 kHz
        float gain = shelfGain * (1f + velocityScale * (velocityCurve(ctx.VelocityNorm) - 1f));
        return lp + high * gain;
    }

    /* ----- helpers ------------------------------------------------------- */
    private void Configure()
    {
        /* biquad coefficients (RBJ low-pass) ------------------------------- */
        float fs = SoundProvider.SampleRate;
        float w0 = 2f * MathF.PI * fRes / fs;
        float cosW = MathF.Cos(w0);
        float sinW = MathF.Sin(w0);
        float alpha = sinW / (2f * qRes);

        float b0N = (1f - cosW) / 2f;
        float b1N = 1f - cosW;
        float b2N = (1f - cosW) / 2f;
        float a0 = 1f + alpha;
        float a1N = -2f * cosW;
        float a2N = 1f - alpha;

        b0 = b0N / a0;
        b1 = b1N / a0;
        b2 = b2N / a0;
        a1 = a1N / a0;
        a2 = a2N / a0;

        /* shelf LP coefficient -------------------------------------------- */
        alphaShelf = 1f - MathF.Exp(-2f * MathF.PI * fShelf / fs);
    }

    protected override void OnReturn()
    {
        shelfGain = resY1 = resY2 = lp = 0f;
        b0 = b1 = b2 = a1 = a2 = alphaShelf = 0f;
        velocityCurve = EffectContext.EaseLinear;
        velocityScale = 1f;
        base.OnReturn();
    }
}

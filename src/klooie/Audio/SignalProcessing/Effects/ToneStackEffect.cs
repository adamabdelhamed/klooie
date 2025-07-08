using System;

namespace klooie;

/// <summary>
/// Classic 3–knob guitar tone stack (Bass / Mid / Treble) implemented as
/// three first-order bands:
/// •  <c>low  &lt;  250 Hz</c>
/// •  <c>mid  250 – 2 500 Hz</c>
/// •  <c>high &gt;  2 500 Hz</c>
/// Gains are linear (1 = unity, 2 = +6 dB, 0.5 = -6 dB).
/// </summary>
public sealed class ToneStackEffect : Recyclable, IEffect
{
    /* -------------------------------------------------------------------- */
    private float bassG, midG, trebG;
    private float lowLpf, highLpf;

    /* coefficients -------------------------------------------------------- */
    private static readonly float alphaLow =
        1f - MathF.Exp(-2f * MathF.PI * 250f / SoundProvider.SampleRate);
    private static readonly float alphaHigh =
        1f - MathF.Exp(-2f * MathF.PI * 2_500f / SoundProvider.SampleRate);

    private static readonly LazyPool<ToneStackEffect> _pool =
        new(() => new ToneStackEffect());
    private ToneStackEffect() { }

    public static ToneStackEffect Create(float bass = 1f, float mid = 1f, float treble = 1f)
    {
        var fx = _pool.Value.Rent();
        fx.bassG = bass;
        fx.midG = mid;
        fx.trebG = treble;
        fx.lowLpf = 0f;
        fx.highLpf = 0f;
        return fx;
    }

    public IEffect Clone() => Create(bassG, midG, trebG);

    public float Process(float x, int frame, float time)
    {
        /* low band --------------------------------------------------------- */
        lowLpf += alphaLow * (x - lowLpf);          // LPF @250 Hz
        float low = lowLpf;

        /* temp high-LPF for high-band split -------------------------------- */
        highLpf += alphaHigh * (x - highLpf);         // LPF @2 500 Hz

        float high = x - highLpf;                     // >2 500 Hz
        float mid = x - low - high;                  // residual 250–2 500 Hz

        return low * bassG +
               mid * midG +
               high * trebG;
    }

    protected override void OnReturn()
    {
        bassG = midG = trebG = lowLpf = highLpf = 0f;
        base.OnReturn();
    }
}

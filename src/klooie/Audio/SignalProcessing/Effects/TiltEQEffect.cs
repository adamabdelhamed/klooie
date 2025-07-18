using System;

namespace klooie;

/// <summary>
/// First-order tilt-EQ centred around a gentle low-pass.
/// Positive <c>tilt</c> brightens; negative warms.
/// </summary>
[SynthDescription("""
First-order tilt EQ balancing lows and highs around a fixed cutoff.
""")]
[SynthCategory("Filter")]
public sealed class TiltEQEffect : Recyclable, IEffect
{
    private float tilt;     // -1 (bass boost) … +1 (treble boost)
    private float alpha;    // LPF coefficient computed from cutoff
    private float low;      // running low-passed state
    private float cutoffHz;

    private static readonly LazyPool<TiltEQEffect> _pool = new(() => new TiltEQEffect());
    private TiltEQEffect() { }

    [SynthDescription("""
    Parameters for TiltEQEffect.
    """)]
    public struct Settings
    {
        [SynthDescription("""Tilt amount from -1 to +1.""")]
        public float Tilt;

        [SynthDescription("""Cutoff frequency for the underlying low-pass.""")]
        public float CutoffHz;
    }

    public static TiltEQEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.Construct(settings.Tilt, settings.CutoffHz);
        return fx;
    }

    private void Construct(float tilt, float cutoffHz)
    {
        this.tilt = tilt;
        this.cutoffHz = cutoffHz;
        float dt = 1f / SoundProvider.SampleRate;
        float rc = 1f / (2f * MathF.PI * cutoffHz);
        alpha = dt / (rc + dt);
        low = 0f;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            Tilt = tilt,
            CutoffHz = cutoffHz
        };
        return Create(in settings);
    }

    public float Process(in EffectContext ctx)
    {
        float input = ctx.Input;
        /* split ---------------------------------------------------------------*/
        low += alpha * (input - low);          // 1-pole LP -> "bass"
        float high = input - low;              // residual  -> "treble"

        /* tilt mix --------------------------------------------------------------*/
        return low * (1f - tilt) +
               high * (1f + tilt);
    }

    protected override void OnReturn()
    {
        tilt = alpha = low = 0f;
        cutoffHz = 0f;
        base.OnReturn();
    }
}

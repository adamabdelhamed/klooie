using System;

namespace klooie;

/// <summary>
/// First-order tilt-EQ centred around a gentle low-pass.
/// Positive <c>tilt</c> brightens; negative warms.
/// </summary>
public sealed class TiltEQEffect : Recyclable, IEffect
{
    private float tilt;     // -1 (bass boost) … +1 (treble boost)
    private float alpha;    // LPF coefficient: smaller = lower pivot
    private float low;      // running low-passed state

    private static readonly LazyPool<TiltEQEffect> _pool = new(() => new TiltEQEffect());
    private TiltEQEffect() { }

    public static TiltEQEffect Create(float tilt = 0.0f, float alpha = 0.02f)
    {
        var fx = _pool.Value.Rent();
        fx.tilt = tilt;
        fx.alpha = alpha;
        fx.low = 0f;
        return fx;
    }

    public IEffect Clone() => Create(tilt, alpha);

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
        base.OnReturn();
    }
}

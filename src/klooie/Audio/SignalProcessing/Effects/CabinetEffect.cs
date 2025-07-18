using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
class CabinetEffect : Recyclable, IEffect
{
    // shelves + mid scoop
    Biquad.State low, mid, high;
    float bl0, bl1, bl2, al1, al2;
    float bm0, bm1, bm2, am1, am2;
    float bh0, bh1, bh2, ah1, ah2;
    private Func<float, float> velocityCurve = EffectContext.EaseLinear;
    private float velocityScale = 1f;

    static readonly LazyPool<CabinetEffect> _pool =
        new(() => new CabinetEffect());

    public static CabinetEffect Create(
        Func<float, float>? velocityCurve = null,
        float velocityScale = 1f)
    {
        var fx = _pool.Value.Rent();
        // design filters (values = typical 4×12 cab)
        Biquad.DesignLowShelf(fc: 90f, gainDb: +4f, out fx.bl0, out fx.bl1, out fx.bl2,
                                                              out fx.al1, out fx.al2);
        Biquad.DesignPeak(fc: 800f, q: 1.2f, gainDb: -6f, out fx.bm0, out fx.bm1, out fx.bm2,
                                                              out fx.am1, out fx.am2);
        Biquad.DesignHighShelf(fc: 4500f, gainDb: -6f, out fx.bh0, out fx.bh1, out fx.bh2,
                                                              out fx.ah1, out fx.ah2);
        fx.low = fx.mid = fx.high = default;
        fx.velocityCurve = velocityCurve ?? EffectContext.EaseLinear;
        fx.velocityScale = velocityScale;
        return fx;
    }

    public IEffect Clone() => Create(velocityCurve, velocityScale);

    public float Process(in EffectContext ctx)
    {
        float x = ctx.Input;
        x = Biquad.Process(ref low, bl0, bl1, bl2, al1, al2, x);
        x = Biquad.Process(ref mid, bm0, bm1, bm2, am1, am2, x);
        x = Biquad.Process(ref high, bh0, bh1, bh2, ah1, ah2, x);
        float gain = velocityCurve(ctx.VelocityNorm) * velocityScale;
        return x * gain;
    }

    protected override void OnReturn()
    {
        low = mid = high = default;
        velocityCurve = EffectContext.EaseLinear;
        velocityScale = 1f;
        base.OnReturn();
    }
}

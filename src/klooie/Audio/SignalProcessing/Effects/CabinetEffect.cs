using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
[SynthDocumentation("""
Simulates the tone of a guitar speaker cabinet using shelving filters plus a
midrange scoop.  Helpful for creating amp-like patches without external IRs.
""")]
[SynthCategory("Filter")]
public class CabinetEffect : Recyclable, IEffect
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

    [SynthDocumentation("""
Settings controlling the cabinet simulation.  These include an optional
curve for scaling output based on note velocity.
""")]
    public struct Settings
    {
        [SynthDocumentation("""
Function that adjusts output level based on the
note's velocity.
""")]
        public Func<float, float>? VelocityCurve;

        [SynthDocumentation("""
Additional multiplier applied after evaluating the
velocity curve.
""")]
        public float VelocityScale;
    }

    public static CabinetEffect Create(in Settings settings)
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
        fx.velocityCurve = settings.VelocityCurve ?? EffectContext.EaseLinear;
        fx.velocityScale = settings.VelocityScale;
        return fx;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            VelocityCurve = velocityCurve,
            VelocityScale = velocityScale
        };
        return Create(in settings);
    }

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

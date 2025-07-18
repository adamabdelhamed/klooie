using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
/// <summary>
/// Scales the input by a constant gain and optionally modulates that gain with
/// note velocity.
/// </summary>
public class VolumeEffect : Recyclable, IEffect
{
    private float gain;
    private Func<float, float> velocityCurve = EffectContext.EaseLinear;
    private float velocityScale = 1f;

    private VolumeEffect() { }
    static readonly LazyPool<VolumeEffect> _pool = new(() => new VolumeEffect());

    public static VolumeEffect Create(
        float gain = 1.0f,
        Func<float, float>? velocityCurve = null,
        float velocityScale = 1f)
    {
        var fx = _pool.Value.Rent();
        fx.gain = gain;
        fx.velocityCurve = velocityCurve ?? EffectContext.EaseLinear;
        fx.velocityScale = velocityScale;
        return fx;
    }

    public IEffect Clone()
    {
        return Create(gain, velocityCurve, velocityScale);
    }

    public float Process(in EffectContext ctx)
    {
        float vel = velocityCurve(ctx.VelocityNorm) * velocityScale;
        return ctx.Input * gain * vel;
    }

    protected override void OnReturn()
    {
        gain = 1.0f;
        velocityCurve = EffectContext.EaseLinear;
        velocityScale = 1f;
        base.OnReturn();
    }
}

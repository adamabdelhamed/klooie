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

public struct Settings
{
    public float Gain;
    public Func<float, float>? VelocityCurve;
    public float VelocityScale;
}

public static VolumeEffect Create(in Settings settings)
{
    var fx = _pool.Value.Rent();
    fx.gain = settings.Gain;
    fx.velocityCurve = settings.VelocityCurve ?? EffectContext.EaseLinear;
    fx.velocityScale = settings.VelocityScale;
    return fx;
}

    public IEffect Clone()
    {
        var settings = new Settings
        {
            Gain = gain,
            VelocityCurve = velocityCurve,
            VelocityScale = velocityScale
        };
        return Create(in settings);
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

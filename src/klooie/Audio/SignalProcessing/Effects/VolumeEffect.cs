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
[SynthDocumentation("""
Applies a constant gain to the signal.  Optionally the gain can respond to note
velocity for expressive dynamics.
""")]
[SynthCategory("Utility")]
public class VolumeEffect : Recyclable, IEffect
{
    private float gain;
    private Func<float, float> velocityCurve = EffectContext.EaseLinear;
    private float velocityScale = 1f;

    private VolumeEffect() { }
    static readonly LazyPool<VolumeEffect> _pool = new(() => new VolumeEffect());

[SynthDocumentation("""
Settings defining the fixed gain and optional velocity response.
""")]
public struct Settings
{
    [SynthDocumentation("""
Gain multiplier applied to the input signal.
""")]
    public float Gain;

    [SynthDocumentation("""
Function that converts note velocity into a gain
multiplier.
""")]
    public Func<float, float>? VelocityCurve;

    [SynthDocumentation("""
Additional multiplier applied after the velocity
curve.
""")]
    public float VelocityScale;
}

public static VolumeEffect Create(in Settings settings)
{
    var fx = _pool.Value.Rent();
    fx.gain = settings.Gain;
    fx.velocityCurve = settings.VelocityCurve ?? DefaultVelocityCurve;
    fx.velocityScale = settings.VelocityScale;
    return fx;
}

    private static float DefaultVelocityCurve(float normalized)
    {
        float minVolume = 0.4f;
        float maxVolume = 1.0f;
        float curve = normalized * normalized; // x^2
        float volume = minVolume + (maxVolume - minVolume) * curve;
        return volume;
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

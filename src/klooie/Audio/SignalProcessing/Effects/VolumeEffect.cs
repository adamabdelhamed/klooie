using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public class VolumeEffect : Recyclable, IEffect
{
    float gain;

    private VolumeEffect() { }
    static readonly LazyPool<VolumeEffect> _pool = new(() => new VolumeEffect());

    public static VolumeEffect Create(float gain = 1.0f)
    {
        var fx = _pool.Value.Rent();
        fx.gain = gain;
        return fx;
    }

    public IEffect Clone()
    {
        return Create(gain);
    }

    public float Process(in EffectContext ctx)
    {
        return ctx.Input * gain;
    }

    protected override void OnReturn()
    {
        gain = 1.0f;
        base.OnReturn();
    }
}

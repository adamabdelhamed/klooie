using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
class LowPassFilterEffect : Recyclable, IEffect
{
    private float alpha;
    private float state;

    private static readonly LazyPool<LowPassFilterEffect> _pool = new(() => new LowPassFilterEffect());

    private LowPassFilterEffect() { }

    public static LowPassFilterEffect Create(float alpha)
    {
        var fx = _pool.Value.Rent();
        fx.alpha = Math.Clamp(alpha, 0, 1);
        fx.state = 0f;
        return fx;
    }

    public IEffect Clone() => LowPassFilterEffect.Create(alpha);

    public float Process(float input, int frameIdx, float time)
    {
        state += alpha * (input - state);
        return state;
    }

    protected override void OnReturn()
    {
        state = 0f;
        alpha = 0f;
        base.OnReturn();
    }
}

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
    private float cutoffHz;

    private static readonly LazyPool<LowPassFilterEffect> _pool = new(() => new LowPassFilterEffect());

    private LowPassFilterEffect() { }

    public static LowPassFilterEffect Create(float cutoffHz = 200f)
    {
        var fx = _pool.Value.Rent();
        fx.Construct(cutoffHz);
        return fx;
    }

    protected void Construct(float cutoffHz)
    {
        this.cutoffHz = cutoffHz;
        float dt = 1f / SoundProvider.SampleRate;
        float rc = 1f / (2f * MathF.PI * cutoffHz);
        alpha = dt / (rc + dt);
        state = 0f;
    }

    public IEffect Clone() => LowPassFilterEffect.Create(cutoffHz);

    public float Process(in EffectContext ctx)
    {
        float input = ctx.Input;
        state += alpha * (input - state);
        return state;
    }

    protected override void OnReturn()
    {
        state = 0f;
        alpha = 0f;
        cutoffHz = 0f;
        base.OnReturn();
    }
}

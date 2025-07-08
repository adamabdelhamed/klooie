using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
class NoiseGateEffect : Recyclable, IEffect
{
    readonly float[] lookBuf = new float[256];   // ≈ 5.8 ms @44.1 kHz
    int lookPos;

    float openThresh, closeThresh;
    float attackMs, releaseMs;
    float env, rise, fall;
    bool open;

    static readonly LazyPool<NoiseGateEffect> _pool =
        new(() => new NoiseGateEffect());

    public static NoiseGateEffect Create(
        float openThresh = 0.05f, float closeThresh = 0.04f,
        float attackMs = 2f, float releaseMs = 60f)
    {
        var fx = _pool.Value.Rent();
        fx.attackMs = attackMs;
        fx.releaseMs = releaseMs;
        fx.openThresh = openThresh;
        fx.closeThresh = closeThresh;
        fx.rise = 1f - MathF.Exp(-1f / (attackMs * 0.001f * SoundProvider.SampleRate));
        fx.fall = 1f - MathF.Exp(-1f / (releaseMs * 0.001f * SoundProvider.SampleRate));
        fx.env = 0f;
        fx.open = false;
        fx.lookPos = 0;
        Array.Clear(fx.lookBuf, 0, fx.lookBuf.Length);
        return fx;
    }

    public IEffect Clone() => NoiseGateEffect.Create(openThresh, closeThresh, attackMs, releaseMs);

    public float Process(float input, int frame, float time)
    {
        // write current sample into look-ahead ring
        lookBuf[lookPos] = input;
        int readPos = (lookPos + 1) & (lookBuf.Length - 1);
        lookPos = readPos;

        float ahead = lookBuf[readPos];
        float abs = MathF.Abs(ahead);

        env = Smoother.Follow(ref env, rise, fall, abs);
        if (!open && env > openThresh) open = true;
        else if (open && env < closeThresh) open = false;

        return open ? ahead : 0f;
    }

    protected override void OnReturn()
    {
        Array.Clear(lookBuf, 0, lookBuf.Length);
        base.OnReturn();
    }
}
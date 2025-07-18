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
    float gain;
    private bool velocityAffectsThreshold;
    private Func<float, float> velocityCurve = EffectContext.EaseLinear;

    static readonly LazyPool<NoiseGateEffect> _pool =
        new(() => new NoiseGateEffect());

    public static NoiseGateEffect Create(
        float openThresh = 0.05f, float closeThresh = 0.04f,
        float attackMs = 2f, float releaseMs = 60f,
        bool velocityAffectsThreshold = true,
        Func<float, float>? velocityCurve = null)
    {
        var fx = _pool.Value.Rent();
        fx.attackMs = attackMs;
        fx.releaseMs = releaseMs;
        fx.openThresh = openThresh;
        fx.closeThresh = closeThresh;
        fx.rise = 1f - MathF.Exp(-1f / (attackMs * 0.001f * SoundProvider.SampleRate));
        fx.fall = 1f - MathF.Exp(-1f / (releaseMs * 0.001f * SoundProvider.SampleRate));
        fx.env = openThresh; // start above closeThresh to avoid initial mute
        fx.open = true;      // begin open so note attacks aren't choked
        fx.gain = 1f;
        fx.velocityAffectsThreshold = velocityAffectsThreshold;
        fx.velocityCurve = velocityCurve ?? EffectContext.EaseLinear;
        fx.lookPos = 0;
        Array.Clear(fx.lookBuf, 0, fx.lookBuf.Length);
        return fx;
    }

    public IEffect Clone() => NoiseGateEffect.Create(openThresh, closeThresh, attackMs, releaseMs, velocityAffectsThreshold, velocityCurve);

    public float Process(in EffectContext ctx)
    {
        float input = ctx.Input;
        // write current sample into look-ahead ring
        lookBuf[lookPos] = input;
        int readPos = (lookPos + 1) & (lookBuf.Length - 1);
        lookPos = readPos;

        float ahead = lookBuf[readPos];
        float abs = MathF.Abs(ahead);

        float openT = openThresh;
        float closeT = closeThresh;
        if (velocityAffectsThreshold)
        {
            float v = velocityCurve(ctx.VelocityNorm);
            openT *= v; closeT *= v;
        }
        env = Smoother.Follow(ref env, rise, fall, abs);
        if (!open && env > openT) open = true;
        else if (open && env < closeT) open = false;

        float target = open ? 1f : 0f;
        gain = Smoother.Follow(ref gain, rise, fall, target);

        return ahead * gain;
    }

    protected override void OnReturn()
    {
        Array.Clear(lookBuf, 0, lookBuf.Length);
        lookPos = 0;
        env = gain = 0f;
        open = false;
        velocityAffectsThreshold = false;
        velocityCurve = EffectContext.EaseLinear;
        base.OnReturn();
    }}
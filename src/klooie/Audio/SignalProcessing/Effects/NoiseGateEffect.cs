using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
[SynthDescription("""
Silences the signal whenever its level drops below a defined threshold,
helping remove background noise between notes.
""")]
[SynthCategory("Dynamics")]
public class NoiseGateEffect : Recyclable, IEffect
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

    [SynthDescription("""
Settings that determine how the gate opens and closes.
""")]
    public struct Settings
    {
        [SynthDescription("""
Signal level above which the gate opens.
""")]
        public float OpenThresh;

        [SynthDescription("""
Level below which the gate starts to close.
""")]
        public float CloseThresh;

        [SynthDescription("""
Time in milliseconds for the gate to fully open
""")]
        public float AttackMs;

        [SynthDescription("""
Time in milliseconds for the gate to fully close
""")]
        public float ReleaseMs;

        [SynthDescription("""
If true, note velocity scales the open and close
thresholds.
""")]
        public bool VelocityAffectsThreshold;

        [SynthDescription("""
Function that maps velocity to a multiplier used
when scaling the thresholds.
""")]
        public Func<float, float>? VelocityCurve;
    }

    public static NoiseGateEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.attackMs = settings.AttackMs;
        fx.releaseMs = settings.ReleaseMs;
        fx.openThresh = settings.OpenThresh;
        fx.closeThresh = settings.CloseThresh;
        fx.rise = 1f - MathF.Exp(-1f / (settings.AttackMs * 0.001f * SoundProvider.SampleRate));
        fx.fall = 1f - MathF.Exp(-1f / (settings.ReleaseMs * 0.001f * SoundProvider.SampleRate));
        fx.env = settings.OpenThresh; // start above closeThresh to avoid initial mute
        fx.open = true;      // begin open so note attacks aren't choked
        fx.gain = 1f;
        fx.velocityAffectsThreshold = settings.VelocityAffectsThreshold;
        fx.velocityCurve = settings.VelocityCurve ?? EffectContext.EaseLinear;
        fx.lookPos = 0;
        Array.Clear(fx.lookBuf, 0, fx.lookBuf.Length);
        return fx;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            OpenThresh = openThresh,
            CloseThresh = closeThresh,
            AttackMs = attackMs,
            ReleaseMs = releaseMs,
            VelocityAffectsThreshold = velocityAffectsThreshold,
            VelocityCurve = velocityCurve
        };
        return Create(in settings);
    }

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
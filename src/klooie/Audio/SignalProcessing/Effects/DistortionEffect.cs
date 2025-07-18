using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
[SynthDescription("""
Multi-stage soft-clipping distortion effect with simple oversampling to reduce aliasing.
""")]
[SynthCategory("Distortion")]
class DistortionEffect : Recyclable, IEffect
{
    // 2× oversample using linear interp, 3 gain stages, tanh softclip
    float gain1, gain2, gain3, bias;
    private Func<float, float> velocityCurve = EffectContext.EaseLinear;
    private float velocityScale = 1f;
    float lpOut;   // simple 1-pole LP to anti-alias
    float prevIn;

    private float stageRatio;

    private DistortionEffect() { }
    static readonly LazyPool<DistortionEffect> _pool = new(() => new DistortionEffect());

    [SynthDescription("""
    Parameters for DistortionEffect.
    """)]
    public struct Settings
    {
        [SynthDescription("""Input gain before distortion stages.""")]
        public float Drive;

        [SynthDescription("""Relative gain drop for each successive stage.""")]
        public float StageRatio;

        [SynthDescription("""Asymmetry bias added to the clipper.""")]
        public float Bias;

        [SynthDescription("""Optional curve mapping velocity to gain scale.""")]
        public Func<float, float>? VelocityCurve;

        [SynthDescription("""Multiplier applied to the velocity curve.""")]
        public float VelocityScale;
    }

    public static DistortionEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.gain1 = settings.Drive;
        fx.gain2 = settings.Drive * settings.StageRatio;
        fx.gain3 = settings.Drive * settings.StageRatio * settings.StageRatio;
        fx.bias = settings.Bias;
        fx.prevIn = 0f;
        fx.lpOut = 0f;
        fx.stageRatio = settings.StageRatio;
        fx.velocityCurve = settings.VelocityCurve ?? EffectContext.EaseLinear;
        fx.velocityScale = settings.VelocityScale;
        return fx;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            Drive = gain1,
            StageRatio = stageRatio,
            Bias = bias,
            VelocityCurve = velocityCurve,
            VelocityScale = velocityScale
        };
        return Create(in settings);
    }

    // one-pole LP at ~9 kHz (post-distortion, base SR)
    static readonly float lpAlpha = 1f - MathF.Exp(-2f * MathF.PI * 9000f / SoundProvider.SampleRate);

    static float SoftClip(float x) => MathF.Tanh(x);

    public float Process(in EffectContext ctx)
    {
        // ---- 2× oversampling (linear) -------------------------------------
        float input = ctx.Input;
        float velFactor = 1f + velocityScale * (velocityCurve(ctx.VelocityNorm) - 1f);
        float mid = 0.5f * (input + prevIn);
        float a = Distort(prevIn, velFactor);
        float b = Distort(mid, velFactor);
        float c = Distort(input, velFactor);
        prevIn = input;

        // anti-alias LP & decimate: weighted average ≈ low-pass
        float down = 0.25f * (a + c) + 0.5f * b;
        lpOut += lpAlpha * (down - lpOut);
        return lpOut;
    }

    float Distort(float x, float velFactor)
    {
        float y1 = SoftClip(x * (gain1 * velFactor) + bias);
        float y2 = SoftClip(y1 * (gain2 * velFactor) - bias);
        float y3 = SoftClip(y2 * (gain3 * velFactor));
        return y3 * 0.6f;  // tame output level
    }

    protected override void OnReturn()
    {
        prevIn = lpOut = 0f;
        gain1 = gain2 = gain3 = bias = 0f;
        velocityCurve = EffectContext.EaseLinear;
        velocityScale = 1f;
        base.OnReturn();
    }
}

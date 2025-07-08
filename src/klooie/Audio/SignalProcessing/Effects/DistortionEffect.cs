using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
class DistortionEffect : Recyclable, IEffect
{
    // 2× oversample using linear interp, 3 gain stages, tanh softclip
    float gain1, gain2, gain3, bias;
    float lpOut;   // simple 1-pole LP to anti-alias
    float prevIn;

    static readonly LazyPool<DistortionEffect> _pool =
        new(() => new DistortionEffect());

    public static DistortionEffect Create(
        float drive = 6f, // overall gain
        float stageRatio = 0.6f, // how each stage’s gain scales
        float bias = 0.15f)      // DC bias to add asymmetry
    {
        var fx = _pool.Value.Rent();
        fx.gain1 = drive;
        fx.gain2 = drive * stageRatio;
        fx.gain3 = drive * stageRatio * stageRatio;
        fx.bias = bias;
        fx.prevIn = 0f;
        fx.lpOut = 0f;
        return fx;
    }

    // one-pole LP at ~9 kHz (post-distortion, base SR)
    static readonly float lpAlpha = 1f - MathF.Exp(-2f * MathF.PI * 9000f / SoundProvider.SampleRate);

    static float SoftClip(float x) => MathF.Tanh(x);

    public float Process(float input, int frameIdx)
    {
        // ---- 2× oversampling (linear) -------------------------------------
        float mid = 0.5f * (input + prevIn);
        float a = Distort(prevIn);
        float b = Distort(mid);
        float c = Distort(input);
        prevIn = input;

        // anti-alias LP & decimate: weighted average ≈ low-pass
        float down = 0.25f * (a + c) + 0.5f * b;
        lpOut += lpAlpha * (down - lpOut);
        return lpOut;
    }

    float Distort(float x)
    {
        float y1 = SoftClip(x * gain1 + bias);
        float y2 = SoftClip(y1 * gain2 - bias);
        float y3 = SoftClip(y2 * gain3);
        return y3 * 0.6f;  // tame output level
    }

    protected override void OnReturn()
    {
        prevIn = lpOut = 0f;
        base.OnReturn();
    }
}

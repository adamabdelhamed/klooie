using System;
using System.Collections.Generic;

namespace klooie;

// =========================
// AllPassFilter (unchanged, with buffer clearing on return)
// =========================
class AllPassFilter : Recyclable
{
    private float[] buffer;
    private int pos;
    private float feedback;

    protected static LazyPool<AllPassFilter> _pool = new(() => new AllPassFilter());
    protected AllPassFilter() { }

    public static AllPassFilter Create(int delaySamples, float feedback)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(delaySamples, feedback);
        return ret;
    }

    protected void Construct(int delaySamples, float feedback)
    {
        buffer = new float[delaySamples];
        this.feedback = feedback;
        pos = 0;
    }

    public float Process(float input)
    {
        float bufOut = buffer[pos];
        float output = -input + bufOut;
        if (Math.Abs(output) < 1e-12f) output = 0f;
        buffer[pos] = input + bufOut * feedback;
        if (Math.Abs(buffer[pos]) < 1e-12f) buffer[pos] = 0f;
        pos = (pos + 1) % buffer.Length;
        return output;
    }

    protected override void OnReturn()
    {
        if (buffer != null)
            Array.Clear(buffer, 0, buffer.Length);
        buffer = null;
        pos = 0;
        base.OnReturn();
    }
}

// =========================
// CombFilter with damping and modulation
// =========================
class CombFilter : Recyclable
{
    private float[] buffer;
    private int baseDelay, pos;
    private float feedback;
    private float lastFiltered; // for damping
    private float damping;
    private float lfoPhase, lfoInc, lfoDepth; // for mod
    private bool enableMod;
    private static LazyPool<CombFilter> _pool = new(() => new CombFilter());

    protected CombFilter() { }

    public static CombFilter Create(int delaySamples, float feedback, float damping = 0.5f,
        bool enableMod = false, float lfoFreq = 0.15f, float lfoDepthSamples = 2f, float sampleRate = 44100f)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(delaySamples, feedback, damping, enableMod, lfoFreq, lfoDepthSamples, sampleRate);
        return ret;
    }

    protected void Construct(int delaySamples, float feedback, float damping, bool enableMod,
        float lfoFreq, float lfoDepthSamples, float sampleRate)
    {
        baseDelay = delaySamples;
        buffer = new float[delaySamples + (int)Math.Ceiling(lfoDepthSamples) + 2]; // pad for mod
        this.feedback = feedback;
        this.damping = damping;
        pos = 0;
        lastFiltered = 0f;
        this.enableMod = enableMod;
        lfoPhase = 0f;
        lfoInc = (float)(2 * Math.PI * lfoFreq / sampleRate);
        this.lfoDepth = lfoDepthSamples;
    }

    public float Process(float input)
    {
        // Modulate delay (if enabled)
        int modDelay = baseDelay;
        if (enableMod)
        {
            float lfo = (float)Math.Sin(lfoPhase);
            lfoPhase += lfoInc;
            if (lfoPhase > 2 * Math.PI) lfoPhase -= 2 * (float)Math.PI;
            modDelay += (int)(lfo * lfoDepth);
        }
        int readPos = pos - modDelay;
        if (readPos < 0) readPos += buffer.Length;
        float output = buffer[readPos];

        // Damping (lowpass)
        lastFiltered = (1f - damping) * output + damping * lastFiltered;

        buffer[pos] = input + lastFiltered * feedback;

        pos = (pos + 1) % buffer.Length;
        return output;
    }

    protected override void OnReturn()
    {
        if (buffer != null)
            Array.Clear(buffer, 0, buffer.Length);
        buffer = null;
        pos = 0;
        lastFiltered = 0f;
        lfoPhase = 0f;
        base.OnReturn();
    }
}

// =========================
// Simple one-pole input highcut filter
// =========================
class OnePoleLowpass : Recyclable
{
    private float a, y;
    public static OnePoleLowpass Create(float cutoffHz, float sampleRate)
    {
        var f = _pool.Value.Rent();
        float x = (float)Math.Exp(-2.0 * Math.PI * cutoffHz / sampleRate);
        f.a = 1f - x;
        f.y = 0f;
        return f;
    }

    private static LazyPool<OnePoleLowpass> _pool = new(() => new OnePoleLowpass());
    protected OnePoleLowpass() { }
    public float Process(float x)
    {
        y += a * (x - y);
        return y;
    }
    protected override void OnReturn() { y = 0; base.OnReturn(); }
}

// =========================
// Stereo ReverbEffect
// =========================
[SynthDocumentation("""
Stereo reverb constructed from multiple comb and all-pass filters.  
Decay, diffusion, damping, input filtering, and delay modulation are all adjustable for tone/cpu tradeoff.
""")]
[SynthCategory("Reverb")]
public class ReverbEffect : Recyclable, IEffect
{
    // === Quality Knobs (CPU vs quality) ===
    // To lower CPU: reduce NUM_COMBS, NUM_ALLPASSES, disable MOD_COMBS, or lower SAMPLE_RATE.
    private const int NUM_COMBS = 6;        // 4–8 recommended; reduce for less CPU
    private const int NUM_ALLPASSES = 4;    // 2–8; reduce for less density/CPU
    private const bool MOD_COMBS = true;    // true = better, false = lower CPU
    private const float SAMPLE_RATE = 44100f;

    // Classic prime delays for combs (pick from well-known lists for variety)
    private static readonly int[] combDelays = { 1557, 1617, 1491, 1422, 1277, 1356, 1188, 1111 };
    private static readonly int[] allpassDelays = { 225, 556, 441, 341, 191, 143, 89, 61 };

    private CombFilter[] combs;
    private AllPassFilter[] allpasses;
    private float feedback, diffusion, wet, dry, damping;
    private OnePoleLowpass inputFilter;
    private float inputCutoffHz;
    private bool velocityAffectsMix;
    private Func<float, float> mixVelocityCurve = EffectContext.EaseLinear;

    [SynthDocumentation("""
Settings controlling reverb decay, diffusion, high-frequency damping, pre-filtering, delay modulation, and how the wet/dry mix reacts to note velocity.
""")]
    public struct Settings
    {
        public float Feedback;      // 0.7–0.85 is typical for long tails
        public float Diffusion;     // 0.5–0.8 for smoothness
        public float Damping;       // 0.2–0.7 for realistic HF rolloff
        public float Wet;           // Wet level
        public float Dry;           // Dry level
        public float InputLowpassHz;// High-cut for pre-filter (7000–12000 typical)
        public bool VelocityAffectsMix;
        public Func<float, float>? MixVelocityCurve;
        public bool EnableModulation; // Enable comb LFO modulation
    }

    public static ReverbEffect Create(in Settings settings)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(settings);
        return ret;
    }

    private static LazyPool<ReverbEffect> _pool = new(() => new ReverbEffect());
    protected ReverbEffect() { }

    protected void Construct(Settings s)
    {
        feedback = s.Feedback;
        diffusion = s.Diffusion;
        wet = s.Wet;
        dry = s.Dry;
        damping = s.Damping;
        inputCutoffHz = s.InputLowpassHz > 0 ? s.InputLowpassHz : 12000f;
        velocityAffectsMix = s.VelocityAffectsMix;
        mixVelocityCurve = s.MixVelocityCurve ?? EffectContext.EaseLinear;

        // --- Setup pre-reverb input filter (high-cut) ---
        inputFilter = OnePoleLowpass.Create(inputCutoffHz, SAMPLE_RATE);

        // --- Setup combs ---
        int usedCombs = Math.Min(NUM_COMBS, combDelays.Length);
        combs = new CombFilter[usedCombs];
        for (int i = 0; i < usedCombs; i++)
        {
            bool enableMod = s.EnableModulation && MOD_COMBS;
            // LFO freq can be randomized per-comb for lushness
            float lfoFreq = enableMod ? 0.11f + 0.05f * (i % 3) : 0f;
            combs[i] = CombFilter.Create(combDelays[i], feedback, damping, enableMod, lfoFreq, 2.3f, SAMPLE_RATE);
        }

        // --- Setup allpasses ---
        int usedAllpasses = Math.Min(NUM_ALLPASSES, allpassDelays.Length);
        allpasses = new AllPassFilter[usedAllpasses];
        for (int i = 0; i < usedAllpasses; i++)
            allpasses[i] = AllPassFilter.Create(allpassDelays[i], diffusion);
    }

    public IEffect Clone()
    {
        return Create(new Settings
        {
            Feedback = feedback,
            Diffusion = diffusion,
            Damping = damping,
            Wet = wet,
            Dry = dry,
            InputLowpassHz = inputCutoffHz,
            VelocityAffectsMix = velocityAffectsMix,
            MixVelocityCurve = mixVelocityCurve,
            EnableModulation = MOD_COMBS
        });
    }

    public float Process(in EffectContext ctx)
    {
        float input = inputFilter != null ? inputFilter.Process(ctx.Input) : ctx.Input;
        // --- Mix combs in parallel ---
        float combOut = 0f;
        for (int i = 0; i < combs.Length; i++)
            combOut += combs[i].Process(input);
        combOut /= combs.Length;

        // --- Pass through allpasses in series ---
        float apOut = combOut;
        for (int i = 0; i < allpasses.Length; i++)
            apOut = allpasses[i].Process(apOut);

        float mixAmt = wet;
        if (velocityAffectsMix)
            mixAmt *= mixVelocityCurve(ctx.VelocityNorm);
        return dry * ctx.Input + mixAmt * apOut;
    }

    protected override void OnReturn()
    {
        if (combs != null)
        {
            foreach (var c in combs) c.Dispose();
            combs = null;
        }
        if (allpasses != null)
        {
            foreach (var a in allpasses) a.Dispose();
            allpasses = null;
        }
        inputFilter?.Dispose();
        inputFilter = null;
        mixVelocityCurve = EffectContext.EaseLinear;
        base.OnReturn();
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;

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
        buffer = null;
        pos = 0;
        base.OnReturn();
    }
}


class CombFilter : Recyclable
{
    private float[] buffer;
    private int pos;
    private float feedback;

    private static LazyPool<CombFilter> _pool = new(() => new CombFilter());

    protected CombFilter() { }

    public static CombFilter Create(int delaySamples, float feedback)
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
        float output = buffer[pos];
        if (Math.Abs(output) < 1e-12f) output = 0f;
        buffer[pos] = input + output * feedback;
        if (Math.Abs(buffer[pos]) < 1e-12f) buffer[pos] = 0f;
        pos = (pos + 1) % buffer.Length;
        return output;
    }

    protected override void OnReturn()
    {
        buffer = null;
        pos = 0;
        base.OnReturn();
    }
}

[SynthDocumentation("""
Stereo reverb constructed from multiple comb and all-pass filters.  Feedback and
diffusion settings shape the size and character of the virtual space.
""")]
[SynthCategory("Reverb")]
public class ReverbEffect : Recyclable, IEffect
{
    private CombFilter[] combs;
    private AllPassFilter[] allpasses;
    private float feedback, diffusion, wet, dry;
    private bool velocityAffectsMix;
    private Func<float, float> mixVelocityCurve = EffectContext.EaseLinear;

    // Some classic reverb delay times (in samples, for 44.1kHz sample rate)
    private static readonly int[] combDelays = { 1557, 1617, 1491, 1422 };
    private static readonly int[] allpassDelays = { 225, 556 };

    private static LazyPool<ReverbEffect> _pool = new(() => new ReverbEffect());
    protected ReverbEffect() { }
    [SynthDocumentation("""
Settings controlling reverb decay, diffusion and how the wet/dry mix reacts
to note velocity.
""")]
    public struct Settings
    {
        [SynthDocumentation("""
Amount of feedback which determines how long the
reverb tail lasts.
""")]
        public float Feedback;

        [SynthDocumentation("""
How dense the reflections become. Higher values
create a smoother tail.
""")]
        public float Diffusion;

        [SynthDocumentation("""
Volume of the reverberated (wet) signal.
""")]
        public float Wet;

        [SynthDocumentation("""
Volume of the unaffected (dry) signal.
""")]
        public float Dry;

        [SynthDocumentation("""
If true, note velocity influences the wet mix
level.
""")]
        public bool VelocityAffectsMix;

        [SynthDocumentation("""
Function mapping velocity to a multiplier applied
to the wet level.
""")]
        public Func<float, float>? MixVelocityCurve;
    }

    public static ReverbEffect Create(in Settings settings)
    {
        var ret = _pool.Value.Rent();
        ret.Construct(settings.Feedback, settings.Diffusion, settings.Wet, settings.Dry);
        ret.velocityAffectsMix = settings.VelocityAffectsMix;
        ret.mixVelocityCurve = settings.MixVelocityCurve ?? EffectContext.EaseLinear;
        return ret;
    }

    protected void Construct(float feedback = 0.78f, float diffusion = 0.5f, float wet = 0.3f, float dry = 0.7f)
    {
        this.feedback = feedback;
        this.diffusion = diffusion;
        // Allocate combs
        combs = new CombFilter[combDelays.Length];
        for (int i = 0; i < combs.Length; i++)
            combs[i] = CombFilter.Create(combDelays[i], feedback);

        // Allocate allpasses
        allpasses = new AllPassFilter[allpassDelays.Length];
        for (int i = 0; i < allpasses.Length; i++)
            allpasses[i] = AllPassFilter.Create(allpassDelays[i], diffusion);

        this.wet = wet;
        this.dry = dry;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            Feedback = feedback,
            Diffusion = diffusion,
            Wet = wet,
            Dry = dry,
            VelocityAffectsMix = velocityAffectsMix,
            MixVelocityCurve = mixVelocityCurve
        };
        return Create(in settings);
    }

    public float Process(in EffectContext ctx)
    {
        float input = ctx.Input;
        // Mix combs in parallel
        float combOut = 0f;
        for (int i = 0; i < combs.Length; i++)
            combOut += combs[i].Process(input);
        combOut /= combs.Length;

        // Pass through allpasses in series
        float apOut = combOut;
        for (int i = 0; i < allpasses.Length; i++)
            apOut = allpasses[i].Process(apOut);

        float mixAmt = wet;
        if (velocityAffectsMix)
            mixAmt *= mixVelocityCurve(ctx.VelocityNorm);
        return dry * input + mixAmt * apOut;
    }


    protected override void OnReturn()
    {
        for (int i = 0; i < combs.Length; i++)
        {
            CombFilter? c = combs[i];
            c.Dispose();
        }
        combs = null;
        for (int i1 = 0; i1 < allpasses.Length; i1++)
        {
            AllPassFilter? a = allpasses[i1];
            a.Dispose();
        }
        allpasses = null;
        velocityAffectsMix = false;
        mixVelocityCurve = EffectContext.EaseLinear;
        base.OnReturn();
    }
}

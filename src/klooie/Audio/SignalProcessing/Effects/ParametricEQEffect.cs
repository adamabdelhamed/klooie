using klooie;

public enum BiquadType { Peak, LowShelf, HighShelf }

[SynthDescription("""
Parametric equalizer supporting peak and shelf modes.
""")]
[SynthCategory("Filter")]
public class ParametricEQEffect : Recyclable, IEffect
{
    // Filter params
    private BiquadType type;
    private float freq;
    private float gainDb;
    private float q;
    private bool velocityAffectsGain = true;
    private Func<float, float> gainVelocityCurve = EffectContext.EaseLinear;
    private float gainVelocityScale = 1f;

    // Coeffs/state
    private float b0, b1, b2, a1, a2;
    private Biquad.State state;

    private static readonly LazyPool<ParametricEQEffect> _pool = new(() => new ParametricEQEffect());

    private ParametricEQEffect() { }

    [SynthDescription("""
    Parameters for ParametricEQEffect.
    """)]
    public struct Settings
    {
        [SynthDescription("""Type of biquad filter.""")]
        public BiquadType Type;

        [SynthDescription("""Center frequency in Hz.""")]
        public float Freq;

        [SynthDescription("""Gain in decibels.""")]
        public float GainDb;

        [SynthDescription("""Quality factor.""")]
        public float Q;

        [SynthDescription("""Whether velocity affects gain.""")]
        public bool VelocityAffectsGain;

        [SynthDescription("""Curve for velocity-based gain scaling.""")]
        public Func<float, float>? GainVelocityCurve;

        [SynthDescription("""Scale factor for velocity curve.""")]
        public float GainVelocityScale;
    }

    public static ParametricEQEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.type = settings.Type;
        fx.freq = settings.Freq;
        fx.gainDb = settings.GainDb;
        fx.q = settings.Q;
        fx.velocityAffectsGain = settings.VelocityAffectsGain;
        fx.gainVelocityCurve = settings.GainVelocityCurve ?? EffectContext.EaseLinear;
        fx.gainVelocityScale = settings.GainVelocityScale;
        fx.state = new Biquad.State();

        // Design coeffs
        switch (settings.Type)
        {
            case BiquadType.Peak:
                Biquad.DesignPeak(settings.Freq, settings.Q, settings.GainDb, out fx.b0, out fx.b1, out fx.b2, out fx.a1, out fx.a2);
                break;
            case BiquadType.LowShelf:
                Biquad.DesignLowShelf(settings.Freq, settings.GainDb, out fx.b0, out fx.b1, out fx.b2, out fx.a1, out fx.a2);
                break;
            case BiquadType.HighShelf:
                Biquad.DesignHighShelf(settings.Freq, settings.GainDb, out fx.b0, out fx.b1, out fx.b2, out fx.a1, out fx.a2);
                break;
        }
        return fx;
    }

    public float Process(in EffectContext ctx)
    {
        float gDb = gainDb;
        if (velocityAffectsGain)
            gDb *= 1f + gainVelocityScale * (gainVelocityCurve(ctx.VelocityNorm) - 1f);
        float b0m=b0,b1m=b1,b2m=b2,a1m=a1,a2m=a2;
        if (gDb != gainDb)
        {
            switch (type)
            {
                case BiquadType.Peak:
                    Biquad.DesignPeak(freq, q, gDb, out b0m, out b1m, out b2m, out a1m, out a2m);
                    break;
                case BiquadType.LowShelf:
                    Biquad.DesignLowShelf(freq, gDb, out b0m, out b1m, out b2m, out a1m, out a2m);
                    break;
                case BiquadType.HighShelf:
                    Biquad.DesignHighShelf(freq, gDb, out b0m, out b1m, out b2m, out a1m, out a2m);
                    break;
            }
        }
        return Biquad.Process(ref state, b0m, b1m, b2m, a1m, a2m, ctx.Input);
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            Type = type,
            Freq = freq,
            GainDb = gainDb,
            Q = q,
            VelocityAffectsGain = velocityAffectsGain,
            GainVelocityCurve = gainVelocityCurve,
            GainVelocityScale = gainVelocityScale
        };
        return Create(in settings);
    }

    protected override void OnReturn()
    {
        state = default;
        velocityAffectsGain = true;
        gainVelocityCurve = EffectContext.EaseLinear;
        gainVelocityScale = 1f;
        base.OnReturn();
    }
}

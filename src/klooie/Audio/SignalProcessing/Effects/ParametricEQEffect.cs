using klooie;

public enum BiquadType { Peak, LowShelf, HighShelf }

[SynthDocumentation("""
Flexible parametric EQ supporting peak, low‑shelf and high‑shelf modes.
The filter frequency can be a fixed value or a multiple of
the note being played. Use note-relative mode to keep the EQ
centered around each note.
""")]
[SynthCategory("Filter")]
public class ParametricEQEffect : Recyclable, IEffect
{
    // Filter params
    private BiquadType type;
    private float? fixedFreq;
    private float? noteFrequencyMultiplier;
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

    [SynthDocumentation("""
    Settings that configure the filter type, how its center
    frequency is determined and how gain responds to velocity.
    Provide either a fixed frequency or a multiplier relative
    to the note being played.
    """)]
    public struct Settings
    {
        [SynthDocumentation("""
Which biquad filter to use (peak, low shelf or high
shelf).
""")]
        public BiquadType Type;

        [SynthDocumentation("""
        Fixed center frequency in hertz. Leave null to use
        NoteFrequencyMultiplier.
        """)]
        public float? Freq;

        [SynthDocumentation("""
        If set, center frequency = note frequency × this
        multiplier.
        """)]
        public float? NoteFrequencyMultiplier;

        [SynthDocumentation("""
Boost or cut amount in decibels.
""")]
        public float GainDb;

        [SynthDocumentation("""
Resonance or bandwidth of the filter.
""")]
        public float Q;

        [SynthDocumentation("""
If true, note velocity modulates the filter's
gain.
""")]
        public bool VelocityAffectsGain;

        [SynthDocumentation("""
Function mapping velocity to a gain multiplier.
""")]
        public Func<float, float>? GainVelocityCurve;

        [SynthDocumentation("""
Multiplier applied after evaluating the velocity
curve.
""")]
        public float GainVelocityScale;
    }

    public static ParametricEQEffect Create(in Settings settings)
    {
        if (settings.Freq.HasValue == settings.NoteFrequencyMultiplier.HasValue)
            throw new ArgumentException("Specify exactly one of Freq or NoteFrequencyMultiplier.");

        var fx = _pool.Value.Rent();
        fx.Construct(in settings);
        return fx;
    }

    private void Construct(in Settings settings)
    {
        type = settings.Type;
        fixedFreq = settings.Freq;
        noteFrequencyMultiplier = settings.NoteFrequencyMultiplier;
        gainDb = settings.GainDb;
        q = settings.Q;
        velocityAffectsGain = settings.VelocityAffectsGain;
        gainVelocityCurve = settings.GainVelocityCurve ?? EffectContext.EaseLinear;
        gainVelocityScale = settings.GainVelocityScale;
        state = new Biquad.State();

        if (fixedFreq.HasValue)
        {
            switch (type)
            {
                case BiquadType.Peak:
                    Biquad.DesignPeak(fixedFreq.Value, q, gainDb, out b0, out b1, out b2, out a1, out a2);
                    break;
                case BiquadType.LowShelf:
                    Biquad.DesignLowShelf(fixedFreq.Value, gainDb, out b0, out b1, out b2, out a1, out a2);
                    break;
                case BiquadType.HighShelf:
                    Biquad.DesignHighShelf(fixedFreq.Value, gainDb, out b0, out b1, out b2, out a1, out a2);
                    break;
            }
        }
    }

    public float Process(in EffectContext ctx)
    {
        float gDb = gainDb;
        if (velocityAffectsGain)
            gDb *= 1f + gainVelocityScale * (gainVelocityCurve(ctx.VelocityNorm) - 1f);

        float freq = fixedFreq ?? ctx.Note.FrequencyHz * noteFrequencyMultiplier!.Value;

        float b0m = b0, b1m = b1, b2m = b2, a1m = a1, a2m = a2;
        if (!fixedFreq.HasValue || gDb != gainDb)
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
            Freq = fixedFreq,
            NoteFrequencyMultiplier = noteFrequencyMultiplier,
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
        fixedFreq = null;
        noteFrequencyMultiplier = null;
        base.OnReturn();
    }
}

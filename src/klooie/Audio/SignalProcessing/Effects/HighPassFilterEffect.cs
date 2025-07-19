using System;

namespace klooie;
[SynthDescription("""
Basic high-pass filter for removing low frequencies below the chosen cutoff.
Cutoff can be either an absolute frequency or a multiple of the note's fundamental frequency.
""")]
[SynthCategory("Filter")]
public class HighPassFilterEffect : Recyclable, IEffect
{
    private float prevInput;
    private float prevOutput;
    private float mix = 1f;
    private bool velocityAffectsMix;
    private Func<float, float> mixVelocityCurve = EffectContext.EaseLinear;

    private float? fixedCutoffHz;
    private float? noteFrequencyMultiplier;

    private static readonly LazyPool<HighPassFilterEffect> _pool = new(() => new HighPassFilterEffect());

    protected HighPassFilterEffect() { }

    [SynthDescription("""
Settings describing how to compute the cutoff frequency and how the mix reacts
to note velocity. Specify exactly one of CutoffHz or NoteFrequencyMultiplier.
""")]
    public struct Settings
    {
        [SynthDescription("""
Fixed cutoff frequency in hertz. Must not be set if NoteFrequencyMultiplier is set.
""")]
        public float? CutoffHz;

        [SynthDescription("""
If set, cutoff frequency is computed as note frequency × this multiplier.
Must not be set if CutoffHz is set.
""")]
        public float? NoteFrequencyMultiplier;

        [SynthDescription("""
Amount of filtered signal mixed with the original
(0 = dry, 1 = fully filtered).
""")]
        public float Mix;

        [SynthDescription("""
When true, harder played notes increase the mix of
the filtered signal.
""")]
        public bool VelocityAffectsMix;

        [SynthDescription("""
Function mapping normalized velocity to a
mix multiplier.
""")]
        public Func<float, float>? MixVelocityCurve;
    }

    public static HighPassFilterEffect Create(in Settings settings)
    {
        if (settings.CutoffHz.HasValue == settings.NoteFrequencyMultiplier.HasValue)
            throw new ArgumentException("Specify exactly one of CutoffHz or NoteFrequencyMultiplier.");

        var ret = _pool.Value.Rent();
        ret.Construct(settings);
        return ret;
    }

    protected void Construct(in Settings settings)
    {
        prevInput = 0f;
        prevOutput = 0f;

        fixedCutoffHz = settings.CutoffHz;
        noteFrequencyMultiplier = settings.NoteFrequencyMultiplier;
        mix = settings.Mix;
        velocityAffectsMix = settings.VelocityAffectsMix;
        mixVelocityCurve = settings.MixVelocityCurve ?? EffectContext.EaseLinear;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            CutoffHz = fixedCutoffHz,
            NoteFrequencyMultiplier = noteFrequencyMultiplier,
            Mix = mix,
            VelocityAffectsMix = velocityAffectsMix,
            MixVelocityCurve = mixVelocityCurve
        };
        return Create(in settings);
    }

    public float Process(in EffectContext ctx)
    {
        float cutoff = fixedCutoffHz ?? (ctx.Note.FrequencyHz * noteFrequencyMultiplier!.Value);
        float dt = 1f / SoundProvider.SampleRate;
        float rc = 1f / (2f * MathF.PI * cutoff);
        float alpha = rc / (rc + dt);

        float input = ctx.Input;
        float output = alpha * (prevOutput + input - prevInput);
        prevInput = input;
        prevOutput = output;

        float mixAmt = mix;
        if (velocityAffectsMix)
            mixAmt *= mixVelocityCurve(ctx.VelocityNorm);

        return input * (1 - mixAmt) + output * mixAmt;
    }

    protected override void OnReturn()
    {
        prevInput = 0f;
        prevOutput = 0f;
        mix = 1f;
        velocityAffectsMix = false;
        mixVelocityCurve = EffectContext.EaseLinear;
        fixedCutoffHz = null;
        noteFrequencyMultiplier = null;
        base.OnReturn();
    }
}

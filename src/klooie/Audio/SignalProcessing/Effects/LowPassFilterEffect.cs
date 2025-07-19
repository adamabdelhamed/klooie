using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
[SynthDescription("""
Simple low-pass filter that reduces high frequencies.
The cutoff can be fixed in hertz or follow the note's
pitch by using a multiplier.
Use a fixed cutoff for consistent tone or a multiplier
to keep the filter in tune with each note.
""")]
[SynthCategory("Filter")]
class LowPassFilterEffect : Recyclable, IEffect
{
    private float alpha;
    private float state;
    private float? fixedCutoffHz;
    private float? noteFrequencyMultiplier;
    private float mix = 1f;
    private bool velocityAffectsMix;
    private Func<float, float> mixVelocityCurve = EffectContext.EaseLinear;

    private static readonly LazyPool<LowPassFilterEffect> _pool = new(() => new LowPassFilterEffect());

    private LowPassFilterEffect() { }

    [SynthDescription("""
    Settings describing how the cutoff is chosen and how
    strongly the filtered signal is mixed in. Specify either
    a fixed cutoff in hertz or a multiplier that is applied
    to the note's frequency.
    """)]
    public struct Settings
    {
        [SynthDescription("""
        Fixed cutoff frequency in hertz. Leave null to use
        NoteFrequencyMultiplier instead.
        """)]
        public float? CutoffHz;

        [SynthDescription("""
        If set, cutoff = note frequency × this multiplier.
        Leave null to use CutoffHz.
        """)]
        public float? NoteFrequencyMultiplier;

        [SynthDescription("""
Mix level between the original and filtered signal
(0 = dry, 1 = filtered).
""")]
        public float Mix;

        [SynthDescription("""
If true, note velocity changes how much of the
filtered signal is heard.
""")]
        public bool VelocityAffectsMix;

        [SynthDescription("""
Function converting velocity into a mix
multiplier.
""")]
        public Func<float, float>? MixVelocityCurve;
    }

    public static LowPassFilterEffect Create(in Settings settings)
    {
        if (settings.CutoffHz.HasValue == settings.NoteFrequencyMultiplier.HasValue)
            throw new ArgumentException("Specify exactly one of CutoffHz or NoteFrequencyMultiplier.");

        var fx = _pool.Value.Rent();
        fx.Construct(in settings);
        return fx;
    }

    protected void Construct(in Settings settings)
    {
        state = 0f;
        fixedCutoffHz = settings.CutoffHz;
        noteFrequencyMultiplier = settings.NoteFrequencyMultiplier;
        mix = settings.Mix;
        velocityAffectsMix = settings.VelocityAffectsMix;
        mixVelocityCurve = settings.MixVelocityCurve ?? EffectContext.EaseLinear;

        if (fixedCutoffHz.HasValue)
        {
            float dt = 1f / SoundProvider.SampleRate;
            float rc = 1f / (2f * MathF.PI * fixedCutoffHz.Value);
            alpha = dt / (rc + dt);
        }
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
        float input = ctx.Input;
        float a = alpha;
        if (!fixedCutoffHz.HasValue)
        {
            float cutoff = ctx.Note.FrequencyHz * noteFrequencyMultiplier!.Value;
            float dt = 1f / SoundProvider.SampleRate;
            float rc = 1f / (2f * MathF.PI * cutoff);
            a = dt / (rc + dt);
        }
        state += a * (input - state);
        float wet = state;
        float mixAmt = mix;
        if (velocityAffectsMix)
            mixAmt *= mixVelocityCurve(ctx.VelocityNorm);
        return input * (1 - mixAmt) + wet * mixAmt;
    }

    protected override void OnReturn()
    {
        state = 0f;
        alpha = 0f;
        fixedCutoffHz = null;
        noteFrequencyMultiplier = null;
        mix = 1f;
        velocityAffectsMix = false;
        mixVelocityCurve = EffectContext.EaseLinear;
        base.OnReturn();
    }
}

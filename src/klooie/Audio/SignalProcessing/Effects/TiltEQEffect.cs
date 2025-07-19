using System;

namespace klooie;

/// <summary>
/// First-order tilt-EQ centred around a gentle low-pass.
/// Positive <c>tilt</c> brightens; negative warms.
/// </summary>
[SynthDescription("""
Tilt EQ that boosts highs while cutting lows (or the reverse).
The internal low‑pass cutoff may be a fixed frequency or a
multiple of the played note. Use the multiplier when you want
the tilt to track the note's pitch.
""")]
[SynthCategory("Filter")]
public sealed class TiltEQEffect : Recyclable, IEffect
{
    private float tilt;     // -1 (bass boost) … +1 (treble boost)
    private float alpha;    // LPF coefficient computed from cutoff
    private float low;      // running low-passed state
    private float? fixedCutoffHz;
    private float? noteFrequencyMultiplier;

    private static readonly LazyPool<TiltEQEffect> _pool = new(() => new TiltEQEffect());
    private TiltEQEffect() { }

    [SynthDescription("""
    Settings defining the tilt amount and how the low‑pass cutoff
    is determined. Provide either a fixed cutoff or a multiplier
    of the note frequency so the EQ follows the pitch.
    """)]
    public struct Settings
    {
        [SynthDescription("""
Tilt amount from -1 (bass boost) to +1 (treble
boost).
""")]
        public float Tilt;

        [SynthDescription("""
        Fixed cutoff for the internal low-pass in hertz. Leave
        null to use NoteFrequencyMultiplier.
        """)]
        public float? CutoffHz;

        [SynthDescription("""
        If set, cutoff = note frequency × this multiplier. Leave
        null to use CutoffHz.
        """)]
        public float? NoteFrequencyMultiplier;
    }

    public static TiltEQEffect Create(in Settings settings)
    {
        if (settings.CutoffHz.HasValue == settings.NoteFrequencyMultiplier.HasValue)
            throw new ArgumentException("Specify exactly one of CutoffHz or NoteFrequencyMultiplier.");

        var fx = _pool.Value.Rent();
        fx.Construct(in settings);
        return fx;
    }

    private void Construct(in Settings settings)
    {
        tilt = settings.Tilt;
        fixedCutoffHz = settings.CutoffHz;
        noteFrequencyMultiplier = settings.NoteFrequencyMultiplier;
        if (fixedCutoffHz.HasValue)
        {
            float dt = 1f / SoundProvider.SampleRate;
            float rc = 1f / (2f * MathF.PI * fixedCutoffHz.Value);
            alpha = dt / (rc + dt);
        }
        low = 0f;
    }

    public IEffect Clone()
    {
        var settings = new Settings
        {
            Tilt = tilt,
            CutoffHz = fixedCutoffHz,
            NoteFrequencyMultiplier = noteFrequencyMultiplier
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
        /* split ---------------------------------------------------------------*/
        low += a * (input - low);          // 1-pole LP -> "bass"
        float high = input - low;              // residual  -> "treble"

        /* tilt mix --------------------------------------------------------------*/
        return low * (1f - tilt) +
               high * (1f + tilt);
    }

    protected override void OnReturn()
    {
        tilt = alpha = low = 0f;
        fixedCutoffHz = null;
        noteFrequencyMultiplier = null;
        base.OnReturn();
    }
}

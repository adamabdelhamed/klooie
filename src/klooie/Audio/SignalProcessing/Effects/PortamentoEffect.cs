using System;

namespace klooie;

/// <summary>
/// Smooth portamento ("glide") pitch transition effect.
/// At the start of each note, if there was a previous note played legato (overlapping or gap < 10ms),
/// smoothly glides from the previous note's pitch to the new pitch over a fixed duration.
/// </summary>
[SynthCategory("Modulation")]
[SynthDocumentation("""
Adds classic synthesizer portamento ("glide") between legato notes.
When you play a new note with the previous note still held (or nearly overlapping), 
the pitch smoothly glides from the previous note to the new note over a fixed short duration.
""")]
public class PortamentoEffect : Recyclable, IPitchModEffect
{
    private static readonly LazyPool<PortamentoEffect> _pool = new(() => new PortamentoEffect());
    private PortamentoEffect() { }

    // Fixed portamento duration in seconds
    private const float PortamentoTime = 0.08f; // 80 ms for classic glide

    /// <summary>
    /// Returns a recycled instance. No settings for now.
    /// </summary>
    public static PortamentoEffect Create()
    {
        return _pool.Value.Rent();
    }

    /// <summary>
    /// Returns the pitch offset in cents for the current sample, gliding from the previous note's pitch
    /// to the current note's pitch if played legato. If not legato, returns zero (no glide).
    /// </summary>
    public float GetPitchOffsetCents(in PitchModContext ctx)
    {
        var prev = ctx.NoteEvent.Previous;
        if (prev == null)
            return 0f;

         // Only glide if the previous note ended less than 10ms before this one started ("legato" or overlap)
        var prevEnd = prev.StartTime + prev.DurationTime;
        double timeGap = (ctx.NoteEvent.Note.StartTime - prevEnd).TotalSeconds;
        if (timeGap > 0.01)
            return 0f;
        
        // Pitch difference in cents
        float diffCents = (ctx.NoteEvent.Note.MidiNote - prev.MidiNote) * 100f;
        // Glide time, clamped to portamento time
        float t = MathF.Min(ctx.Time, PortamentoTime);

        // Linear glide from prev pitch to current pitch
        return diffCents * (t / PortamentoTime - 1f);
        // Actually, classic portamento starts at prev pitch and moves toward current: 
        // At t = 0, offset = -diffCents; at t = PortamentoTime, offset = 0
        // So: offset = -diffCents * (1 - t / PortamentoTime)
    }

    /// <summary>
    /// Passes through the audio unchanged (only affects pitch).
    /// </summary>
    public float Process(in EffectContext ctx) => ctx.Input;

    /// <summary>
    /// Clones the effect. No state to copy.
    /// </summary>
    public IEffect Clone() => Create();

    protected override void OnReturn()
    {
        // No state to reset
        base.OnReturn();
    }
}

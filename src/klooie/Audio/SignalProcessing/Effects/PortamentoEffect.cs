using System;

namespace klooie;

[SynthCategory("Modulation")]
public class PortamentoEffect : Recyclable, IPitchModEffect
{
    private float glideSeconds;
    private static readonly LazyPool<PortamentoEffect> _pool = new(() => new PortamentoEffect());
    private PortamentoEffect() { }

    public struct Settings
    {
        public float GlideSeconds;
    }

    public static PortamentoEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.glideSeconds = settings.GlideSeconds;
        return fx;
    }

    public float GetPitchOffsetCents(in PitchModContext ctx)
    {
        var next = ctx.NoteEvent.Next;
        if (next == null) return 0f;

        float diffCents = (next.Note.MidiNote - ctx.NoteEvent.Note.MidiNote) * 100f;
        float timeToNext = (float)(next.Note.StartTime - ctx.NoteEvent.Note.StartTime).TotalSeconds;
        float dur = MathF.Min(glideSeconds, timeToNext);
        if (dur <= 0) return diffCents;
        float t = MathF.Min(ctx.Time, dur);
        return diffCents * (t / dur);
    }

    public float Process(in EffectContext ctx) => ctx.Input;

    public IEffect Clone() => Create(new Settings { GlideSeconds = glideSeconds });

    protected override void OnReturn()
    {
        glideSeconds = 0f;
        base.OnReturn();
    }
}

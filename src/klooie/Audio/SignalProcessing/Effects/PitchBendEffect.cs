using klooie;
using System.Drawing;

public interface IPitchModEffect : IEffect
{
    float GetPitchOffsetCents(in PitchModContext ctx);
}


[SynthDocumentation("""
Modulates pitch during note attack and release using user-provided curves
expressed in cents.
""")]
[SynthCategory("Modulation")]
public class PitchBendEffect : Recyclable, IPitchModEffect
{
    private Func<float, float> attackBendFunc;
    private Func<float, float> releaseBendFunc;
    private float attackDuration;
    private float releaseDuration;

    private static readonly LazyPool<PitchBendEffect> _pool = new(() => new PitchBendEffect());

    private PitchBendEffect() { }

    [SynthDocumentation("""
Functions and timing values defining the attack and release pitch bends.
""")]
    public struct Settings
    {
        [SynthDocumentation("""
Function that returns a pitch offset (in cents)
over the course of the attack.
""")]
        public Func<float, float> AttackBend;

        [SynthDocumentation("""
Length of the attack bend in seconds.
""")]
        public float AttackDuration;

        [SynthDocumentation("""
Function providing the pitch offset curve during
the release phase.
""")]
        public Func<float, float> ReleaseBend;

        [SynthDocumentation("""
Length of the release bend in seconds.
""")]
        public float ReleaseDuration;
    }

    public static PitchBendEffect Create(in Settings settings)
    {
        var fx = _pool.Value.Rent();
        fx.attackBendFunc = settings.AttackBend;
        fx.attackDuration = settings.AttackDuration;
        fx.releaseBendFunc = settings.ReleaseBend;
        fx.releaseDuration = settings.ReleaseDuration;
        return fx;
    }

    // The noteReleaseTime is nullable; if null, note is still held
    public float GetPitchOffsetCents(in PitchModContext ctx)
    {
        if (ctx.ReleaseTime == null || ctx.Time < ctx.ReleaseTime)
        {
            // attack/sustain phase
            float t = Math.Min(ctx.Time, attackDuration);
            return attackBendFunc(t);
        }
        else
        {
            // release/decay phase
            float tRelease = ctx.Time - ctx.ReleaseTime.Value;
            float t = Math.Min(tRelease, releaseDuration);
            return releaseBendFunc(t);
        }
    }

    public float Process(in EffectContext ctx) => ctx.Input;

    public IEffect Clone()
    {
        var settings = new Settings
        {
            AttackBend = attackBendFunc,
            AttackDuration = attackDuration,
            ReleaseBend = releaseBendFunc,
            ReleaseDuration = releaseDuration
        };
        return Create(in settings);
    }

    protected override void OnReturn()
    {
        attackBendFunc = null!;
        releaseBendFunc = null!;
        attackDuration = 0;
        releaseDuration = 0;
        base.OnReturn();
    }
}


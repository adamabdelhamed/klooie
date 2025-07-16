using klooie;
using System.Drawing;

public interface IPitchModEffect : IEffect
{
    float GetPitchOffsetCents(in PitchModContext ctx);
}


public class PitchBendEffect : Recyclable, IPitchModEffect
{
    private Func<float, float> attackBendFunc;
    private Func<float, float> releaseBendFunc;
    private float attackDuration;
    private float releaseDuration;

    private static readonly LazyPool<PitchBendEffect> _pool = new(() => new PitchBendEffect());

    private PitchBendEffect() { }

    public static PitchBendEffect Create(Func<float, float> attackBend, float attackDur, Func<float, float> releaseBend, float releaseDur)
    {
        var fx = _pool.Value.Rent();
        fx.attackBendFunc = attackBend;
        fx.attackDuration = attackDur;
        fx.releaseBendFunc = releaseBend;
        fx.releaseDuration = releaseDur;
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

    public IEffect Clone() => Create(attackBendFunc, attackDuration, releaseBendFunc, releaseDuration);

    protected override void OnReturn()
    {
        attackBendFunc = null!;
        releaseBendFunc = null!;
        attackDuration = 0;
        releaseDuration = 0;
        base.OnReturn();
    }
}


using klooie;
using System.Drawing;

public interface IPitchModEffect : IEffect
{
    float GetPitchOffsetCents(float time, float? releaseTime); // <- note new arg
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
    public float GetPitchOffsetCents(float time, float? noteReleaseTime)
    {
        if (noteReleaseTime == null || time < noteReleaseTime)
        {
            // attack/sustain phase
            float t = Math.Min(time, attackDuration);
            return attackBendFunc(t);
        }
        else
        {
            // release/decay phase
            float tRelease = time - noteReleaseTime.Value;
            float t = Math.Min(tRelease, releaseDuration);
            return releaseBendFunc(t);
        }
    }

    public float Process(float input, int frameIndex, float time) => input;

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


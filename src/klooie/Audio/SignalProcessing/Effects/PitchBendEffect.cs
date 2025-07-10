using klooie;
using System.Drawing;

public interface IPitchModEffect : IEffect
{
    float GetPitchOffsetCents(float time); // returns additive cents to apply at given time
}


public class PitchBendEffect : Recyclable, IPitchModEffect
{
    private Func<float, float> bendFunc; // Function: time (seconds) -> cents
    private float duration;

    private static readonly LazyPool<PitchBendEffect> _pool = new(() => new PitchBendEffect());

    private PitchBendEffect() { }

    public static PitchBendEffect Create(Func<float, float> bendFunc, float duration)
    {
        var fx = _pool.Value.Rent();
        fx.bendFunc = bendFunc;
        fx.duration = duration;
        return fx;
    }

    public float GetPitchOffsetCents(float time)
    {
        var cents = (time <= duration) ? bendFunc(time) : bendFunc(duration);
        return cents;
    }

    public float Process(float input, int frameIndex, float time) => input; // No audio processing

    public IEffect Clone() => Create(bendFunc, duration);

    protected override void OnReturn()
    {
        bendFunc = null!;
        duration = 0;
        base.OnReturn();
    }
}

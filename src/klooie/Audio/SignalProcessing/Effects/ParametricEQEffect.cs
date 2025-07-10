using klooie;

public enum BiquadType { Peak, LowShelf, HighShelf }

public class ParametricEQEffect : Recyclable, IEffect
{
    // Filter params
    private BiquadType type;
    private float freq;
    private float gainDb;
    private float q;

    // Coeffs/state
    private float b0, b1, b2, a1, a2;
    private Biquad.State state;

    private static readonly LazyPool<ParametricEQEffect> _pool = new(() => new ParametricEQEffect());

    private ParametricEQEffect() { }

    public static ParametricEQEffect Create(BiquadType type, float freq, float gainDb, float q = 1.0f)
    {
        var fx = _pool.Value.Rent();
        fx.type = type;
        fx.freq = freq;
        fx.gainDb = gainDb;
        fx.q = q;
        fx.state = new Biquad.State();

        // Design coeffs
        switch (type)
        {
            case BiquadType.Peak:
                Biquad.DesignPeak(freq, q, gainDb, out fx.b0, out fx.b1, out fx.b2, out fx.a1, out fx.a2);
                break;
            case BiquadType.LowShelf:
                Biquad.DesignLowShelf(freq, gainDb, out fx.b0, out fx.b1, out fx.b2, out fx.a1, out fx.a2);
                break;
            case BiquadType.HighShelf:
                Biquad.DesignHighShelf(freq, gainDb, out fx.b0, out fx.b1, out fx.b2, out fx.a1, out fx.a2);
                break;
        }
        return fx;
    }

    public float Process(float input, int frameIndex, float time)
        => Biquad.Process(ref state, b0, b1, b2, a1, a2, input);

    public IEffect Clone()
        => Create(type, freq, gainDb, q);

    protected override void OnReturn()
    {
        state = default;
        base.OnReturn();
    }
}

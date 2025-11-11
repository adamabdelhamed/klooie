
using klooie.Gaming;

namespace klooie;
public sealed class Friction : Recyclable
{
    public const int DefaultFrictionEvalFrequency = 50;
    public const float DefaultDecay = .9f;
    private float decay;
    private GameCollider collider;
    private int evalFrequency;

    private static LazyPool<Friction> pool = new LazyPool<Friction>(() => new Friction());

    public static void Bind(GameCollider collider, int evalFrequency = DefaultFrictionEvalFrequency, float decay = DefaultDecay)
    {
        var me = pool.Value.Rent();
        me.collider = collider;
        me.evalFrequency = evalFrequency;
        me.decay = decay;
        collider.OnDisposed(me, DisposeMe);
        var state = DelayState.Create(me);
        state.AddDependency(collider);
        state.AddDependency(collider.Velocity);
        Game.Current.PausableScheduler.DelayIfValid(me.evalFrequency, state, static state => ((Friction)(state.MainDependency)).Execute(state));
    }

    private static void DisposeMe(object obj) => (obj as Friction).TryDispose();

    private void Execute(DelayState state)
    {
        this.collider.Velocity.Speed *= this.decay;
        if (this.collider.Velocity.Speed < .1f) this.collider.Velocity.Speed = 0;
        Game.Current.PausableScheduler.DelayIfValid(this.evalFrequency, DelayState.Create(this), Execute);
    }
}

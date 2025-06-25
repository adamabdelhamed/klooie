
using klooie.Gaming;

namespace klooie;
public sealed class Friction : Recyclable
{
    public const int DefaultFrictionEvalFrequency = 50;
    public const float DefaultDecay = .9f;
    private float decay;
    private GameCollider collider;
    private int evalFrequency;
    public void Bind(GameCollider collider, int evalFrequency = DefaultFrictionEvalFrequency, float decay = DefaultDecay)
    {
        this.collider = collider;
        this.evalFrequency = evalFrequency;
        this.decay = decay;
        collider.OnDisposed(this, DisposeMe);
        var state = DelayState.Create(this);
        state.AddDependency(collider);
        state.AddDependency(collider.Velocity);
        ConsoleApp.Current.Scheduler.DelayIfValid(this.evalFrequency, state, Execute);
    }

    private static void DisposeMe(object obj) => (obj as Friction).TryDispose();

    private void Execute(DelayState state)
    {
        this.collider.Velocity.Speed *= this.decay;
        if (this.collider.Velocity.Speed < .1f) this.collider.Velocity.Speed = 0;
        ConsoleApp.Current.Scheduler.DelayIfValid(this.evalFrequency, DelayState.Create(this), Execute);
    }
}

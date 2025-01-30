
namespace klooie;
public sealed class Friction : Recyclable
{
    public const int DefaultFrictionEvalFrequency = 50;
    public const float DefaultDecay = .9f;
    private float decay;
    private Velocity velocity;
    private int evalFrequency;
    public void Bind(Velocity v, int evalFrequency = DefaultFrictionEvalFrequency, float decay = DefaultDecay)
    {
        this.velocity = v;
        this.evalFrequency = evalFrequency;
        this.decay = decay;
        velocity.Collider.OnDisposed(this, DisposeMe);
        Execute(this);
    }

    private static void DisposeMe(object obj) => (obj as Friction).TryDispose();

    private static void Execute(object me)
    {
        var friction = me as Friction;
        if (friction.ShouldStop) return;
        
        friction.velocity.Speed *= friction.decay;
        if (friction.velocity.Speed < .1f) friction.velocity.Speed = 0;
        ConsoleApp.Current.InnerLoopAPIs.Delay(friction.evalFrequency, me, Execute);
    }
}

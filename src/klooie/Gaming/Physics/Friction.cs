
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
        Execute(this);
    }

    private static void DisposeMe(object obj) => (obj as Friction).TryDispose();

    private void Execute(object leaseObj)
    {
        var lease = leaseObj as int? ?? 0;
        if (this.IsStillValid(lease) == false) return;
        
        this.collider.Velocity.Speed *= this.decay;
        if (this.collider.Velocity.Speed < .1f) this.collider.Velocity.Speed = 0;
        ConsoleApp.Current.InnerLoopAPIs.Delay(this.evalFrequency, lease, Execute);
    }
}

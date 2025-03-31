
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

    private void Execute(object leaseObj)
    {
        var lease = leaseObj as int? ?? 0;
        if (this.IsStillValid(lease) == false) return;
        
        this.velocity.Speed *= this.decay;
        if (this.velocity.Speed < .1f) this.velocity.Speed = 0;
        ConsoleApp.Current.InnerLoopAPIs.Delay(this.evalFrequency, lease, Execute);
    }
}

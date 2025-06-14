using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie.Gaming;
public class MovementState : DelayState
{
    public Vision Vision { get; private set; }
    public Velocity Velocity { get; private set; }
    public Targeting Targeting { get; private set; }
    public GameCollider Eye { get; private set; }
    public Func<MovementState, RectF?> CuriosityPoint { get; set; }
    public Func<float> Speed { get; set; }

    protected MovementState() { }
    private static LazyPool<MovementState> pool = new LazyPool<MovementState>(() => new MovementState());

    protected virtual void Construct(Targeting targeting, Func<MovementState, RectF?> curiosityPoint, Func<float> speed)
    {
        AddDependency(targeting);
        AddDependency(targeting.Options.Vision);
        AddDependency(targeting.Options.Vision.Eye);
        Vision = targeting.Options.Vision;
        Targeting = targeting;
        Eye = targeting.Options.Vision.Eye;
        Velocity = Eye.Velocity;
        CuriosityPoint = curiosityPoint;
        Speed = speed;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Targeting = null;
        Eye = null;
        Vision = null;
        Velocity = null;
        CuriosityPoint = null;
        Speed = null;
    }
}
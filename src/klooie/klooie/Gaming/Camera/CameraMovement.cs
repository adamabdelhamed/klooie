namespace klooie.Gaming;
public abstract class CameraMovement : Lifetime
{
    public Camera Camera { get; set; }
    public int CheckPriority { get; protected set; } = int.MaxValue;
    public Event<int> SituationDetected { get; private set; } = new Event<int>();
    public ILifetimeManager MovementLifetime { get; set; }
    public abstract Task Move();
    public bool IsMoving => MovementLifetime != null;
    public ConsoleControl FocalElement { get; set; }
    public Velocity FocalVelocity { get; set; }
    public LocF FocalPoint => FocalElement.Bounds.Center;
    public bool IsOutOfBounds => Camera.CameraBounds.OverlapPercentage(FocalElement.Bounds) < 1;

    public IDelayProvider DelayProvider { get; set; }

    public virtual void Init() { }

}


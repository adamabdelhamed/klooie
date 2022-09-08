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


    protected RectF EnsureWithinBigBounds(RectF given)
    {
        var bounds = Camera.BigBounds;

        float x = given.Left < bounds.Left ? bounds.Left : given.Left;
        float y = given.Top < bounds.Top ? bounds.Top : given.Top;

        x = given.Right > bounds.Right ? bounds.Right - given.Width : x;
        y = given.Bottom > bounds.Bottom ? bounds.Bottom - given.Height : y;

        var ret = new RectF(x, y, given.Width, given.Height);
        return ret;
    }
}


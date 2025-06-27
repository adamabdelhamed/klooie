namespace klooie.Gaming;
/// <summary>
/// A movement that can be performed by a camera operator
/// </summary>
public abstract class CameraMovement : Recyclable
{
    /// <summary>
    /// Gets the camera
    /// </summary>
    public Camera Camera { get; internal set; }

    /// <summary>
    /// An event that the movement should fire when it believes it should take over
    /// as the current movement. The event argument is the priority (lower is more important)
    /// </summary>
    private Event<int> situationDetected;
    public Event<int> SituationDetected => situationDetected ??= Event<int>.Create();

    /// <summary>
    /// A lifetime that will be set for you before Move is called. It will be
    /// cleaned up for you as well.
    /// </summary>
    public ILifetime MovementLifetime { get; internal set; }

    /// <summary>
    /// Derived classes are free to move the camera during this call
    /// </summary>
    /// <returns></returns>
    public abstract Task Move();

    /// <summary>
    /// true if this move's Move method is currently running
    /// </summary>
    public bool IsMoving => MovementLifetime != null;

    /// <summary>
    /// An optional control to focus on
    /// </summary>
    public ConsoleControl FocalElement { get; internal set; }

    /// <summary>
    /// An optional velocity to focus on
    /// </summary>
    public Velocity FocalVelocity { get; internal set; }

    /// <summary>
    /// The center of the FocalElement
    /// </summary>
    public LocF FocalPoint => FocalElement.Bounds.Center;

    /// <summary>
    /// returns true if the current camera position cannot see the entire FocalElement
    /// </summary>
    public bool IsOutOfBounds => Camera.CameraBounds.OverlapPercentage(FocalElement.Bounds) < 1;


    /// <summary>
    /// Initialized your movement. It is not safe to operate the camera during this call.
    /// </summary>
    public virtual void Init() { }

    protected override void OnReturn()
    {
        base.OnReturn();
        situationDetected?.TryDispose();
        situationDetected = null;
        MovementLifetime = null;
        FocalElement = null;
        FocalVelocity = null;
        Camera = null;
    }
}


namespace klooie.Gaming;
public class WanderOptions : MovementOptions
{
    /// <summary>
    /// Point of interest to wander towards, if any. If set, the wanderer will
    /// try to move towards this point which may be an object or an empty space.
    /// 
    /// If not set, the wanderer will wander in the space, trying to avoid objects without
    /// bumping into them. 
    /// 
    /// In all cases, the wanderer will try to avoid turning backwards to avoid obstacles.
    /// Instead it will intelligently alter its trajectory to move around obstacles. That
    /// intelligence is informed by the curiosity point, if set, or by common sense if not.
    /// </summary>
    public Func<ICollidable>? CuriousityPoint { get; set; }

    /// <summary>
    /// When CuriosityPoint is set, the wanderer will stop wandering if it gets within
    /// this distance from the point. 
    /// </summary>
    public float CloseEnough { get; set; } = Mover.DefaultCloseEnough;

    /// <summary>
    /// Optional code to run before the wanderer is delayed for its next adjustment.
    /// </summary>
    public Action OnDelay { get; set; }
}

/// <summary>
/// A movement that wanders around the space, trying to avoid obstacles and
/// optionally moving towards a point of interest.
/// </summary>
public class Wander : Movement
{
    private const int DelayMs = 50;
    private TaskCompletionSource _tcs;
    public WanderOptions WanderOptions => (WanderOptions)Options;

    public static Movement Create(WanderOptions options)
    {
        var w = WanderPool.Instance.Rent();
        w.Bind(options);
        return w;
    }

    private void Bind(WanderOptions opts)
    {
        base.Bind(opts);
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _tcs?.TrySetResult();
        _tcs = null;
    }

    protected override Task Move()
    {
        _tcs = new TaskCompletionSource();
        var state = WanderLoopState.Create(this);
        MoveOnce(state);
        return _tcs.Task;
    }

    private void MoveOnce(WanderLoopState state)
    {
        if (!state.IsStillValid())
        {
            state.Dispose();
            Finish();
            return;
        }
        WanderLogic.AdjustSpeedAndVelocity(state);

        if (!state.IsStillValid())
        {
            state.Dispose();
            Finish();
            return;
        }

        ScheduleNextMove(state);
    }

 

    private void ScheduleNextMove(WanderLoopState state)
    {
        ConsoleApp.Current.InnerLoopAPIs.Delay(DelayMs, state, MoveOnceStatic);
    }

    private static void MoveOnceStatic(object o)
    {
        var state = (WanderLoopState)o;
        state.Wander.WanderOptions.OnDelay?.Invoke();
        state.Wander.MoveOnce(state);
    }

    private void Finish()
    {
        _tcs?.TrySetResult();
    }
}

public class WanderLoopState : Recyclable
{
    public Wander Wander { get; private set; } = null!;
    public int WanderLease { get; private set; }
    public int ElementLease { get; private set; }

    /// <summary>
    /// The last few angles the wanderer has taken. This is used to avoid
    /// turning backwards over and over and to help the wanderer make intelligent decisions
    /// </summary>
    public List<Angle> LastFewAngles = new List<Angle>();

    public bool IsStillValid()
        => Wander.IsStillValid(WanderLease)
        && Wander.Options.Velocity?.Collider.IsStillValid(ElementLease) == true;

    public static WanderLoopState Create(Wander w)
    {
        var s = WanderLoopStatePool.Instance.Rent();
        s.Wander = w;
        s.WanderLease = w.Lease;
        s.ElementLease = w.Options.Velocity.Collider.Lease;
        return s;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Wander = null!;
        ElementLease = 0;
        WanderLease = 0;
        LastFewAngles.Clear();
    }
}

/// <summary>
/// Logic for the Wander movement, including speed and velocity adjustments.
/// 
/// Requirements:
/// - The wanderer intelligentlys avoid obstacles by altering its trajectory a few seconds before collision.
/// - The wanderer strongly prefers to walk around obstacles rather than turning around and going back.
/// - If a curiosity point is set, the wanderer should try to move towards it. Speed should be set to zero if the wanderer is close enough to the curiosity point.
/// - If the curiosity point is not set, the wanderer should wander in the space, trying to avoid objects without bumping into them.
/// - All code of non-trivial complexity should be isolated in its own method that accepts the WanderLoopState as a parameter.
///     - All math of non-trivial complexity should have helpful variable names and comments.
/// - Wanderers and obstacles are rectangles that can be any size. The code should never assume a specific size.
/// </summary>
public static class WanderLogic
{
    public static void AdjustSpeedAndVelocity(WanderLoopState currentState)
    {
        var options = currentState.Wander.WanderOptions;
        var curiosityPoint = options.CuriousityPoint?.Invoke();
        var newSpeed = currentState.Wander.Options.Speed(); // initially set to default speed, set to zero if close enough to curiosity point
        var newAngle = currentState.Wander.Options.Velocity.Angle; // initially set to current angle, will be adjusted based on obstacles and curiosity point
        var trackedObjects = currentState.Wander.Options.Vision.TrackedObjectsList;

        // consider all of the objects that the wanderer can see
        for (var i = 0; i < trackedObjects.Count; i++)
        {
            var trackedObject = trackedObjects[i];
            var bounds = trackedObject.Target.Bounds; // a RectF with Top, Left, Width, Height, Center, TopEdge, BottomEdge, LeftEdge, RightEdge, etc.
            var angle = trackedObject.Angle; // The angle from the wanderer's perspective to the tracked object
            var distance = trackedObject.Distance; // The distance from the wanderer to the tracked object
            var closestEdge = trackedObject.RayCastResult.Edge; // The edge of the tracked object that was hit by the ray cast with From (LocF) and To (LocF) properties

            // TODO: Use the information from the tracked object to adjust the speed and angle of the wanderer
        }

        options.Velocity.Speed = newSpeed;
        options.Velocity.Angle = newAngle;

        currentState.LastFewAngles.Add(options.Velocity.Angle);
        if (currentState.LastFewAngles.Count > 5) currentState.LastFewAngles.RemoveAt(0);
    }
}


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
    public WanderOptions WanderOptions => (WanderOptions)Options;
    public static Wander Create(WanderOptions options)
    {
        var w = WanderPool.Instance.Rent();
        w.Bind(options);
        return w;
    }

    protected override Task Move()
    {
        var state = WanderLoopState.Create(this);
        Tick(state);
        return state.Task ?? Task.CompletedTask;
    }

    private void Tick(WanderLoopState state)
    {
        if (!IsStillValid(this, state))
        {            
            state.TryDispose();
            return;
        }
        WanderLogic.AdjustSpeedAndVelocity(state);

        if (!IsStillValid(this, state))
        {
            state.TryDispose();
            return;
        }

        ScheduleNextTick(state);
    }

    private void ScheduleNextTick(WanderLoopState state) => 
        ConsoleApp.Current.InnerLoopAPIs.Delay(DelayMs, state, StaticTick);

    private static void StaticTick(object o)
    {
        var state = (WanderLoopState)o;
        state.Wander.WanderOptions.OnDelay?.Invoke();
        state.Wander.Tick(state);
    }

    public bool IsStillValid(Wander wander, WanderLoopState state) => 
        wander.IsStillValid(state.WanderLease)
        && wander.Options.Vision.IsStillValid(state.VisionLease)
        && wander.Options.Velocity?.Collider.IsStillValid(state.ElementLease) == true;
}






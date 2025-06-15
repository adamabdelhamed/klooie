using klooie;
using PowerArgs;

#if DEBUG
namespace klooie.Gaming;
public class WanderDebugger : Wander
{
    private static readonly BackgroundColorFilter StuckFilter = new BackgroundColorFilter(RGB.Orange);
    private static readonly BackgroundColorFilter HasMovedRecentlyFilter = new BackgroundColorFilter(RGB.Green);
    private static readonly BackgroundColorFilter HasCollidedRecentlyFilter = new BackgroundColorFilter(RGB.Red);
    private static readonly BackgroundColorFilter LoopRunningFilter = new BackgroundColorFilter(RGB.DarkYellow);
    private static readonly BackgroundColorFilter IsAtPointOfInterestFilter = new BackgroundColorFilter(RGB.Magenta);
    private static readonly BackgroundColorFilter StaleEvaluationFilter = new BackgroundColorFilter(RGB.Gray);

    private static LazyPool<WanderDebugger> pool = new LazyPool<WanderDebugger>(() => new WanderDebugger());

    private TimeSpan lastMoveTime;
    private TimeSpan lastCollisionTime;
    private TimeSpan lastVelocityEvaluationTime;
    public bool HasMovedInPastSecond => Game.Current.MainColliderGroup.Now - lastMoveTime < TimeSpan.FromSeconds(1);
    public bool HasCollidedInPastSecond => Game.Current.MainColliderGroup.Now - lastCollisionTime < TimeSpan.FromSeconds(1);
    public bool HasBeenEvaluatedInPastSecond => Game.Current.MainColliderGroup.Now - lastVelocityEvaluationTime < TimeSpan.FromSeconds(1);

    protected override void Construct(Vision vision, Func<Movement, RectF?>? curiosityPoint, Func<float> speed)
    {
        base.Construct(vision, curiosityPoint, speed);
        Eye.Velocity.AfterEvaluate.Subscribe(this, static (me, eval) => me.OnAfterEvaluateVelocity(eval), this);
    }

    public static WanderDebugger Create(Vision vision, Func<Movement, RectF?> curiosityPoint, Func<float> speed)
    {
        var state = pool.Value.Rent();
        state.Construct(vision, curiosityPoint, speed);
        return state;
    }

    private void OnAfterEvaluateVelocity(Velocity.MoveEval eval)
    {
        lastVelocityEvaluationTime = Game.Current.MainColliderGroup.Now;
        lastMoveTime = eval.Result == Velocity.MoveEvalResult.Moved ? Game.Current.MainColliderGroup.Now : lastMoveTime;
        lastCollisionTime = eval.Result == Velocity.MoveEvalResult.Collision ? Game.Current.MainColliderGroup.Now : lastCollisionTime;
    }

    public void ClearStuckFilter() => ClearFilter(StuckFilter);

    public void ApplyStuckFilter() => SetFilter(StuckFilter);

    public void ApplyMovingFreeFilter()
    {
        if (Eye.Velocity != Velocity || Eye != Eye) throw new InvalidOperationException("Lifetime management bug: Velocity or Eye references bindings are out of sync");
        if (Velocity.ContainsInfluence(Influence) == false) throw new InvalidOperationException("Lifetime management bug: Velocity does not contain the Influence reference");
        if (Influence.DeltaSpeed < 1 && IsCurrentlyCloseEnoughToPointOfInterest == false) throw new InvalidOperationException($"Lifetime management bug: DeltaSpeed is {Influence.DeltaSpeed}, but should be at least 1 to be considered moving free.");

        SetOrClearFilter(HasCollidedInPastSecond, HasCollidedRecentlyFilter);
        SetOrClearFilter(IsCurrentlyCloseEnoughToPointOfInterest, IsAtPointOfInterestFilter);
        SetOrClearFilter(HasMovedInPastSecond, HasMovedRecentlyFilter);
        SetOrClearFilter(HasBeenEvaluatedInPastSecond == false && IsCurrentlyCloseEnoughToPointOfInterest == false, StaleEvaluationFilter);
    }

    public void RefreshLoopRunningFilter()
    {
        if (Eye.Filters.Contains(LoopRunningFilter)) return;
        
        Eye.Filters.Insert(0, LoopRunningFilter);
        var dependency = DelayState.Create(Eye);
        Game.Current.InnerLoopAPIs.DelayIfValid(250, dependency, RemoveRunningLoopFilter);
    }

    private static void RemoveRunningLoopFilter(object obj)
    {
        var state = (DelayState)obj;
        var eye = (GameCollider)state.MainDependency;
        if (eye.Filters.Contains(LoopRunningFilter))
        {
            eye.Filters.Remove(LoopRunningFilter);
        }
        state.Dispose();
    }

    private void SetOrClearFilter(bool condition, IConsoleControlFilter filter)
    {
        if (condition) SetFilter(filter);
        else ClearFilter(filter);
    }

    private void SetFilter(IConsoleControlFilter filter)
    {
        if (Eye?.Filters.Contains(filter) == true) return;
        Eye.Filters.Add(filter);
    }

    private void ClearFilter(IConsoleControlFilter filter)
    {
        if (Eye == null || Eye.Filters.Contains(filter) == false) return;
        Eye.Filters.Remove(filter);
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        ClearFilter(StuckFilter);
        ClearFilter(HasMovedRecentlyFilter);
        ClearFilter(HasCollidedRecentlyFilter);
        ClearFilter(LoopRunningFilter);
        ClearFilter(IsAtPointOfInterestFilter);
        ClearFilter(StaleEvaluationFilter);
        lastMoveTime = default;
        lastCollisionTime = default;
    }
}
#endif
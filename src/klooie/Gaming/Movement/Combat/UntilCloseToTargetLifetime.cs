namespace klooie.Gaming;

public class UntilCloseToTargetLifetime : Recyclable
{
    public GameCollider mover;
    public Targeting targeting;
    private float closeEnough;

    public UntilCloseToTargetLifetime() { }

    public static UntilCloseToTargetLifetime Create(GameCollider mover, Targeting targeting, float closeEnough)
    {
        var instance = UntilCloseToTargetLifetimePool.Instance.Rent();
        instance.mover = mover;
        instance.targeting = targeting;
        instance.closeEnough = closeEnough;
        var state = UntilCloseToTargetLifetimeState.Create(instance);
        Monitor(state);
        return instance;
    }

    private static void Monitor(object stateObj)
    {
        var state = (UntilCloseToTargetLifetimeState)stateObj;
        if (state.AreAllDependenciesValid == false)
        {
            state.Dispose();
            return;
        }

        var _this = state.Lifetime;

        if (_this.targeting.Target == null)
        {
            Game.Current.InnerLoopAPIs.DelayIfValid(250, state, Monitor);
            return;
        }
        if (_this.IsFinished())
        {
            state.Dispose();
            return;
        }
        Game.Current.InnerLoopAPIs.DelayIfValid(250, state, Monitor);
    }

    private bool IsFinished()
    {
        var buffer = ObstacleBufferPool.Instance.Rent();
        mover.GetObstacles(buffer);
        try
        {
            if (mover.CalculateDistanceTo(targeting.Target.Bounds) <= closeEnough && mover.HasLineOfSight(targeting.Target, buffer.WriteableBuffer))
            {
                TryDispose();
                return true;
            }
            return false;
        }
        finally
        {
            buffer.TryDispose();
        }
    }
}

public class UntilCloseToTargetLifetimeState : DelayState
{
    public UntilCloseToTargetLifetime Lifetime { get; set; }
    public static UntilCloseToTargetLifetimeState Create(UntilCloseToTargetLifetime lt)
    {
        var ret = UntilCloseToTargetLifetimeStatePool.Instance.Rent();
        ret.Lifetime = lt;
        ret.AddDependency(lt);
        ret.AddDependency(lt.mover);
        ret.AddDependency(lt.targeting);
        ret.AddDependency(lt.mover.Velocity);
        ret.AddDependency(lt.targeting.Options.Vision);
        return ret;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Lifetime = null;
    }
}
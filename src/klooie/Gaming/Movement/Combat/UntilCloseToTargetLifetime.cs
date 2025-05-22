namespace klooie.Gaming;

public class UntilCloseToTargetLifetime : Recyclable
{
    private GameCollider mover;
    private Targeting targeting;
    private float closeEnough;

    public UntilCloseToTargetLifetime() { }

    public static UntilCloseToTargetLifetime Create(GameCollider mover, Targeting targeting, float closeEnough)
    {
        var instance = UntilCloseToTargetLifetimePool.Instance.Rent();
        instance.mover = mover;
        instance.targeting = targeting;
        instance.closeEnough = closeEnough;
        Monitor(instance);
        return instance;
    }

    private static void Monitor(object me)
    {
        var _this = (UntilCloseToTargetLifetime)me;
        var lease = _this.Lease;
        var colliderLease = _this.mover.Lease;
        var targetingLease = _this.targeting.Lease;
 
        if (_this.IsStillValid(lease) && _this.mover.IsStillValid(colliderLease))
        {
            if (_this.targeting.Target == null)
            {
                Game.Current.InnerLoopAPIs.Delay(250, me, Monitor);
                return;
            }
            if (_this.IsFinished()) return;
            Game.Current.InnerLoopAPIs.Delay(250, me, Monitor);
        }
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

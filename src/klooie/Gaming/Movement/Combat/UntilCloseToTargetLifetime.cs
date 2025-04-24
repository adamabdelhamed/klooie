using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        Game.Current.Invoke(instance.Monitor);
        return instance;
    }

    private async Task Monitor()
    {
        var lease = this.Lease;
        var colliderLease = mover.Lease;
        var targetingLease = targeting.Lease;
 
        while (IsStillValid(lease) && mover.IsStillValid(colliderLease))
        {
            if (targeting.Target == null)
            {
                await Task.Delay(250);
                continue;
            }
            var buffer = ObstacleBufferPool.Instance.Rent();
            mover.GetObstacles(buffer);
            try
            {
                if (mover.CalculateDistanceTo(targeting.Target.Bounds) <= closeEnough && mover.HasLineOfSight(targeting.Target, buffer.WriteableBuffer))
                {
                    TryDispose();
                    break;
                }
            }
            finally
            {
                buffer.TryDispose();
            }
            await Task.Delay(250);
        }
    }
}

using System.Runtime.CompilerServices;

namespace klooie.Gaming;
public sealed class ColliderGroup
{
    private const float MaxDTSeconds = .05f;
    private const float MaxDTMilliseconds = MaxDTSeconds * 1000f;
    private FrameRateMeter frameRateMeter = new FrameRateMeter();
    public int FramesPerSecond => frameRateMeter.CurrentFPS;

    private Event<Collision>? onCollision;
    public Event<Collision> OnCollision => onCollision ?? (onCollision = Event<Collision>.Create());

    private UniformGrid spatialIndex;
    public UniformGrid SpacialIndex => spatialIndex;
    private ObstacleBuffer queryBuffer = ObstacleBufferPool.Instance.Rent();
    public float LatestDT { get; private set; }

    // these properties model a linear progression that determines the appropriate min
    // evaluation time period for an object given it's current speed
    public const float LeastFrequentEval = 0.25f;        // 250 ms (4 Hz) for slowest movers
    public const float LowestSpeedForEvalCalc = 0f;      // x1
    public const float MostFrequentEval = .006f;        
    public const float HighestSpeedForEvalCalc = 60f;    // x2 (or higher, if needed)
    public const float EvalFrequencySlope =
        (MostFrequentEval - LeastFrequentEval) / (HighestSpeedForEvalCalc - LowestSpeedForEvalCalc);
    private ObstacleBuffer colliderBuffer;
    private CollisionPrediction hitPrediction;
    private ILifetime? lt;
    private TimeSpan lastExecuteTime;
    private float now;
    private double scaledNowSeconds;


    public float SpeedRatio { get; set; } = 1;


    public ColliderGroup(ILifetime lt, PauseManager? pauseManager, IStopwatch stopwatch = null)
    {
        this.lt = lt;
        lt.OnDisposed(() => this.lt = null);
        hitPrediction = new CollisionPrediction();
        this.stopwatch = stopwatch ?? new WallClockStopwatch();
        colliderBuffer = ObstacleBufferPool.Instance.Rent();
        lastExecuteTime = TimeSpan.Zero;
        spatialIndex = new UniformGrid();
        scaledNowSeconds = 0; // start scaled timeline at zero
        this.stopwatch.Start();
        pauseManager?.OnPaused.Subscribe(this, static (me,pauseLt) => me.HandlePause(pauseLt), lt);
        Game.Current?.AfterPaint?.SubscribePaused(pauseManager, Tick, lt);
        ConsoleApp.Current?.OnDisposed(Cleanup);
    }

    private void HandlePause(ILifetime pauseLt)
    {
        stopwatch.Stop();
        pauseLt.OnDisposed(this, static (me) => me.stopwatch.Start());
    }

    private void Cleanup()
    {
        colliderBuffer.WriteableBuffer.Clear();
        colliderBuffer.Dispose();
        queryBuffer.Dispose();

        colliderBuffer = null;
        queryBuffer = null;
        onCollision?.Dispose();
        onCollision = null;
    }

    internal void Register(GameCollider c)
    {
        c.lastEvalTime = (float)lastExecuteTime.TotalSeconds; 
        spatialIndex.Insert(c);
    }


    public TimeSpan WallClockNow => stopwatch.Elapsed; 
    public TimeSpan ScaledNow => TimeSpan.FromSeconds(scaledNowSeconds);
    private IStopwatch stopwatch;

    public void Tick()
    {
        UpdateTime();
        colliderBuffer.WriteableBuffer.Clear();
        spatialIndex.EnumerateAll(colliderBuffer);
        frameRateMeter.Increment();
        for (var i = 0; i < colliderBuffer.WriteableBuffer.Count; i++)
        {
            var item = colliderBuffer.WriteableBuffer[i];
            if(item == null) throw new InvalidOperationException($"Collider at index {i} is null. This should never happen, please report this issue.");
            Tick(item);
        }
    }

    private void UpdateTime()
    {
        var nowTime = WallClockNow; // always sample the real stopwatch here
        now = (float)nowTime.TotalSeconds;

        var dtMs = (float)(nowTime - lastExecuteTime).TotalMilliseconds;
        LatestDT = stopwatch.SupportsMaxDT ? Math.Min(MaxDTMilliseconds, dtMs) : dtMs;
        lastExecuteTime = nowTime;

        // --- New: advance scaled timeline using clamped dt and group SpeedRatio ---
        // Keep visuals in lockstep with physics by using the same clamp.
        double dtSecondsForScale = LatestDT / 1000.0;
        scaledNowSeconds += dtSecondsForScale * SpeedRatio;
    }

    private void Tick(GameCollider item)
    {
        if (spatialIndex.IsExpired(item)) return;
        if (IsReadyToMove(item) == false) return;

        var expectedTravelDistance = CalculateExpectedTravelDistance(item);
        if (TryDetectCollision(item, expectedTravelDistance))
        {
            var moved = ProcessCollision(item, expectedTravelDistance);
            item.Velocity?._afterEvaluate?.Fire(new Velocity.MoveEval(Velocity.MoveEvalResult.Collision, moved));
        }
        else
        {
            MoveColliderWithoutCollision(item, expectedTravelDistance);
            item.Velocity?._afterEvaluate?.Fire(new Velocity.MoveEval(Velocity.MoveEvalResult.Moved, expectedTravelDistance));
        }
    }

    private bool TryDetectCollision(GameCollider item, float expectedTravelDistance)
    {
        // Swept AABB: from where it is to where it’s going
        var from = item.Bounds;
        var to = from.RadialOffset(item.Velocity.Angle, expectedTravelDistance, false); // todo: Investigate why we're using false for reverse
        var swept = from.SweptAABB(to).Grow(.01f);

        queryBuffer.WriteableBuffer.Clear();
        spatialIndex.Query(swept, queryBuffer);
        var list = queryBuffer.WriteableBuffer;

        CollisionDetector.Predict(item, item.Velocity.Angle, list, expectedTravelDistance, CastingMode.Precise, list.Count, hitPrediction);
        hitPrediction.ColliderHit = hitPrediction.ColliderHit;
        item.Velocity.NextCollision = hitPrediction;
        return hitPrediction.CollisionPredicted;
    }

    private void MoveColliderWithoutCollision(GameCollider item, float expectedTravelDistance)
    {
        var colliderBoundsBeforeMovement = item.Bounds;
        var newLocation = item.Bounds.RadialOffset(item.Velocity.Angle, expectedTravelDistance, false);

        if (WouldCauseTouching(item, newLocation, out GameCollider preventer))
        {
#if DEBUG
            ColliderGroupDebugger.VelocityEventOccurred?.Fire(new FailedMove()
            {
                MovingObject = item,
                Obstacle = preventer,
                Angle = item.Velocity.Angle,
                From = item.Bounds,
                To = newLocation,
                NowSeconds = now
            });
#endif
            return;
        }

#if DEBUG
        ColliderGroupDebugger.VelocityEventOccurred?.Fire(new SuccessfulMove()
        {
            MovingObject = item,
            Angle = item.Velocity.Angle,
            From = colliderBoundsBeforeMovement,
            To = newLocation,
            NowSeconds = now
        });
#endif

        item.MoveTo(newLocation.Left, newLocation.Top);
    }

    private float ProcessCollision(GameCollider item, float expectedTravelDistance)
    {
        var originalLocation = item.Bounds;
        var otherBounds = hitPrediction.ColliderHit.Bounds;

        if (spatialIndex.IsExpired(item)) return 0f;

        var collision = CollisionPool.Instance.Rent();
        try
        {
            collision.Bind(item.Velocity.speed, item.Velocity.angle, item, hitPrediction.ColliderHit, hitPrediction);
            OnCollision.Fire(collision);
            item.Velocity?._onCollision?.Fire(collision);
            if (spatialIndex.IsExpired(item)) return 0f;

            if (item.Velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.DoNothing &&
                collision.ColliderHitLeaseState.IsRecyclableValid == false &&
                collision.MovingObjectLeaseState.IsRecyclableValid == true &&
                TryMoveIfWouldNotCauseTouching(item, originalLocation.RadialOffset(item.Velocity.angle, expectedTravelDistance, normalized: false), RGB.Red))
            {
                return expectedTravelDistance;
            }
        }
        finally
        {
            collision.Dispose();
        }

        if (spatialIndex.IsExpired(item)) return 0f;

        // .1 is large enough to be bigger than any epsilon used elsewhere, but small enough to not cross a cell boundary that typically rounds at .5
        var distanceToContact = Math.Max(0f, Math.Min(hitPrediction.LKGD, expectedTravelDistance) - .1f); 
        if (distanceToContact > 0f)
        {
            var contactBounds = originalLocation.RadialOffset(item.Velocity.angle, distanceToContact, normalized: false);
            item.MoveTo(contactBounds.Left, contactBounds.Top);
        }

        if (spatialIndex.IsExpired(item)) return 0f;

        if (item.Velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Bounce)
        {
            BounceMe(item, otherBounds, hitPrediction.ColliderHit, expectedTravelDistance);
        }
        else if (item.Velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Stop)
        {
            item.Velocity.Stop();
        }

        return distanceToContact;
    }

    private void BounceMe(GameCollider item, RectF otherBounds, ICollidable other, float expectedTravelDistance)
    {
        // We should already be at the "contact" position if ProcessCollision moved us.
        // But compute remaining defensively in case this is called from elsewhere.
        var distanceToContact = Math.Max(0f, Math.Min(hitPrediction.LKGD, expectedTravelDistance) - .1f);
        var remaining = expectedTravelDistance - distanceToContact;
        if (remaining <= 0f)
        {
            // Still update angle so next tick goes the reflected direction.
            item.Velocity.Angle = ComputeBounceAngle(item.Velocity, otherBounds, hitPrediction);
            return;
        }

        var newAngle = ComputeBounceAngle(item.Velocity, otherBounds, hitPrediction);
        item.Velocity.Angle = newAngle;

        // Spend the remaining distance along the new angle.
        var adjustedBounds = item.Bounds.RadialOffset(item.Velocity.Angle, remaining, false);
        if (TryMoveIfWouldNotCauseTouching(item, adjustedBounds, RGB.Orange)) return;

        // Fallback: try a tiny nudge to get unstuck (also based on remaining travel)
        adjustedBounds = item.Bounds.RadialOffset(item.Velocity.Angle, Math.Min(remaining, CollisionDetector.VerySmallNumber), false);
        if (TryMoveIfWouldNotCauseTouching(item, adjustedBounds, RGB.Orange.Darker)) return;

        // Last resort: pick a nearby angle and try a tiny move
        var saveMeAngle = item.Bounds.Center.CalculateAngleTo(hitPrediction.Intersection).Opposite();
#if DEBUG
    ColliderGroupDebugger.VelocityEventOccurred?.Fire(new AngleChange() { MovingObject = item, From = item.Velocity.Angle, To = saveMeAngle, NowSeconds = now, });
#endif
        item.Velocity.Angle = FindFreeAngle(item, saveMeAngle);

        if (other is GameCollider collider2)
        {
            var otherAngle = collider2.CalculateAngleTo(item.Bounds).Opposite();
#if DEBUG
        ColliderGroupDebugger.VelocityEventOccurred?.Fire(new AngleChange() { MovingObject = collider2, From = collider2.Velocity.Angle, To = otherAngle, NowSeconds = now, });
#endif
            collider2.Velocity.Angle = otherAngle;
        }

        adjustedBounds = item.Bounds.RadialOffset(item.Velocity.Angle, CollisionDetector.VerySmallNumber, false);
        TryMoveIfWouldNotCauseTouching(item, adjustedBounds, RGB.Orange.Darker);
    }

    public static Angle ComputeBounceAngle(Velocity v, RectF otherBounds, CollisionPrediction hitPrediction)
    {
        // Convert velocity to Cartesian components
        float velocityX = v.Speed * MathF.Cos(v.Angle.ToRadians());
        float velocityY = v.Speed * MathF.Sin(v.Angle.ToRadians());

        // Determine the normal vector based on the edge hit
        (float normalX, float normalY) = hitPrediction.Edge switch
        {
            var edge when edge == otherBounds.TopEdge => (0, -1),
            var edge when edge == otherBounds.BottomEdge => (0, 1),
            var edge when edge == otherBounds.LeftEdge => (-1, 0),
            var edge when edge == otherBounds.RightEdge => (1, 0),
            _ => throw new InvalidOperationException("Unknown edge hit")
        };

        // Reflect velocity vector over the normal
        float dotProduct = (velocityX * normalX + velocityY * normalY) * 2;
        float reflectedX = velocityX - dotProduct * normalX;
        float reflectedY = velocityY - dotProduct * normalY;

        // Convert reflected velocity back to polar coordinates
        float newAngleRadians = MathF.Atan2(reflectedY, reflectedX);
        var newAngleDegrees = Angle.FromRadians(newAngleRadians);
        return newAngleDegrees;
    }

    private Random r = new Random();
    private Angle FindFreeAngle(GameCollider item, Angle priority)
    {
        return priority.Add(r.Next(-45,45));
        /*
        foreach (var angle in Angle.Enumerate360Angles(priority,30))
        {
            for (var j = 0; j < colliderBuffer.Length; j++)
            {
                if (colliderBuffer[j] == item) continue;
                var prediction = CollisionDetector.Predict(item, angle, colliderBuffer, .5f, CastingMode.Precise, numColliders, new CollisionPrediction());
                hitPrediction.ColliderHit = hitPrediction.ColliderHit is VelocityHashTable.Item vItem ? vItem.Collider : hitPrediction.ColliderHit;
                if (prediction.CollisionPredicted == false) return angle;
            }
        }
        return priority;
        */
    }

    private float CalculateExpectedTravelDistance(GameCollider collider)
    {
        var elapsed = Math.Max(0f, now - collider.lastEvalTime);
        var initialDt = elapsed * SpeedRatio * collider.Velocity.SpeedRatio;
        collider.lastEvalTime = now;
        var maxDt = MaxDTSeconds * SpeedRatio * collider.Velocity.SpeedRatio;
        var timeElapsedSinceLastEval = stopwatch.SupportsMaxDT ? Math.Min(maxDt, initialDt) : initialDt;
        var expectedTravelDistance = ConsoleMath.NormalizeQuantity(collider.Velocity.Speed * timeElapsedSinceLastEval, collider.Velocity.Angle);
        return expectedTravelDistance;
    }

    private bool IsReadyToMove(GameCollider item)
    {
        if (spatialIndex.IsExpired(item)) return false;
        var velocity = item.Velocity;
        velocity.ApplyInfluences();
        var isReadyToMove = !(spatialIndex.IsExpired(item) || velocity.Speed == 0 || now < item.MinNextEvalTime);
        return isReadyToMove;
    }
    

    public bool TryMoveIfWouldNotCauseTouching(GameCollider item, RectF proposedBounds, RGB color)
    {
        if (WouldCauseTouching(item, proposedBounds, out GameCollider preventer) == false)
        {
#if DEBUG
            ColliderGroupDebugger.VelocityEventOccurred?.Fire(new SuccessfulMove()
            {
                MovingObject = item,
                Angle = item.Velocity.Angle,
                From = item.Bounds,
                To = proposedBounds,
                NowSeconds = now,
            });
#endif
            item.MoveTo(proposedBounds.Left, proposedBounds.Top);
            return true;
        }

#if DEBUG
        ColliderGroupDebugger.VelocityEventOccurred?.Fire(new FailedMove()
        {
            MovingObject = item,
            Obstacle = preventer,
            Angle = item.Velocity.Angle,
            From = item.Bounds,
            To = proposedBounds,
            NowSeconds = now
        });
#endif
        return false;
    }

    private bool WouldCauseTouching(GameCollider item, RectF proposed, out GameCollider preventer)
    {
        var swept = item.Bounds.SweptAABB(proposed).Grow(2f);
        queryBuffer.WriteableBuffer.Clear();
        spatialIndex.Query(swept, queryBuffer);
        var list = queryBuffer.WriteableBuffer;

        for (int i = 0; i < list.Count; i++)
        {
            var obstacle = list[i];
            if (obstacle == null || ReferenceEquals(obstacle, item) || spatialIndex.IsExpired(obstacle) || !CollidableFast.CanCollideFast(item, obstacle)) continue;

            var distance = obstacle.Bounds.CalculateDistanceTo(proposed);
            if (Math.Abs(distance) == 0)
            {
                preventer = obstacle;
                return true;
            }         
        }
        preventer = null;
        return false;
    }
 

    public void GetObstacles(GameCollider owner, ObstacleBuffer buffer)
    {
        spatialIndex.QueryExcept(buffer, owner);

        // backward loop to filter out obstacles that we can't collide with
        for (int i = buffer.WriteableBuffer.Count - 1; i >= 0;i--)
        {
            var other = buffer.WriteableBuffer[i];
            if(other.CanCollideWith(owner) == false || owner.CanCollideWith(other) == false)
            {
                buffer.WriteableBuffer.RemoveAt(i);
            }
        }
    }
}

public class ObstacleBuffer : Recyclable
{
    private List<GameCollider> _buffer = new List<GameCollider>();
    public IEnumerable<GameCollider> ReadableBuffer => _buffer;

    public List<GameCollider> WriteableBuffer => _buffer;

    protected override void OnInit()
    {
        base.OnInit();
        _buffer.Clear();
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _buffer.Clear();
    }

    public void FilterNonTouching(RectF testBounds, float tolerance = 0)
    {
        for (var i = _buffer.Count - 1; i >= 0; i--)
        {
            if (_buffer[i].CalculateNormalizedDistanceTo(testBounds) > tolerance)
            {
                _buffer.RemoveAt(i);
            }
        }
    }
}

internal static class CollidableFast
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool CanCollideFast<TLeft, TRight>(TLeft left, TRight right)
        where TLeft : ICollidable
        where TRight : ICollidable
        // NB: left ↔ right symmetry kept, exactly matches original semantics
        => left.CanCollideWith(right) && right.CanCollideWith(left);
}
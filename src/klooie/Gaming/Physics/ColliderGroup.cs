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
    

  
    public float SpeedRatio { get; set; } = 1;

    internal PauseManager? PauseManager { get; set; }

    public ColliderGroup(ILifetime lt, IStopwatch stopwatch = null)
    {
        this.lt = lt;
        lt.OnDisposed(() => this.lt = null);
        hitPrediction = new CollisionPrediction();
        this.stopwatch = stopwatch ?? new WallClockStopwatch();
        colliderBuffer = ObstacleBufferPool.Instance.Rent();
        lastExecuteTime = TimeSpan.Zero;
        spatialIndex = new UniformGrid();
        this.stopwatch.Start();
        Game.Current?.AfterPaint?.Subscribe(Tick, lt);
        ConsoleApp.Current?.OnDisposed(Cleanup);
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


    public TimeSpan Now => stopwatch.Elapsed;
    private IStopwatch stopwatch;

    public void Tick()
    {
        /*
        if(PauseManager?.IsPaused == true)
        {
            stopwatch.Stop();
            return;
        }
        else if(stopwatch.IsRunning)
        {
            stopwatch.Start();
        }
        */
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
        var nowTime = Now;
        now = (float)nowTime.TotalSeconds;
        var stopwatchDt = (float)(nowTime - lastExecuteTime).TotalMilliseconds;
        LatestDT = stopwatch.SupportsMaxDT ? Math.Min(MaxDTMilliseconds, stopwatchDt) : stopwatchDt;
        lastExecuteTime = nowTime;
    }

    private void Tick(GameCollider item)
    {
        if (spatialIndex.IsExpired(item)) return;
        if (IsReadyToMove(item) == false) return;

        var expectedTravelDistance = CalculateExpectedTravelDistance(item);
        if(TryDetectCollision(item, expectedTravelDistance))
        {
            ProcessCollision(item, expectedTravelDistance);
            item.Velocity?._afterEvaluate?.Fire(new Velocity.MoveEval(Velocity.MoveEvalResult.Collision, 0));
        }
        else
        {
            MoveColliderWithoutCollision(item, expectedTravelDistance);
            item.Velocity?._afterEvaluate?.Fire(new Velocity.MoveEval(Velocity.MoveEvalResult.Moved, expectedTravelDistance)); // could have expired during move
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

    private void ProcessCollision(GameCollider item, float expectedTravelDistance)
    {
        var encroachment = GetCloseToColliderWeAreCollidingWith(item);


        var otherBounds = hitPrediction.ColliderHit.Bounds;

        if (spatialIndex.IsExpired(item)) return;
        var collision = CollisionPool.Instance.Rent();
        try
        {
            collision.Bind(item.Velocity.speed, item.Velocity.angle, item, hitPrediction.ColliderHit, hitPrediction);
            OnCollision.Fire(collision);
            item.Velocity?._onCollision?.Fire(collision);
            if (spatialIndex.IsExpired(item)) return;
        }
        finally
        {
            collision.Dispose();
        }
        if (spatialIndex.IsExpired(item)) return;
        if (item.Velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Bounce)
        {
            BounceMe(item, otherBounds, hitPrediction.ColliderHit, expectedTravelDistance, encroachment);
        }
        else if (item.Velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Stop)
        {
            item.Velocity.Stop();
        }
    }
     
    private void BounceMe(GameCollider item, RectF otherBounds, ICollidable other, float expectedTravelDistance, float encroachment)
    {
        Angle newAngleDegrees = ComputeBounceAngle(item.Velocity, otherBounds, hitPrediction);
        item.Velocity.Angle = newAngleDegrees;

        var adjustedBounds = item.Bounds.RadialOffset(item.Velocity.Angle, encroachment == 0 ? expectedTravelDistance : encroachment * 2, false);
        if (TryMoveIfWouldNotCauseTouching(item, adjustedBounds, RGB.Orange) == false)
        {
            adjustedBounds = item.Bounds.RadialOffset(item.Velocity.Angle, CollisionDetector.VerySmallNumber, false);
            if (TryMoveIfWouldNotCauseTouching(item, adjustedBounds, RGB.Orange.Darker) == false)
            {
                var saveMeAngle = item.Bounds.Center.CalculateAngleTo(hitPrediction.Intersection).Opposite();

#if DEBUG
                ColliderGroupDebugger.VelocityEventOccurred?.Fire(new AngleChange()
                {
                    MovingObject = item,
                    From = item.Velocity.Angle,
                    To = saveMeAngle,
                    NowSeconds = now,
                });
#endif

                item.Velocity.Angle = FindFreeAngle(item, saveMeAngle);
                if (other is GameCollider collider2)
                {
                    var otherAngle = collider2.CalculateAngleTo(item.Bounds).Opposite();
#if DEBUG
                    ColliderGroupDebugger.VelocityEventOccurred?.Fire(new AngleChange()
                    {
                        MovingObject = collider2,
                        From = collider2.Velocity.Angle,
                        To = otherAngle,
                        NowSeconds = now,
                    });
#endif
                    collider2.Velocity.Angle = otherAngle;
                }
                adjustedBounds = item.Bounds.RadialOffset(item.Velocity.Angle, CollisionDetector.VerySmallNumber, false);
                TryMoveIfWouldNotCauseTouching(item, adjustedBounds, RGB.Orange.Darker);
            }
        }
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
        var initialDt = (now - collider.lastEvalTime) * SpeedRatio * collider.Velocity.SpeedRatio;
        collider.lastEvalTime = now;
        var timeElapsedSinceLastEval = stopwatch.SupportsMaxDT ? Math.Min(MaxDTSeconds * SpeedRatio * collider.Velocity.SpeedRatio, initialDt) : initialDt;
        var expectedTravelDistance = ConsoleMath.NormalizeQuantity(collider.Velocity.Speed * timeElapsedSinceLastEval, collider.Velocity.Angle);
        return expectedTravelDistance;
    }

    private bool IsReadyToMove(GameCollider item)
    {
        if (spatialIndex.IsExpired(item)) return false;
        var velocity = item.Velocity;
        velocity._beforeEvaluate?.Fire();
        var isReadyToMove = !(spatialIndex.IsExpired(item) || velocity.Speed == 0 || now < item.MinNextEvalTime);
        return isReadyToMove;
    }
    

    private float GetCloseToColliderWeAreCollidingWith(GameCollider item)
    {
        var proposedBounds = item.Bounds.RadialOffset(item.Velocity.Angle, hitPrediction.LKGD, false);
        var encroachment = TryMoveIfWouldNotCauseTouching(item, proposedBounds, RGB.Green) ? hitPrediction.LKGD : 0;
        return encroachment;
    }

    private bool TryMoveIfWouldNotCauseTouching(GameCollider item, RectF proposedBounds, RGB color)
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
        var swept = item.Bounds.SweptAABB(proposed).Grow(.01f);
        queryBuffer.WriteableBuffer.Clear();
        spatialIndex.Query(swept, queryBuffer);
        var list = queryBuffer.WriteableBuffer;

        for (int i = 0; i < list.Count; i++)
        {
            var obstacle = list[i] as GameCollider;
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
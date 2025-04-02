namespace klooie.Gaming;
public sealed class ColliderGroup
{
    private const float MaxDTSeconds = .05f;
    private const float MaxDTMilliseconds = MaxDTSeconds * 1000f;
    private FrameRateMeter frameRateMeter = new FrameRateMeter();
    public int FramesPerSecond => frameRateMeter.CurrentFPS;

    private int NextHashCode = 0;

    private Event<Collision>? onCollision;
    public Event<Collision> OnCollision => onCollision ?? (onCollision = EventPool<Collision>.Instance.Rent());
    public int Count { get; private set; }
    private VelocityHashTable velocities;
    public float LatestDT { get; private set; }

    // these properties model a linear progression that determines the appropriate min
    // evaluation time period for an object given it's current speed
    public const float LeastFrequentEval = .05f; // y1
    public const float LowestSpeedForEvalCalc = 0; // x1
    public const float MostFrequentEval = .002f; // y2
    public const float HighestSpeedForEvalCalc = 60; // x2
    public const float EvalFrequencySlope = (MostFrequentEval - LeastFrequentEval) / (HighestSpeedForEvalCalc - LowestSpeedForEvalCalc);

    private VelocityHashTable.Item[] colliderBuffer;
    private CollisionPrediction hitPrediction;
    private ILifetime? lt;
    private TimeSpan lastExecuteTime;
    private float now;
    private int numColliders;
    
    private Event<GameCollider> _added;
    public Event<GameCollider> Added { get => _added ?? (_added = EventPool<GameCollider>.Instance.Rent()); }

    private Event<GameCollider> _removed;
    public Event<GameCollider> Removed { get => _removed ?? (_removed = EventPool<GameCollider>.Instance.Rent()); }
    
    public float SpeedRatio { get; set; } = 1;

    internal PauseManager? PauseManager { get; set; }

    public ColliderGroup(ILifetime lt, IStopwatch stopwatch = null)
    {
        this.lt = lt;
        lt.OnDisposed(() => this.lt = null);
        hitPrediction = new CollisionPrediction();
        this.stopwatch = stopwatch ?? new WallClockStopwatch();
        velocities = new VelocityHashTable();
        colliderBuffer = new VelocityHashTable.Item[100];
        lastExecuteTime = TimeSpan.Zero;
        ConsoleApp.Current?.Invoke(ExecuteAsync);
        ConsoleApp.Current?.OnDisposed(Cleanup);
    }

    private void Cleanup()
    {
        _added?.Dispose();
        _removed?.Dispose();
        onCollision?.Dispose();
        _added = null;
        _removed = null;
        onCollision = null;
    }

    internal void Add(GameCollider c)
    {
        if (c.ColliderHashCode >= 0)
        {
            throw new System.Exception("Already has a hashcode");
        }
        c.ColliderHashCode = NextHashCode++;
        if (Count == colliderBuffer.Length)
        {
            var tmp = colliderBuffer;
            colliderBuffer = new VelocityHashTable.Item[tmp.Length * 2];
            Array.Copy(tmp, colliderBuffer, tmp.Length);

        }
        c.Velocity.lastEvalTime = (float)lastExecuteTime.TotalSeconds;
        velocities.Add(c, c.Velocity);
        Count++;
        _added?.Fire(c);
    }

    internal bool Remove(GameCollider c)
    {
        if (velocities.Remove(c))
        {
            _removed?.Fire(c);
            Count--;
            return true;
        }
        return false;
    }

    public TimeSpan Now => stopwatch.Elapsed;
    private IStopwatch stopwatch;
    private async Task ExecuteAsync()
    {
        stopwatch.Start();
        while (lt != null)
        {
            await Task.Yield();
            if (PauseManager?.IsPaused == true)
            {
                stopwatch.Stop();
                while (PauseManager.IsPaused)
                {
                    await Task.Yield();
                }
                stopwatch.Start();
            }
            Tick();
        }
    }

    public void Tick()
    {
        UpdateTime();
        FillColliderBuffer();
        frameRateMeter.Increment();
        for (var i = 0; i < numColliders; i++)
        {
            Tick(colliderBuffer[i]);
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

    private void Tick(VelocityHashTable.Item item)
    {
        if (item.IsExpired) return;
        if (IsReadyToMove(item) == false) return;

        var expectedTravelDistance = CalculateExpectedTravelDistance(item.Velocity);
        if(TryDetectCollision(item, expectedTravelDistance))
        {
            ProcessCollision(item, expectedTravelDistance);
        }
        else
        {
            MoveColliderWithoutCollision(item, expectedTravelDistance);
        }
        item.Velocity?._onVelocityEnforced?.Fire();
    }

    private bool TryDetectCollision(VelocityHashTable.Item item, float expectedTravelDistance)
    {
        CollisionDetector.Predict(item.Collider, item.Velocity.Angle, colliderBuffer, expectedTravelDistance, CastingMode.Precise, numColliders, hitPrediction);
        hitPrediction.ColliderHit = hitPrediction.ColliderHit is VelocityHashTable.Item vItem ? vItem.Collider : hitPrediction.ColliderHit;
        item.Velocity.NextCollision = hitPrediction;
        return hitPrediction.CollisionPredicted;  
    }

    private void MoveColliderWithoutCollision(VelocityHashTable.Item item, float expectedTravelDistance)
    {
        var colliderBoundsBeforeMovement = item.Bounds;
        var newLocation = item.Bounds.RadialOffset(item.Velocity.Angle, expectedTravelDistance, false);

        if (WouldCauseTouching(item, newLocation, out GameCollider preventer))
        {
#if DEBUG
            ColliderGroupDebugger.VelocityEventOccurred?.Fire(new FailedMove()
            {
                MovingObject = item.Collider,
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
            MovingObject = item.Collider,
            Angle = item.Velocity.Angle,
            From = colliderBoundsBeforeMovement,
            To = newLocation,
            NowSeconds = now
        });
#endif

        item.Collider.MoveTo(newLocation.Left, newLocation.Top);
    }

    private void ProcessCollision(VelocityHashTable.Item item, float expectedTravelDistance)
    {
        var encroachment = GetCloseToColliderWeAreCollidingWith(item);


        var otherBounds = hitPrediction.ColliderHit.Bounds;

        if (item.IsExpired) return;
        var collision = CollisionPool.Instance.Rent();
        try
        {
            collision.Bind(item.Velocity.speed, item.Velocity.angle, item.Collider, hitPrediction.ColliderHit, hitPrediction);
            OnCollision.Fire(collision);
            item.Velocity?._onCollision?.Fire(collision);
            if (item.IsExpired) return;
        }
        finally
        {
            collision.Dispose();
        }
        if (item.IsExpired) return;
        if (item.Velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Bounce)
        {
            BounceMe(item, otherBounds, hitPrediction.ColliderHit, expectedTravelDistance, encroachment);
        }
        else if (item.Velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Stop)
        {
            item.Velocity.Stop();
        }
    }
     
    private void BounceMe(VelocityHashTable.Item item, RectF otherBounds, ICollidable other, float expectedTravelDistance, float encroachment)
    {
        Angle newAngleDegrees = ComputeBounceAngle(item.Velocity, otherBounds, hitPrediction);
        item.Velocity.Angle = newAngleDegrees;

        var adjustedBounds = item.Velocity.Collider.Bounds.RadialOffset(item.Velocity.Angle, encroachment == 0 ? expectedTravelDistance : encroachment * 2, false);
        if (TryMoveIfWouldNotCauseTouching(item, adjustedBounds, RGB.Orange) == false)
        {
            adjustedBounds = item.Bounds.RadialOffset(item.Velocity.Angle, CollisionDetector.VerySmallNumber, false);
            if (TryMoveIfWouldNotCauseTouching(item, adjustedBounds, RGB.Orange.Darker) == false)
            {
                var saveMeAngle = item.Bounds.Center.CalculateAngleTo(hitPrediction.Intersection).Opposite();

#if DEBUG
                ColliderGroupDebugger.VelocityEventOccurred?.Fire(new AngleChange()
                {
                    MovingObject = item.Collider,
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
                adjustedBounds = item.Velocity.Collider.Bounds.RadialOffset(item.Velocity.Angle, CollisionDetector.VerySmallNumber, false);
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
    private Angle FindFreeAngle(VelocityHashTable.Item item, Angle priority)
    {
        return priority.Add(r.Next(-45,45));
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
    }

    private float CalculateExpectedTravelDistance(Velocity velocity)
    {
        var initialDt = (now - velocity.lastEvalTime) * SpeedRatio * velocity.SpeedRatio;
        velocity.lastEvalTime = now;
        var timeElapsedSinceLastEval = stopwatch.SupportsMaxDT ? Math.Min(MaxDTSeconds * SpeedRatio * velocity.SpeedRatio, initialDt) : initialDt;
        var expectedTravelDistance = ConsoleMath.NormalizeQuantity(velocity.Speed * timeElapsedSinceLastEval, velocity.Angle);
        return expectedTravelDistance;
    }

    private bool IsReadyToMove(VelocityHashTable.Item item)
    {
        if (item.IsExpired) return false;
        var velocity = item.Velocity;
        velocity._beforeEvaluate?.Fire();
        var isReadyToMove = !(item.IsExpired || velocity.Speed == 0 || now < velocity.MinEvalSeconds);

        if(isReadyToMove)
        {
            velocity._beforeMove?.Fire();
            if (item.IsExpired) isReadyToMove = false;
        }
        return isReadyToMove;
    }
    

    private float GetCloseToColliderWeAreCollidingWith(VelocityHashTable.Item item)
    {
        var proposedBounds = item.Bounds.RadialOffset(item.Velocity.Angle, hitPrediction.LKGD, false);
        var encroachment = TryMoveIfWouldNotCauseTouching(item, proposedBounds, RGB.Green) ? hitPrediction.LKGD : 0;
        return encroachment;
    }

    private bool TryMoveIfWouldNotCauseTouching(VelocityHashTable.Item item, RectF proposedBounds, RGB color)
    {
        if (WouldCauseTouching(item, proposedBounds, out GameCollider preventer) == false)
        {
#if DEBUG
            ColliderGroupDebugger.VelocityEventOccurred?.Fire(new SuccessfulMove()
            {
                MovingObject = item.Collider,
                Angle = item.Collider.Velocity.Angle,
                From = item.Collider.Bounds,
                To = proposedBounds,
                NowSeconds = now,
            });
#endif
            item.Collider.MoveTo(proposedBounds.Left, proposedBounds.Top);
            return true;
        }

#if DEBUG
        ColliderGroupDebugger.VelocityEventOccurred?.Fire(new FailedMove()
        {
            MovingObject = item.Collider,
            Obstacle = preventer,
            Angle = item.Collider.Velocity.Angle,
            From = item.Collider.Bounds,
            To = proposedBounds,
            NowSeconds = now
        });
#endif
        return false;
    }

    private bool WouldCauseTouching(VelocityHashTable.Item item, RectF proposed, out GameCollider preventer)
    {
        for (var i = 0; i < numColliders; i++)
        {
            var obstacle = colliderBuffer[i];
            if (obstacle == item || obstacle.IsExpired || obstacle.CanCollideWith(item) == false || item.CanCollideWith(obstacle) == false) continue;
            var distance = colliderBuffer[i].Bounds.CalculateDistanceTo(proposed);
            if (Math.Abs(distance) == 0)
            {
                preventer = obstacle.Collider;
                return true;
            }
        }
        preventer = null;
        return false;
    }
    private void FillColliderBuffer()
    {
        var ret = 0;
        var colliderBufferSpan = colliderBuffer;
        var span = velocities.Table;
        for (var i = 0; i < span.Length; i++)
        {
            var entry = span[i];
            for (var j = 0; j < entry.Length; j++)
            {
                var item = entry[j];
                if (item == null) continue;
                if (item.IsExpired) continue;
                colliderBufferSpan[ret] = item;
                ret++;
            }
        }
        numColliders = ret;
    }

    public IEnumerable<GameCollider> EnumerateCollidersSlow(List<GameCollider> list)
    {
        list = list ?? new List<GameCollider>(Count);
        var span = velocities.Table;
        for (var i = 0; i < span.Length; i++)
        {
            var entry = span[i];
            for (var j = 0; j < entry.Length; j++)
            {
                var item = entry[j]?.Collider;
                if (item == null) continue;
                list.Add(item);
            }
        }
        return list;
    }
 

    public void GetObstacles(GameCollider owner, ObstacleBuffer buffer)
    {
        var table = velocities.Table;
        for (var i = 0; i < table.Length; i++)
        {
            var entry = table[i];
            for (var j = 0; j < entry.Length; j++)
            {
                var item = entry[j];
                var collider = entry[j]?.Collider;
                if (collider == null || collider == owner || item.IsExpired || owner.CanCollideWith(collider) == false || collider.CanCollideWith(owner) == false) continue;
                buffer.WriteableBuffer.Add(collider);
            }
        }
    }

    private sealed class VelocityHashTable
    {
        public sealed class Item : ICollidable
        {
            public GameCollider Collider;
            public Velocity Velocity;

            private int colliderLease;
            private int velocityLease;

            public void Bind(GameCollider c, Velocity v)
            {
                Collider = c;
                Velocity = v;
                colliderLease = c.Lease;
                velocityLease = v.Lease;
            }

            public bool IsColliderStillValid => Collider?.IsStillValid(colliderLease) == true;
            public bool IsVelocityStillValid => Velocity?.IsStillValid(velocityLease) == true;

            public bool IsStillValid => IsColliderStillValid && IsVelocityStillValid;
            public bool IsExpired => !IsStillValid;

            public RectF Bounds => Collider.Bounds;

            public bool CanCollideWith(ICollidable other) => IsStillValid ? Collider.CanCollideWith(other) : false;
        }

        public static class ItemPool
        {
#if DEBUG
    public static int Created { get; private set; }
    public static int Rented { get; private set; }
    public static int Returned { get; private set; }
    public static int AllocationsSaved => Rented - Created;

#endif
            private static readonly List<Item> _pool = new List<Item>();

            internal static Item Rent(GameCollider c, Velocity v)
            {
#if DEBUG
        Rented++;
#endif
                if (_pool.Count > 0)
                {
                    var item = _pool[_pool.Count - 1];
                    _pool.RemoveAt(_pool.Count - 1);
                    item.Bind(c, v);
                    return item;
                }

#if DEBUG
        Created++;
#endif

                var ret = new Item();
                ret.Bind(c, v);
                return ret;
            }

            internal static void Return(Item item)
            {
#if DEBUG
        Returned++;
#endif
                item.Collider = null;
                item.Velocity = null;
                _pool.Add(item);
            }
        }

        public Item[][] Table;

        public VelocityHashTable()
        {
            Table = new Item[300][];
            var span = Table.AsSpan();
            for (var i = 0; i < span.Length; i++)
            {
                span[i] = new Item[4];
            }
        }

        internal void Add(GameCollider c, Velocity v)
        {
            var i = c.ColliderHashCode % Table.Length;
            var myArray = Table[i].AsSpan();
            for (var j = 0; j < myArray.Length; j++)
            {
                if (myArray[j] == null)
                {
                    myArray[j] = ItemPool.Rent(c, v);
                    return;
                }
            }
            var biggerArray = new Item[myArray.Length * 2];
            Array.Copy(Table[i], biggerArray, myArray.Length);
            biggerArray[myArray.Length] = ItemPool.Rent(c, v);
            Table[i] = biggerArray;
        }

        internal bool Remove(GameCollider c)
        {
            var i = c.ColliderHashCode % Table.Length;
            var myArray = Table[i].AsSpan();
            for (var j = 0; j < myArray.Length; j++)
            {
                if (ReferenceEquals(c, myArray[j]?.Collider))
                {
                    ItemPool.Return(myArray[j]);
                    myArray[j] = null;
                    for (var k = j; k < myArray.Length - 1; k++)
                    {
                        myArray[k] = myArray[k + 1];
                        myArray[k + 1] = null;
                        if (myArray[k] == null) break;
                    }
                    return true;
                }
            }
            return false;
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
        _buffer.Clear();
    }
}

namespace klooie.Gaming;
public sealed class ColliderGroup
{
    private const float MaxDTSeconds = .05f;
    private const float MaxDTMilliseconds = MaxDTSeconds * 1000f;
    private FrameRateMeter frameRateMeter = new FrameRateMeter();
    public int FramesPerSecond => frameRateMeter.CurrentFPS;

    private int NextHashCode = 0;
    public Event<Collision> OnCollision { get; private set; } = new Event<Collision>();
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

    private GameCollider[] colliderBuffer;
    private CollisionPrediction hitPrediction;
    private ILifetimeManager lt;
    private TimeSpan lastExecuteTime;
    private float now;
    private int numColliders;
    
    private Event<(Velocity Velocity, GameCollider Collider)> _added;
    public Event<(Velocity Velocity, GameCollider Collider)> Added { get => _added ?? (_added = new Event<(Velocity Velocity, GameCollider Collider)>()); }

    private Event<(Velocity Velocity, GameCollider Collider)> _removed;
    public Event<(Velocity Velocity, GameCollider Collider)> Removed { get => _removed ?? (_removed = new Event<(Velocity Velocity, GameCollider Collider)>()); }
    
    public float SpeedRatio { get; set; } = 1;

    internal PauseManager? PauseManager { get; set; }

    public ColliderGroup(ILifetimeManager lt, IStopwatch stopwatch = null)
    {
        this.lt = lt;
        hitPrediction = new CollisionPrediction();
        this.stopwatch = stopwatch ?? new WallClockStopwatch();
        velocities = new VelocityHashTable();
        colliderBuffer = new GameCollider[100];
        lastExecuteTime = TimeSpan.Zero;
        ConsoleApp.Current?.Invoke(ExecuteAsync);
    }

    public bool TryLookupVelocity(GameCollider c, out Velocity v) => velocities.TryGetValue(c, out v);

    internal (int RowIndex, int ColIndex) Add(GameCollider c, Velocity v)
    {
        if (c.ColliderHashCode >= 0)
        {
            throw new System.Exception("Already has a hashcode");
        }
        c.ColliderHashCode = NextHashCode++;
        if (Count == colliderBuffer.Length)
        {
            var tmp = colliderBuffer;
            colliderBuffer = new GameCollider[tmp.Length * 2];
            Array.Copy(tmp, colliderBuffer, tmp.Length);

        }
        v.lastEvalTime = (float)lastExecuteTime.TotalSeconds;
        var ret = velocities.Add(c, v);
        Count++;
        //_added?.Fire((v, c));
        return ret;
    }

    public bool Remove(GameCollider c)
    {
        if (velocities.Remove(c, out Velocity v))
        {
            //_removed?.Fire((v, c));
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
        while (lt.IsExpired == false)
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

    private void Tick(GameCollider collider)
    {
        if (IsReadyToMove(collider) == false) return;

        var expectedTravelDistance = CalculateExpectedTravelDistance(collider.Velocity);
        if(TryDetectCollision(collider, expectedTravelDistance))
        {
            ProcessCollision(collider, expectedTravelDistance);
        }
        else
        {
            MoveColliderWithoutCollision(collider, expectedTravelDistance);
        }
        collider.Velocity._onVelocityEnforced?.Fire();
    }

    private bool TryDetectCollision(GameCollider collider, float expectedTravelDistance)
    {
        CollisionDetector.Predict(collider, collider.Velocity.Angle, colliderBuffer, expectedTravelDistance, CastingMode.Precise, numColliders, hitPrediction);
        collider.Velocity.NextCollision = hitPrediction;
        return hitPrediction.CollisionPredicted;  
    }

    private void MoveColliderWithoutCollision(GameCollider collider, float expectedTravelDistance)
    {
        var colliderBoundsBeforeMovement = collider.Bounds;
        var newLocation = collider.Bounds.RadialOffset(collider.Velocity.Angle, expectedTravelDistance, false);

        if (WouldCauseTouching(collider, numColliders, newLocation, out GameCollider preventer))
        {
#if DEBUG
            ColliderGroupDebugger.VelocityEventOccurred?.Fire(new FailedMove()
            {
                MovingObject = collider,
                Obstacle = preventer,
                Angle = collider.Velocity.Angle,
                From = collider.Bounds,
                To = newLocation,
                NowSeconds = now
            });
#endif
            return;
        }

#if DEBUG
        ColliderGroupDebugger.VelocityEventOccurred?.Fire(new SuccessfulMove()
        {
            MovingObject = collider,
            Angle = collider.Velocity.Angle,
            From = colliderBoundsBeforeMovement,
            To = newLocation,
            NowSeconds = now
        });
#endif

        collider.Background = RGB.White;
        collider.MoveTo(newLocation.Left, newLocation.Top);
    }

    private void ProcessCollision(GameCollider collider, float expectedTravelDistance)
    {
        var encroachment = GetCloseToColliderWeAreCollidingWith(numColliders, collider.Velocity);

        collider.Velocity.LastCollision = new Collision()
        {
            MovingObjectSpeed = collider.Velocity.speed,
            Angle = collider.Velocity.Angle,
            MovingObject = collider,
            ColliderHit = hitPrediction.ColliderHit,
            Prediction = hitPrediction,
        };

        var otherBounds = hitPrediction.ColliderHit.Bounds;
        if (hitPrediction.ColliderHit is GameCollider otherGameCollider)
        {
            var vOther = otherGameCollider.Velocity;
            if (vOther.CollisionBehavior == Velocity.CollisionBehaviorMode.Bounce)
            {
                BounceOther(collider, expectedTravelDistance, vOther);
            }
            else if (vOther.CollisionBehavior == Velocity.CollisionBehaviorMode.Stop)
            {
                vOther.Stop();
            }

            vOther._onCollision?.Fire(new Collision()
            {
                MovingObjectSpeed = collider.Velocity.speed,
                Angle = collider.Velocity.Angle.Opposite(),
                MovingObject = hitPrediction.ColliderHit,
                ColliderHit = collider,
            });
        }

        collider.Velocity._onCollision?.Fire(collider.Velocity.LastCollision);
        OnCollision.Fire(collider.Velocity.LastCollision);

        if (collider.Velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Bounce)
        {
            BounceMe(collider, otherBounds, hitPrediction.ColliderHit, expectedTravelDistance, encroachment);
        }
        else if (collider.Velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Stop)
        {
            collider.Velocity.Stop();
        }
    }

    private void BounceOther(GameCollider me, float expectedTravelDistance, Velocity vOther)
    {
        var originalAngle = vOther.Angle;
        var newAngle = ComputeBounceAngle(me, vOther.Collider.Bounds).Opposite();
        vOther.Angle = newAngle;

#if DEBUG
        ColliderGroupDebugger.VelocityEventOccurred?.Fire(new AngleChange()
        {
            MovingObject = vOther.Collider,
            From = originalAngle,
            To = vOther.Angle,
            NowSeconds = now,
        });
#endif

        var adjustedBounds = vOther.Collider.Bounds.RadialOffset(vOther.Angle, expectedTravelDistance, false);
        if (TryMoveIfWouldNotCauseTouching(vOther, numColliders, adjustedBounds, RGB.Red) == false)
        {
            adjustedBounds = vOther.Collider.Bounds.RadialOffset(vOther.Angle, CollisionDetector.VerySmallNumber, false);
            if (TryMoveIfWouldNotCauseTouching(vOther, numColliders, adjustedBounds, RGB.DarkRed) == false)
            {
                var otherAngleChange = vOther.Collider.CalculateAngleTo(me).Opposite();
#if DEBUG
                ColliderGroupDebugger.VelocityEventOccurred?.Fire(new AngleChange()
                {
                    MovingObject = vOther.Collider,
                    From = vOther.Collider.Velocity.Angle,
                    To = otherAngleChange,
                    NowSeconds = now,
                });
#endif
                vOther.Angle = FindFreeAngle(vOther.Collider, otherAngleChange);
                adjustedBounds = vOther.Collider.Bounds.RadialOffset(vOther.Angle, CollisionDetector.VerySmallNumber, false);
                TryMoveIfWouldNotCauseTouching(vOther, numColliders, adjustedBounds, RGB.Orange.Darker);
            }
        }
    }

    private void BounceMe(GameCollider collider, RectF otherBounds, ICollidable other, float expectedTravelDistance, float encroachment)
    {
        Angle newAngleDegrees = ComputeBounceAngle(collider, otherBounds);
        collider.Velocity.Angle = newAngleDegrees;

        var adjustedBounds = collider.Velocity.Collider.Bounds.RadialOffset(collider.Velocity.Angle, encroachment == 0 ? expectedTravelDistance : encroachment * 2, false);
        if (TryMoveIfWouldNotCauseTouching(collider.Velocity, numColliders, adjustedBounds, RGB.Orange) == false)
        {
            adjustedBounds = collider.Velocity.Collider.Bounds.RadialOffset(collider.Velocity.Angle, CollisionDetector.VerySmallNumber, false);
            if (TryMoveIfWouldNotCauseTouching(collider.Velocity, numColliders, adjustedBounds, RGB.Orange.Darker) == false)
            {
                var saveMeAngle = collider.Center().CalculateAngleTo(hitPrediction.Intersection).Opposite();

#if DEBUG
                ColliderGroupDebugger.VelocityEventOccurred?.Fire(new AngleChange()
                {
                    MovingObject = collider,
                    From = collider.Velocity.Angle,
                    To = saveMeAngle,
                    NowSeconds = now,
                });
#endif

                collider.Velocity.Angle = FindFreeAngle(collider, saveMeAngle);
                if (other is GameCollider collider2)
                {
                    var otherAngle = collider2.CalculateAngleTo(collider.Bounds).Opposite();
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
                adjustedBounds = collider.Velocity.Collider.Bounds.RadialOffset(collider.Velocity.Angle, CollisionDetector.VerySmallNumber, false);
                TryMoveIfWouldNotCauseTouching(collider.Velocity, numColliders, adjustedBounds, RGB.Orange.Darker);
            }
        }
    }

    private Angle ComputeBounceAngle(GameCollider collider, RectF otherBounds)
    {
        // Convert velocity to Cartesian components
        float velocityX = collider.Velocity.Speed * MathF.Cos(collider.Velocity.Angle.ToRadians());
        float velocityY = collider.Velocity.Speed * MathF.Sin(collider.Velocity.Angle.ToRadians());

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
    private Angle FindFreeAngle(GameCollider collider, Angle priority)
    {
        return priority.Add(r.Next(-45,45));
        foreach (var angle in Angle.Enumerate360Angles(priority,30))
        {
            for (var j = 0; j < colliderBuffer.Length; j++)
            {
                if (colliderBuffer[j] == collider) continue;
                var prediction = CollisionDetector.Predict(collider, angle, colliderBuffer, .5f, CastingMode.Precise, numColliders, new CollisionPrediction());
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

    private bool IsReadyToMove(GameCollider collider)
    {
        var velocity = collider.Velocity;
        velocity._beforeEvaluate?.Fire();
        var isReadyToMove = !(velocity.ShouldStop || velocity.Speed == 0 || now < velocity.MinEvalSeconds);

        if(isReadyToMove)
        {
            velocity._beforeMove?.Fire();
            if (velocity.ShouldStop) isReadyToMove = false;
        }
        return isReadyToMove;
    }
    

    private float GetCloseToColliderWeAreCollidingWith(int numColliders, Velocity velocity)
    {
        var proposedBounds = velocity.Collider.Bounds.RadialOffset(velocity.Angle, hitPrediction.LKGD, false);
        var encroachment = TryMoveIfWouldNotCauseTouching(velocity, numColliders, proposedBounds, RGB.Green) ? hitPrediction.LKGD : 0;
        return encroachment;
    }

    private bool TryMoveIfWouldNotCauseTouching(Velocity item, int numColliders, RectF proposedBounds, RGB color)
    {
        if (WouldCauseTouching(item.Collider, numColliders, proposedBounds, out GameCollider preventer) == false)
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

    private bool WouldCauseTouching(ICollidable item, int bufferLength, RectF proposed, out GameCollider preventer)
    {
        for (var i = 0; i < bufferLength; i++)
        {
            var obstacle = colliderBuffer[i];
            if (obstacle == item) continue;
            var distance = colliderBuffer[i].Bounds.CalculateDistanceTo(proposed);
            if (Math.Abs(distance) == 0)
            {
                preventer = obstacle;
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
                var item = entry[j]?.Collider;
                if (item == null) continue;

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
                var item = entry[j]?.Collider;
                if (item == null || item == owner || owner.CanCollideWith(item) == false || item.CanCollideWith(owner) == false) continue;
                buffer.WriteableBuffer.Add(item);
            }
        }
    }

    private sealed class VelocityHashTable
    {
        public sealed class Item
        {
            public GameCollider Collider;
            public Velocity Velocity;

            public Item(GameCollider c, Velocity v)
            {
                Collider = c;
                Velocity = v;
            }
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
                    item.Collider = c;
                    item.Velocity = v;
                    return item;
                }

#if DEBUG
        Created++;
#endif

                return new Item(c, v);
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

        public void ValidateEntries()
        {
            for (var i = 0; i < Table.Length; i++)
            {
                for (var j = 0; j < Table[i].Length; j++)
                {
                    var entry = Table[i][j];
                    if (entry == null) continue;
                    var correctIndex = entry.Collider.ColliderHashCode % Table.Length;
                    if (correctIndex != i)
                    {
                        throw new System.Exception($"Item in the wrong place: Expected: {correctIndex}, Actual: {i}");
                    }
                }
            }
        }

        internal (int RowIndex, int ColIndex) Add(GameCollider c, Velocity v)
        {
            var i = c.ColliderHashCode % Table.Length;
            var myArray = Table[i].AsSpan();
            for (var j = 0; j < myArray.Length; j++)
            {
                if (myArray[j] == null)
                {
                    myArray[j] = ItemPool.Rent(c, v);
                    return (i, j);
                }
            }
            var biggerArray = new Item[myArray.Length * 2];
            Array.Copy(Table[i], biggerArray, myArray.Length);
            biggerArray[myArray.Length] = ItemPool.Rent(c, v);
            Table[i] = biggerArray;
            return (i, myArray.Length);
        }

        public bool Remove(GameCollider c, out Velocity v)
        {
            var i = c.ColliderHashCode % Table.Length;
            var myArray = Table[i].AsSpan();
            for (var j = 0; j < myArray.Length; j++)
            {
                if (ReferenceEquals(c, myArray[j]?.Collider))
                {
                    v = myArray[j].Velocity;
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
            v = null;
            return false;
        }

        public bool TryGetValue(GameCollider c, out Velocity v)
        {
            var i = c.ColliderHashCode % Table.Length;
            var myArray = Table[i].AsSpan();
            for (var j = 0; j < myArray.Length; j++)
            {
                var item = myArray[j];
                if (item == null)
                {
                    v = null;
                    return false;
                }

                if (ReferenceEquals(c, item.Collider))
                {
                    v = item.Velocity;
                    return true;
                }
            }
            v = null;
            return false;
        }
    }
}

public class ObstacleBuffer : Recyclable
{
    private List<GameCollider> _buffer = new List<GameCollider>();
    public IEnumerable<GameCollider> ReadableBuffer => _buffer;

    public List<GameCollider> WriteableBuffer => _buffer;

    protected override void ProtectedInit()
    {
        _buffer.Clear();
    }
}

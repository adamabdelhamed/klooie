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
    internal const float LeastFrequentEval = .05f; // y1
    internal const float LowestSpeedForEvalCalc = 0; // x1
    internal const float MostFrequentEval = .002f; // y2
    internal const float HighestSpeedForEvalCalc = 60; // x2
    internal const float EvalFrequencySlope = (MostFrequentEval - LeastFrequentEval) / (HighestSpeedForEvalCalc - LowestSpeedForEvalCalc);

    private GameCollider[] colliderBuffer;
    private RectF[] obstacleBuffer;
    private CollisionPrediction hitPrediction;
    private ILifetimeManager lt;
    private TimeSpan lastExecuteTime;


    private Event<(Velocity Velocity, GameCollider Collider)> _added;
    public Event<(Velocity Velocity, GameCollider Collider)> Added { get => _added ?? (_added = new Event<(Velocity Velocity, GameCollider Collider)>()); }

    private Event<(Velocity Velocity, GameCollider Collider)> _removed;
    public Event<(Velocity Velocity, GameCollider Collider)> Removed { get => _removed ?? (_removed = new Event<(Velocity Velocity, GameCollider Collider)>()); }

    public float SpeedRatio { get; set; } = 1;

    internal PauseManager PauseManager { get; set; }

    public ColliderGroup(ILifetimeManager lt, IStopwatch stopwatch = null)
    {
        this.lt = lt;
        hitPrediction = new CollisionPrediction();
        this.stopwatch = stopwatch ?? new WallClockStopwatch();
        velocities = new VelocityHashTable();
        colliderBuffer = new GameCollider[100];
        obstacleBuffer = new RectF[100];
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

            var tmp2 = obstacleBuffer;
            obstacleBuffer = new RectF[tmp.Length * 2];
            Array.Copy(tmp2, obstacleBuffer, tmp2.Length);
        }
        v.lastEvalTime = (float)lastExecuteTime.TotalSeconds;
        var ret = velocities.Add(c, v);
        Count++;
        _added?.Fire((v, c));
        return ret;
    }

    public bool Remove(GameCollider c)
    {
        if (velocities.Remove(c, out Velocity v))
        {
            _removed?.Fire((v, c));
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
        var nowTime = Now;
        var now = (float)nowTime.TotalSeconds;
        var stopwatchDt = (float)(nowTime - lastExecuteTime).TotalMilliseconds;
        LatestDT = stopwatch.SupportsMaxDT ? Math.Min(MaxDTMilliseconds, stopwatchDt) : stopwatchDt;
        lastExecuteTime = nowTime;
        var numColliders = CalcObstacles();
        var vSpan = velocities.Table.AsSpan();
        for (var i = 0; i < vSpan.Length; i++)
        {
            var entry = vSpan[i].AsSpan();
            for (var j = 0; j < entry.Length; j++)
            {
                var item = entry[j];
                // item is null if our sparse hashtable is empty in this spot or if the item has expired
                if (item == null || item.Velocity.ShouldStop) continue;

                // no need to evaluate this velocity if it's not moving
                var velocity = item.Velocity;
                item.Velocity._beforeEvaluate?.Fire();
                if (velocity.Speed <= 0)
                {
                    continue;
                }

                // Tick can happen very frequently, but velocities that are moving slowly don't
                // need to be evaluated as frequently. These next few lines will use a linear model to determine
                // the appropriate time to wait between evaluations, based on the object's speed
                if (now < velocity.MinEvalSeconds)
                {
                    continue;
                }
                var initialDt = ((float)now - velocity.lastEvalTime) * SpeedRatio * velocity.SpeedRatio;
                var dt = stopwatch.SupportsMaxDT ?  Math.Min(MaxDTSeconds, initialDt) : initialDt;
                velocity.lastEvalTime = now;

                // before moving the object, see if the movement would collide with another object
                float d = velocity.Speed * dt;
                d = ConsoleMath.NormalizeQuantity(d, velocity.Angle);
                CollisionDetector.Predict(velocity.Collider, obstacleBuffer, velocity.Angle, colliderBuffer, d, CastingMode.Precise, numColliders, hitPrediction);
                velocity.NextCollision = hitPrediction;
                velocity._beforeMove?.Fire();
                if (velocity.Collider.ShouldStop) continue;
                if (hitPrediction.CollisionPredicted)
                {
                    var obstacleHit = hitPrediction.ColliderHit;
                    var proposedBounds = velocity.Collider.Bounds.RadialOffset(velocity.Angle, hitPrediction.LKGD, false);
                    if (WouldCauseTouching(obstacleBuffer, numColliders, proposedBounds) == false)
                    {
                        item.Collider.TryMoveTo(proposedBounds.Left, proposedBounds.Top);
                    }

                    velocity.LastCollision = new Collision()
                    {
                        MovingObjectSpeed = velocity.speed,
                        Angle = velocity.Angle,
                        MovingObject = item.Collider,
                        ColliderHit = obstacleHit,
                        Prediction = hitPrediction,
                    };

                    if (obstacleHit is GameCollider && velocities.TryGetValue((GameCollider)obstacleHit, out Velocity vOther))
                    {
                        if (vOther.CollisionBehavior == Velocity.CollisionBehaviorMode.Bounce)
                        {
                            var topOrBottomEdgeWasHit = hitPrediction.Edge == obstacleHit.Bounds.TopEdge || hitPrediction.Edge == obstacleHit.Bounds.BottomEdge;
                            vOther.Angle = topOrBottomEdgeWasHit ? Angle.Right.Add(-vOther.Angle.Value) : Angle.Left.Add(-vOther.Angle.Value);
                        }

                        vOther._onCollision?.Fire(new Collision()
                        {
                            MovingObjectSpeed = velocity.speed,
                            Angle = velocity.Angle.Opposite(),
                            MovingObject = obstacleHit,
                            ColliderHit = item.Collider,
                        });
                    }

                    velocity._onCollision?.Fire(velocity.LastCollision);
                    OnCollision.Fire(velocity.LastCollision);


                    if (velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Bounce)
                    {
                        var topOrBottomEdgeWasHit = hitPrediction.Edge == obstacleHit.Bounds.TopEdge || hitPrediction.Edge == obstacleHit.Bounds.BottomEdge;
                        velocity.Angle = topOrBottomEdgeWasHit ? Angle.Right.Add(-velocity.Angle.Value) : Angle.Left.Add(-velocity.Angle.Value);
                    }
                    else if (velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Stop)
                    {
                        velocity.Stop();
                    }
                }
                else
                {
                    var newLocation = item.Collider.Bounds.RadialOffset(velocity.Angle, d, false);
                    item.Collider.TryMoveTo(newLocation.Left, newLocation.Top);
                }

                velocity._onVelocityEnforced?.Fire();
            }
        }
        frameRateMeter.Increment();
    }

    private bool WouldCauseTouching(RectF[] obstacleBuffer, int bufferLength, RectF proposed)
    {
        for(var i = 0; i < bufferLength; i++)
        {
            if (obstacleBuffer[i].CalculateDistanceTo(proposed) == 0) return true;
        }
        return false;
    }
    private int CalcObstacles()
    {
        var ret = 0;
        var colliderBufferSpan = colliderBuffer.AsSpan();
        var obstacleBufferSpan = obstacleBuffer.AsSpan();
        var span = velocities.Table.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var entry = span[i].AsSpan();
            for (var j = 0; j < entry.Length; j++)
            {
                var item = entry[j]?.Collider;
                if (item == null) continue;

                colliderBufferSpan[ret] = item;
                obstacleBufferSpan[ret] = item.Bounds;
                ret++;
            }
        }
        return ret;
    }

    public IEnumerable<GameCollider> EnumerateCollidersSlow(List<GameCollider> list)
    {
        list = list ?? new List<GameCollider>(Count);
        var span = velocities.Table.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var entry = span[i].AsSpan();
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
        var span = velocities.Table.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var entry = span[i].AsSpan();
            for (var j = 0; j < entry.Length; j++)
            {
                var item = entry[j]?.Collider;
                if (item == null || item == owner || owner.CanCollideWith(item) == false || item.CanCollideWith(owner) == false) continue;
                buffer.WriteableBuffer.Add(item);
            }
        }
    }

    private class VelocityHashTable
    {
        public class Item
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
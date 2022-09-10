using klooie.Gaming;
using System.Diagnostics;
namespace klooie;
public class Velocity
{
    public enum CollisionBehaviorMode
    {
        Bounce,
        Stop,
        DoNothing
    }

    internal bool haveMovedSinceLastHitDetection = true;
    internal Angle angle;
    internal float speed;
    internal float lastEvalTime;

    public ColliderGroup Group { get; private set; }


    internal Event _onAngleChanged, _onSpeedChanged, _beforeMove, _onVelocityEnforced, _beforeEvaluate;
    internal Event<Impact> _impactOccurred;
    public Event OnAngleChanged { get => _onAngleChanged ?? (_onAngleChanged = new Event()); }
    public Event OnSpeedChanged { get => _onSpeedChanged ?? (_onSpeedChanged = new Event()); }
    public Event BeforeEvaluate { get => _beforeEvaluate ?? (_beforeEvaluate = new Event()); }
    public Event BeforeMove { get => _beforeMove ?? (_beforeMove = new Event()); }
    public Event OnVelocityEnforced { get => _onVelocityEnforced ?? (_onVelocityEnforced = new Event()); }
    public Event<Impact> ImpactOccurred { get => _impactOccurred ?? (_impactOccurred = new Event<Impact>()); }

    public Impact LastImpact { get; internal set; }
    public CollisionBehaviorMode CollisionBehavior { get; set; } = Velocity.CollisionBehaviorMode.Stop;
    public HitPrediction NextCollision { get; internal set; }
    public GameCollider Collider { get; private set; }

    public float SpeedRatio { get; set; } = 1;

    public Angle Angle
    {
        get
        {
            return angle;
        }
        set
        {
            if (value == angle) return;
            angle = value;
            _onAngleChanged?.Fire();
        }
    }

    public float Speed
    {
        get
        {
            return speed;
        }
        set
        {
            if (value == speed) return;
            lastEvalTime = (float)Group.Now.TotalSeconds;
            speed = value;
            _onSpeedChanged?.Fire();
        }
    }
    public float MinEvalSeconds => this.lastEvalTime + EvalFrequencySeconds;
    public float EvalFrequencySeconds =>  (this.Speed > ColliderGroup.HighestSpeedForEvalCalc? .025f : ColliderGroup.EvalFrequencySlope* this.speed + ColliderGroup.LeastFrequentEval);

    public TimeSpan NextCollisionETA
    {
        get
        {
            if (NextCollision == null || Speed == 0 || NextCollision.Type == HitType.None) return TimeSpan.MaxValue;
            var d = NextCollision.LKGD;
            var seconds = d / speed;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public Velocity(GameCollider collider, ColliderGroup group)
    {
        this.Group = group;
        this.Collider = collider;
        if (collider is ColliderBox == false)
        {
            group.Add(collider, this);

            this.Group = group;
            this.Collider = collider;
            collider.OnDisposed(() =>
            {
                if (this.Group.Remove(Collider) == false)
                {
                    throw new InvalidOperationException($"Failed to remove myself from group after dispose: {collider.GetType().Name}-{collider.ColliderHashCode}");
                }
            });
        }
    }


    public ILifetimeManager CreateVelocityChangedLifetime() => 
        Lifetime.EarliestOf(OnSpeedChanged.CreateNextFireLifetime(), OnAngleChanged.CreateNextFireLifetime()).Manager;
    

    public IEnumerable<GameCollider> GetObstaclesSlow(List<GameCollider> buffer = null) => Group.GetObstaclesSlow(Collider, buffer);
    public void Stop() => Speed = 0;
}

public class ColliderGroup
{
    private int NextHashCode = 0;
    public Event<Impact> ImpactOccurred { get; private set; } = new Event<Impact>();
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

    private int colliderBufferLength;
    private GameCollider[] colliderBuffer;
    private RectF[] obstacleBuffer;
    private HitPrediction hitPrediction;
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
        hitPrediction = new HitPrediction();
        this.stopwatch = stopwatch ?? new WallClockStopwatch();
        velocities = new VelocityHashTable();
        colliderBufferLength = 0;
        colliderBuffer = new GameCollider[100];
        obstacleBuffer = new RectF[100];
        lastExecuteTime = TimeSpan.Zero;
        ConsoleApp.Current?.Invoke(ExecuteAsync);
    }

    public bool TryLookupVelocity(GameCollider c, out Velocity v) => velocities.TryGetValue(c, out v);

    internal (int RowIndex, int ColIndex) Add(GameCollider c, Velocity v)
    {
        if(c.ColliderHashCode >= 0)
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
        if(velocities.Remove(c, out Velocity v))
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

            if (PauseManager?.State == PauseManager.PauseState.Paused)
            {
                stopwatch.Stop();
                while (PauseManager.State == PauseManager.PauseState.Paused)
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
        LatestDT = (float)(nowTime - lastExecuteTime).TotalMilliseconds;
        lastExecuteTime = nowTime;
        CalcObstacles();
        var vSpan = velocities.Table.AsSpan();
        for (var i = 0; i < vSpan.Length; i++)
        {
            var entry = vSpan[i].AsSpan();
            for (var j = 0; j < entry.Length; j++)
            {
                var item = entry[j];
                // item is null if our sparse hashtable is empty in this spot
                if (item == null) continue;

                // no need to evaluate this velocity if it's not moving
                var velocity = item.Velocity;
                item.Velocity._beforeEvaluate?.Fire();
                if (velocity.Speed <= 0)
                {
                    velocity._onVelocityEnforced?.Fire();
                    continue;
                }

                // Tick can happen very frequently, but velocities that are moving slowly don't
                // need to be evaluated as frequently. These next few lines will use a linear model to determine
                // the appropriate time to wait between evaluations, based on the object's speed
                if (now < velocity.MinEvalSeconds)
                {
                    velocity._onVelocityEnforced?.Fire();
                    continue;
                }
                var dt = ((float)now - velocity.lastEvalTime) * SpeedRatio * velocity.SpeedRatio;
                velocity.lastEvalTime = now;

                // before moving the object, see if the movement would impact another object
                float d = velocity.Speed * dt;
                HitDetection.PredictHit(velocity.Collider, obstacleBuffer, velocity.Angle, colliderBuffer, 1.5f * d, CastingMode.Precise, colliderBufferLength, hitPrediction);
                velocity.NextCollision = hitPrediction;
                velocity._beforeMove?.Fire();

                if (hitPrediction.Type != HitType.None && hitPrediction.LKGD <= d)
                {
                    var obstacleHit = hitPrediction.ColliderHit;

                    var proposedBounds = item.Collider.Bounds;
                    var distanceToObstacleHit = proposedBounds.CalculateDistanceTo(obstacleHit.Bounds);
                   
                    proposedBounds = proposedBounds.OffsetByAngleAndDistance(velocity.Angle, distanceToObstacleHit - HitDetection.VerySmallNumber, false);
                    item.Collider.Bounds = new RectF(proposedBounds.Left, proposedBounds.Top, item.Collider.Width(), item.Collider.Height());
                    velocity.haveMovedSinceLastHitDetection = true;
                    
                    var angle = velocity.Collider.MassBounds.CalculateAngleTo(obstacleHit.MassBounds);

                    if (velocity.haveMovedSinceLastHitDetection)
                    {
                        velocity.LastImpact = new Impact()
                        {
                            MovingObjectSpeed = velocity.speed,
                            Angle = angle,
                            MovingObject = item.Collider,
                            ColliderHit = obstacleHit,
                            HitType = hitPrediction.Type,
                            Prediction = hitPrediction,
                        };

                        if (obstacleHit is GameCollider && velocities.TryGetValue((GameCollider)obstacleHit, out Velocity vOther))
                        {
                            if(vOther.CollisionBehavior == Velocity.CollisionBehaviorMode.Bounce)
                            {
                                var topOrBottomEdgeWasHit = hitPrediction.Edge == obstacleHit.Bounds.TopEdge || hitPrediction.Edge == obstacleHit.Bounds.BottomEdge;
                                vOther.Angle = topOrBottomEdgeWasHit ? Angle.Right.Add(-vOther.Angle.Value) : Angle.Left.Add(-vOther.Angle.Value);
                            }

                            vOther._impactOccurred?.Fire(new Impact()
                            {
                                MovingObjectSpeed = velocity.speed,
                                Angle = angle.Opposite(),
                                MovingObject = obstacleHit,
                                ColliderHit = item.Collider,
                                HitType = hitPrediction.Type,
                            });
                        }

                        velocity._impactOccurred?.Fire(velocity.LastImpact);
                        ImpactOccurred.Fire(velocity.LastImpact);
                        velocity.haveMovedSinceLastHitDetection = false;
                    }

                    if (velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Bounce)
                    {
                        var topOrBottomEdgeWasHit = hitPrediction.Edge == obstacleHit.Bounds.TopEdge || hitPrediction.Edge == obstacleHit.Bounds.BottomEdge;
                        velocity.Angle = topOrBottomEdgeWasHit ? Angle.Right.Add(-velocity.Angle.Value) : Angle.Left.Add(-velocity.Angle.Value);
                    }
                    else if(velocity.CollisionBehavior == Velocity.CollisionBehaviorMode.Stop)
                    {
                        velocity.Stop();
                    }
                }
                else
                {
                    var newLocation = item.Collider.Bounds.OffsetByAngleAndDistance(velocity.Angle, d);
                    item.Collider.Bounds = newLocation;
                    velocity.haveMovedSinceLastHitDetection = true;
                }

                velocity._onVelocityEnforced?.Fire();
            }
        }
    }
 
    private void CalcObstacles()
    {
        colliderBufferLength = 0;
        var colliderBufferSpan = colliderBuffer.AsSpan();
        var obstacleBufferSpan = obstacleBuffer.AsSpan();
        var span = velocities.Table.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var entry = span[i].AsSpan();
            for (var j = 0; j < entry.Length; j++)
            {
                var item = entry[j];
                if (item == null) continue;

                colliderBufferSpan[colliderBufferLength] = item.Collider;
                obstacleBufferSpan[colliderBufferLength] = item.Collider.Bounds;
                colliderBufferLength++;
            }
        }
    }

    public IEnumerable<GameCollider> EnumerateCollidersSlow(List<GameCollider> list)
    {
        list = list ?? new List<GameCollider>(Count);
        colliderBufferLength = 0;
        var span = velocities.Table.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var entry = span[i].AsSpan();
            for (var j = 0; j < entry.Length; j++)
            {
                var item = entry[j];
                if (item == null) continue;
                list.Add(item.Collider);
            }
        }
        return list;
    }

    public IEnumerable<GameCollider> EnumerateCollidersSlow(List<GameCollider> list, GameCollider except)
    {
        list = list ?? new List<GameCollider>(Count);
        colliderBufferLength = 0;
        var span = velocities.Table.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var entry = span[i].AsSpan();
            for (var j = 0; j < entry.Length; j++)
            {
                var item = entry[j];
                if (item == null || item.Collider == except) continue;
                list.Add(item.Collider);
            }
        }
        return list;
    }

    public IEnumerable<GameCollider> GetObstaclesSlow(GameCollider owner, List<GameCollider> list = null)
    {
        list = list ?? new List<GameCollider>(Count);
        colliderBufferLength = 0;
        var span = velocities.Table.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            var entry = span[i].AsSpan();
            for (var j = 0; j < entry.Length; j++)
            {
                var item = entry[j];

                if (item == null) continue;

                if (item.Collider == owner) continue;

                if (owner.CanCollideWith(item.Collider) == false) continue;

                list.Add(item.Collider);
            }
        }
        return list;
    }

    private class VelocityHashTable
    {
        public class Item
        {
            public readonly GameCollider Collider;
            public readonly Velocity Velocity;

            public Item(GameCollider c, Velocity v)
            {
                Collider = c;
                Velocity = v;
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
            for(var i = 0; i < Table.Length; i++)
            {
                for(var j = 0; j < Table[i].Length; j++)
                {
                    var entry = Table[i][j];
                    if (entry == null) continue;
                    var correctIndex = entry.Collider.ColliderHashCode % Table.Length;
                    if(correctIndex != i)
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
                    myArray[j] = new Item(c, v);
                    return (i,j);
                }
            }
            var biggerArray = new Item[myArray.Length * 2];
            Array.Copy(Table[i], biggerArray, myArray.Length);
            biggerArray[myArray.Length] = new Item(c, v);
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
                    myArray[j] = null;
                    for (var k = j; k < myArray.Length - 1; k++)
                    {
                        myArray[k] = myArray[k + 1];
                        myArray[k+1] = null;
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




public static class GameColliderEx
{
    public static float NumberOfPixelsThatOverlap(this GameCollider c, RectF other) => c.Bounds.NumberOfPixelsThatOverlap(other);
    public static float NumberOfPixelsThatOverlap(this GameCollider c, GameCollider other) => c.Bounds.NumberOfPixelsThatOverlap(other.Bounds);

    public static float OverlapPercentage(this GameCollider c, RectF other) => c.Bounds.OverlapPercentage(other);
    public static float OverlapPercentage(this GameCollider c, GameCollider other) => c.Bounds.OverlapPercentage(other.Bounds);

    public static bool Touches(this GameCollider c, RectF other) => c.Bounds.Touches(other);
    public static bool Touches(this GameCollider c, GameCollider other) => c.Bounds.Touches(other.Bounds);

    public static bool Contains(this GameCollider c, RectF other) => c.Bounds.Contains(other);
    public static bool Contains(this GameCollider c, GameCollider other) => c.Bounds.Contains(other.Bounds);

    public static float Top(this GameCollider c) => c.Bounds.Top;
    public static float Left(this GameCollider c) => c.Bounds.Left;

    public static float Bottom(this GameCollider c) => c.Bounds.Bottom;
    public static float Right(this GameCollider c) => c.Bounds.Right;

    public static float Width(this GameCollider c) => c.Bounds.Width;
    public static float Height(this GameCollider c) => c.Bounds.Height;

    public static LocF TopRight(this GameCollider c) => c.Bounds.TopRight;
    public static LocF BottomRight(this GameCollider c) => c.Bounds.BottomRight;
    public static LocF TopLeft(this GameCollider c) => c.Bounds.TopLeft;
    public static LocF BottomLeft(this GameCollider c) => c.Bounds.BottomLeft;

    public static LocF Center(this GameCollider c) => c.Bounds.Center;
    public static float CenterX(this GameCollider c) => c.Bounds.CenterX;
    public static float CenterY(this GameCollider c) => c.Bounds.CenterY;

    public static RectF Round(this GameCollider c) => c.Bounds.Round();

    public static RectF OffsetByAngleAndDistance(this GameCollider c, Angle a, float d, bool normalized = true) => c.Bounds.OffsetByAngleAndDistance(a, d, normalized);
    public static RectF Offset(this GameCollider c, float dx, float dy) => c.Bounds.Offset(dx, dy);

    public static Angle CalculateAngleTo(this GameCollider c, RectF other) => c.Bounds.CalculateAngleTo(other);
    public static Angle CalculateAngleTo(this GameCollider c, GameCollider other) => c.Bounds.CalculateAngleTo(other.Bounds);

    public static float CalculateDistanceTo(this GameCollider c, RectF other) => c.Bounds.CalculateDistanceTo(other);
    public static float CalculateDistanceTo(this GameCollider c, GameCollider other) => c.Bounds.CalculateDistanceTo(other.Bounds);

    public static float CalculateNormalizedDistanceTo(this GameCollider c, RectF other) => c.Bounds.CalculateNormalizedDistanceTo(other);
    public static float CalculateNormalizedDistanceTo(this GameCollider c, GameCollider other) => c.Bounds.CalculateNormalizedDistanceTo(other.Bounds);
}

public static class ColliderEx
{
    public static float CalculateDistanceTo(this RectF rect, GameCollider collider) =>
        rect.CalculateDistanceTo(collider.Left(), collider.Top(), collider.Width(), collider.Height());

    public static Angle CalculateAngleTo(this RectF rect, GameCollider collider) =>
        rect.CalculateAngleTo(collider.Left(), collider.Top(), collider.Width(), collider.Height());
}

public class ColliderBox : GameCollider
{
    public ColliderBox(RectF bounds) : base(bounds) { }

    public ColliderBox(float x, float y, float w, float h) : this(new RectF(x, y, w, h)) { }

}

public interface IStopwatch
{
    public TimeSpan Elapsed { get; }
    void Start();
    void Stop();
}

public class WallClockStopwatch : IStopwatch
{
    private Stopwatch sw = new Stopwatch();
    public TimeSpan Elapsed => sw.Elapsed;
    public void Start() => sw.Start();
    public void Stop() => sw.Stop();
}
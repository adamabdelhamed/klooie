using klooie.Gaming;
namespace klooie;
public sealed class Velocity : Recyclable
{
    public enum CollisionBehaviorMode
    {
        DoNothing,
        Bounce,
        Stop
    }

    public enum MoveEvalResult
    {
        Moved,
        Collision
    }
    public readonly struct MoveEval
    {
        public readonly MoveEvalResult Result;
        public readonly float DistanceMoved;
        public MoveEval(MoveEvalResult result, float distanceMoved)
        {
            Result = result;
            DistanceMoved = distanceMoved;
        }

        public override string ToString() => $"MoveEval(Result: {Result}, DistanceMoved: {DistanceMoved})";
    }

    internal Angle angle;
    internal float speed;
    internal Event _onAngleChanged, _onSpeedChanged;
    internal Event<MoveEval> _afterEvaluate;
    internal Event<Collision> _onCollision;

    private readonly List<MotionInfluence> _influences = new();

    public Event OnAngleChanged { get => _onAngleChanged ?? (_onAngleChanged = Event.Create()); }
    public Event OnSpeedChanged { get => _onSpeedChanged ?? (_onSpeedChanged = Event.Create()); }
    public Event<MoveEval> AfterEvaluate { get => _afterEvaluate ?? (_afterEvaluate = Event<MoveEval>.Create()); }
    public Event<Collision> OnCollision { get => _onCollision ?? (_onCollision = Event<Collision>.Create()); }
    public CollisionBehaviorMode CollisionBehavior { get; set; } = CollisionBehaviorMode.Stop;
    public CollisionPrediction NextCollision { get; internal set; }
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
            speed = value;
            _onSpeedChanged?.Fire();
        }
    }

    public TimeSpan NextCollisionETA
    {
        get
        {
            if (NextCollision == null || Speed == 0 || NextCollision.CollisionPredicted == false) return TimeSpan.MaxValue;
            var d = NextCollision.LKGD;
            var seconds = d / speed;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public int InfluenceCount => _influences?.Count ?? 0;

    public Velocity() { }

    protected override void OnInit()
    {
        base.OnInit();
        speed = 0;
        angle = 0;
        SpeedRatio = 1;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _influences.Clear();
        _onAngleChanged?.TryDispose();
        _onAngleChanged = null;
        _onSpeedChanged?.TryDispose();
        _onSpeedChanged = null;
        _afterEvaluate?.TryDispose();
        _afterEvaluate = null;
        _onCollision?.TryDispose();
        _onCollision = null;
        SpeedRatio = 1;
    }



    public void Stop() => Speed = 0;

    public void AddInfluence(MotionInfluence influence)
    {
        if(influence.IsExclusive && _influences.Contains(influence))
        {
            throw new InvalidOperationException($"Cannot add a second influence with name '{influence.Name}' because it is exclusive and already exists in the list.");
        }
        _influences.Add(influence);
    }

    public bool ContainsInfluence(MotionInfluence influence)
    {
        return _influences.Contains(influence);
    }



    public void RemoveInfluence(MotionInfluence influence)
    {
        if(this.IsStillValid(Lease) == false) throw new InvalidOperationException("Cannot remove influence from a Velocity that is no longer valid.");
        var removed = _influences.Remove(influence);
        if(removed == false) throw new InvalidOperationException($"Cannot remove influence with name '{influence.Name}' because it does not exist in the list.");
        if (_influences.Count > 0) return;

        Speed = 0; // If there are no influences left, then we should assume the owners would prefer to be stopped.
    }

    public void ApplyInfluences()
    {
        if (_influences.Count == 0) return;

        float x = 0, y = 0;
        for (int index = 0; index < _influences.Count; index++)
        {
            var influence = _influences[index];
            x += influence.DeltaSpeed * (float)Math.Cos(influence.Angle.ToRadians());
            y += influence.DeltaSpeed * (float)Math.Sin(influence.Angle.ToRadians());
        }
        float speed = MathF.Sqrt(x * x + y * y);

        if (speed > 0)
        {
            Speed = speed;
            Angle = Angle.FromRadians(MathF.Atan2(y, x));
        }
        else
        {
            // Average the angles
            float sumX = 0, sumY = 0;
            for (int i = 0; i < _influences.Count; i++)
            {
                float radians = _influences[i].Angle.ToRadians();
                sumX += (float)Math.Cos(radians);
                sumY += (float)Math.Sin(radians);
            }
            // Edge case: influences exactly cancel (sumX=sumY=0); fallback to first influence.
            if (sumX != 0 || sumY != 0)
            {
                Angle = Angle.FromRadians(MathF.Atan2(sumY, sumX));
            }
            else
            {
                Angle = _influences[0].Angle;
            }
            Speed = 0;
        }
    }


}

public class MotionInfluence : Recyclable, IEquatable<MotionInfluence>
{
    public float DeltaSpeed;
    public Angle Angle;
    public string Name;
    public bool IsExclusive;

    public bool Equals(MotionInfluence? other) => Name.Equals(other?.Name, StringComparison.OrdinalIgnoreCase);

    private static LazyPool<MotionInfluence> pool = new LazyPool<MotionInfluence>(() => new MotionInfluence());
    private MotionInfluence() { }
    public static MotionInfluence Create(string name, float deltaSpeed, Angle angle, bool isExclusive = false)
    {
        var ret = pool.Value.Rent();
        ret.Name = name ?? throw new ArgumentNullException(nameof(name));
        ret.DeltaSpeed = deltaSpeed;
        ret.Angle = angle;
        ret.IsExclusive = isExclusive;
        return ret;
    }

    public static MotionInfluence Create(string name, bool isExclusive = false)
    {
        var ret = pool.Value.Rent();
        ret.Name = name ?? throw new ArgumentNullException(nameof(name));
        ret.DeltaSpeed = 0;
        ret.Angle = Angle.Right;
        ret.IsExclusive = isExclusive;
        return ret;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Name = null;
        DeltaSpeed = 0;
        Angle = 0;
        IsExclusive = false;
    }
}
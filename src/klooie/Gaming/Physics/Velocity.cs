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

    internal Angle angle;
    internal float speed;
    internal float lastEvalTime;
    internal Event _onAngleChanged, _onSpeedChanged, _beforeMove, _onVelocityEnforced, _beforeEvaluate;
    internal Event<Collision> _onCollision;
 
    public ColliderGroup Group { get; private set; }
    public Event OnAngleChanged { get => _onAngleChanged ?? (_onAngleChanged = EventPool.Instance.Rent()); }
    public Event OnSpeedChanged { get => _onSpeedChanged ?? (_onSpeedChanged = EventPool.Instance.Rent()); }
    public Event BeforeEvaluate { get => _beforeEvaluate ?? (_beforeEvaluate = EventPool.Instance.Rent()); }
    public Event BeforeMove { get => _beforeMove ?? (_beforeMove = EventPool.Instance.Rent()); }
    public Event OnVelocityEnforced { get => _onVelocityEnforced ?? (_onVelocityEnforced = EventPool.Instance.Rent()); }
    public Event<Collision> OnCollision { get => _onCollision ?? (_onCollision = EventPool<Collision>.Instance.Rent()); }
    public CollisionBehaviorMode CollisionBehavior { get; set; } = CollisionBehaviorMode.Stop;
    public CollisionPrediction NextCollision { get; internal set; }
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
            if (NextCollision == null || Speed == 0 || NextCollision.CollisionPredicted == false) return TimeSpan.MaxValue;
            var d = NextCollision.LKGD;
            var seconds = d / speed;
            return TimeSpan.FromSeconds(seconds);
        }
    }

    public Velocity() { }

    protected override void OnInit()
    {
        base.OnInit();
        speed = 0;
        angle = 0;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _onAngleChanged?.Dispose();
        _onAngleChanged = null;
        _onSpeedChanged?.Dispose();
        _onSpeedChanged = null;
        _beforeEvaluate?.Dispose();
        _beforeEvaluate = null;
        _beforeMove?.Dispose();
        _beforeMove = null;
        _onVelocityEnforced?.Dispose();
        _onVelocityEnforced = null;
        _onCollision?.Dispose();
        _onCollision = null;
    }

    internal void Init(GameCollider collider, ColliderGroup group)
    {
        this.Group = group;
        this.Collider = collider;
        group.Add(collider);
        this.Group = group;
        this.Collider = collider;

        collider.OnDisposed(this, RemoveMyselfFromGroup);
    }

    private static void RemoveMyselfFromGroup(object me)
    {
        var _this = me as Velocity;
        if (_this.Group.Remove(_this.Collider) == false)
        {
            throw new InvalidOperationException($"Failed to remove myself from group after dispose: {_this.Collider.GetType().Name}-{_this.Collider.ColliderHashCode}");
        }
    }

    public void GetObstacles(ObstacleBuffer buffer) => Group.GetObstacles(Collider, buffer);

    public IEnumerable<GameCollider> GetObstacles()
    {
        var buffer = ObstacleBufferPool.Instance.Rent();
        try
        {
            GetObstacles(buffer);
            return buffer.ReadableBuffer.ToArray();
        }
        finally
        {
            buffer.Dispose();
        }
    }

    public void Stop() => Speed = 0;
}

 
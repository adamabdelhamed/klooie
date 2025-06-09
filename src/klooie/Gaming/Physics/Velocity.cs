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

    private readonly List<MotionInfluence> _influences = new();
    private Recyclable influenceSubscriptionLifetime;

    public ColliderGroup Group { get; private set; }
    public Event OnAngleChanged { get => _onAngleChanged ?? (_onAngleChanged = Event.Create()); }
    public Event OnSpeedChanged { get => _onSpeedChanged ?? (_onSpeedChanged = Event.Create()); }
    public Event BeforeEvaluate { get => _beforeEvaluate ?? (_beforeEvaluate = Event.Create()); }
    public Event BeforeMove { get => _beforeMove ?? (_beforeMove = Event.Create()); }
    public Event OnVelocityEnforced { get => _onVelocityEnforced ?? (_onVelocityEnforced = Event.Create()); }
    public Event<Collision> OnCollision { get => _onCollision ?? (_onCollision = Event<Collision>.Create()); }
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
    public float EvalFrequencySeconds =>  (this.Speed > ColliderGroup.HighestSpeedForEvalCalc ? ColliderGroup.MostFrequentEval : ColliderGroup.EvalFrequencySlope * this.speed + ColliderGroup.LeastFrequentEval);

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
        _onAngleChanged?.TryDispose();
        _onAngleChanged = null;
        _onSpeedChanged?.TryDispose();
        _onSpeedChanged = null;
        _beforeEvaluate?.TryDispose();
        _beforeEvaluate = null;
        _beforeMove?.TryDispose();
        _beforeMove = null;
        _onVelocityEnforced?.TryDispose();
        _onVelocityEnforced = null;
        _onCollision?.TryDispose();
        _onCollision = null;
        Group?.Remove(Collider);
        Collider = null;
        Group = null;
    }

    internal void Init(GameCollider collider, ColliderGroup group)
    {
        this.Group = group;
        this.Collider = collider;
        group.Add(collider);
        this.Group = group;
        this.Collider = collider;
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

    public void AddInfluence(MotionInfluence influence)
    {
        _influences.Add(influence);
        EnsureInfluenceSubscribed();
    }

    public void RemoveInfluence(MotionInfluence influence)
    {
        _influences.Remove(influence);
        if (_influences.Count == 0 && influenceSubscriptionLifetime != null)
        {
            influenceSubscriptionLifetime.Dispose();
            influenceSubscriptionLifetime = null;
        }
    }

    private void EnsureInfluenceSubscribed()
    {
        if (influenceSubscriptionLifetime == null)
        {
            influenceSubscriptionLifetime = DefaultRecyclablePool.Instance.Rent();
            this.BeforeEvaluate.Subscribe(ApplyInfluences, influenceSubscriptionLifetime);
        }
    }

    private void ApplyInfluences()
    {
        if (_influences.Count == 0) return;
        float x = 0, y = 0;
        for (int index = 0; index < _influences.Count; index++)
        {
            var influence = _influences[index];
            x += influence.DeltaSpeed * (float)Math.Cos(influence.Angle.ToRadians());
            y += influence.DeltaSpeed * (float)Math.Sin(influence.Angle.ToRadians());
        }
        Speed = MathF.Sqrt(x * x + y * y);
        Angle = Angle.FromRadians(MathF.Atan2(y, x));
    }
}

public class MotionInfluence
{
    public float DeltaSpeed;
    public Angle Angle;
}
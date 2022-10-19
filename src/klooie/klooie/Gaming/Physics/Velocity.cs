using klooie.Gaming;
namespace klooie;
public sealed class Velocity
{
    public enum CollisionBehaviorMode
    {
        Bounce,
        Stop,
        DoNothing
    }

    internal Angle angle;
    internal float speed;
    internal float lastEvalTime;
    internal Event _onAngleChanged, _onSpeedChanged, _beforeMove, _onVelocityEnforced, _beforeEvaluate;
    internal Event<Collision> _onCollision;

    public ColliderGroup Group { get; private set; }
    public Event OnAngleChanged { get => _onAngleChanged ?? (_onAngleChanged = new Event()); }
    public Event OnSpeedChanged { get => _onSpeedChanged ?? (_onSpeedChanged = new Event()); }
    public Event BeforeEvaluate { get => _beforeEvaluate ?? (_beforeEvaluate = new Event()); }
    public Event BeforeMove { get => _beforeMove ?? (_beforeMove = new Event()); }
    public Event OnVelocityEnforced { get => _onVelocityEnforced ?? (_onVelocityEnforced = new Event()); }
    public Event<Collision> OnCollision { get => _onCollision ?? (_onCollision = new Event<Collision>()); }
    public Collision LastCollision { get; internal set; }
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

    public Velocity(GameCollider collider, ColliderGroup group)
    {
        this.Group = group;
        this.Collider = collider;
        if (collider.AutoAddToColliderGroup)
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

    public IEnumerable<GameCollider> GetObstacles(List<GameCollider> buffer = null) => Group.GetObstacles(Collider, buffer);
    public void Stop() => Speed = 0;
}
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
    internal Event _onAngleChanged, _onSpeedChanged, _beforeEvaluate;
    internal Event<MoveEval> _afterEvaluate;
    internal Event<Collision> _onCollision;

    private readonly List<MotionInfluence> _influences = new();
    private Recyclable influenceSubscriptionLifetime;

    public Event OnAngleChanged { get => _onAngleChanged ?? (_onAngleChanged = Event.Create()); }
    public Event OnSpeedChanged { get => _onSpeedChanged ?? (_onSpeedChanged = Event.Create()); }
    public Event BeforeEvaluate { get => _beforeEvaluate ?? (_beforeEvaluate = Event.Create()); }
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
        _onCollision?.TryDispose();
        _onCollision = null;
        influenceSubscriptionLifetime?.TryDispose();
        influenceSubscriptionLifetime = null;
    }



    public void Stop() => Speed = 0;

    public void AddInfluence(MotionInfluence influence)
    {
        _influences.Add(influence);
        EnsureInfluenceSubscribed();
    }

    public bool ContainsInfluence(MotionInfluence influence)
    {
        return _influences.Contains(influence);
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
        if (influenceSubscriptionLifetime != null) return;
        influenceSubscriptionLifetime = DefaultRecyclablePool.Instance.Rent();
        this.BeforeEvaluate.Subscribe(this, static me => me.ApplyInfluences(), influenceSubscriptionLifetime);
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

public class MotionInfluence
{
    public float DeltaSpeed;
    public Angle Angle;
}
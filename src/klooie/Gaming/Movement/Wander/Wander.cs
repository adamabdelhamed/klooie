using System.Numerics;

namespace klooie.Gaming;
public class WanderOptions
{
    public float AnglePrecision { get; set; } = 45;
    public float Visibility { get; set; } = 8;
    public Func<ICollidable> CuriousityPoint { get; set; }
    public float CloseEnough { get; set; } = Mover.DefaultCloseEnough;
    public Action OnDelay { get; set; }
}

/// <summary>
/// Potential-field wanderer: attraction to goal, inverse-linear obstacle repulsion,
/// reduced inertia, plus optional jitter. Fully pooled, no per-angle scoring.
/// </summary>
public class Wander : Movement
{
    private const int DelayMs = 50;
    private const float GoalWeight = 1.0f;
    private const float ObstacleWeight = 2.5f;
    private const float InertiaWeight = 0.5f;   // reduced from 1.2f

    private WanderOptions _options;
    private Vector2 _lastDir;
    private ObstacleBuffer _obstacles;
    private TaskCompletionSource _tcs;

    public static Movement Create(Velocity v, SpeedEval speed, WanderOptions opts = null)
    {
        var w = WanderPool.Instance.Rent();
        w.Bind(v, speed, opts ?? new WanderOptions());
        return w;
    }

    private void Bind(Velocity v, SpeedEval speed, WanderOptions opts)
    {
        base.Bind(v, speed);
        _options = opts;
        _lastDir = Vector2.UnitX;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _obstacles?.TryDispose();
        _tcs?.TrySetResult();
        _tcs = null;
        _options = null;
        _lastDir = Vector2.Zero;
    }

    protected override Task Move()
    {
        _tcs = new TaskCompletionSource();
        var state = WanderState.Create(this);
        MoveOnce(state);
        return _tcs.Task;
    }

    private void MoveOnce(WanderState state)
    {
        if (!state.IsStillValid())
        {
            state.Dispose();
            Finish();
            return;
        }

        AcquireObstacleBuffer();
        Vector2 position = GetCurrentPosition();

        Vector2 force = Vector2.Zero;
        force += ComputeGoalAttractionForce(position);
        force += ComputeObstacleRepulsionForce(position);
        force += ComputeInertiaForce();

        Vector2 heading = ComputeHeading(force);

        SetVelocityFromHeading(heading);

        _obstacles.TryDispose();

        if (!state.IsStillValid())
        {
            state.Dispose();
            Finish();
            return;
        }

        ScheduleNextMove(state);
    }

    private void AcquireObstacleBuffer()
    {
        _obstacles = ObstacleBufferPool.Instance.Rent();
        Velocity.GetObstacles(_obstacles);
    }

    private Vector2 GetCurrentPosition()
    {
        var center = Element.Bounds.Center;
        return new Vector2(center.Left, center.Top);
    }

    private Vector2 ComputeGoalAttractionForce(Vector2 position)
    {
        var curiosity = _options.CuriousityPoint?.Invoke();
        if (curiosity == null) return Vector2.Zero;

        var center = curiosity.Bounds.Center;
        var target = new Vector2(center.Left, center.Top);
        var toGoal = target - position;
        if (toGoal.LengthSquared() == 0f) return Vector2.Zero;
        return Vector2.Normalize(toGoal) * GoalWeight;
    }

    private Vector2 ComputeObstacleRepulsionForce(Vector2 position)
    {
        float visibility = _options.Visibility;
        float visSq = visibility * visibility;
        Vector2 repulsion = Vector2.Zero;

        for (int i = 0; i < _obstacles.WriteableBuffer.Count; i++)
        {
            var obstacle = _obstacles.WriteableBuffer[i];
            var bounds = obstacle.Bounds;

            float cx = position.X < bounds.Left ? bounds.Left : position.X > bounds.Right ? bounds.Right : position.X;
            float cy = position.Y < bounds.Top ? bounds.Top : position.Y > bounds.Bottom ? bounds.Bottom : position.Y;
            var closest = new Vector2(cx, cy);

            var away = position - closest;
            float d2 = away.LengthSquared();
            if (d2 == 0f) continue;

            float d = MathF.Sqrt(d2);
            if (d > visibility) continue;

            float strength = ObstacleWeight * (visibility - d) / d;
            repulsion += Vector2.Normalize(away) * strength;
        }

        return repulsion;
    }

    private Vector2 ComputeInertiaForce()
    {
        return _lastDir * InertiaWeight;
    }

    private Vector2 ComputeHeading(Vector2 force)
    {
        return force.LengthSquared() < float.Epsilon ? _lastDir : Vector2.Normalize(force);
    }

    private void SetVelocityFromHeading(Vector2 heading)
    {
        float rad = MathF.Atan2(heading.Y, heading.X);
        float deg = rad * (180f / MathF.PI);
        float precision = _options.AnglePrecision;
        float quantDeg = MathF.Round(deg / precision) * precision;

        Velocity.Angle = (Angle)quantDeg;
        Velocity.Speed = Speed();

        float quantRad = quantDeg * (MathF.PI / 180f);
        _lastDir = new Vector2(MathF.Cos(quantRad), MathF.Sin(quantRad));
    }

    private void ScheduleNextMove(WanderState state)
    {
        ConsoleApp.Current.InnerLoopAPIs.Delay(DelayMs, state, MoveOnceStatic);
    }

    private static void MoveOnceStatic(object o)
        => ((WanderState)o).Wander.MoveOnce((WanderState)o);

    private void Finish()
    {
        _tcs?.TrySetResult();
        _obstacles.TryDispose();
    }
}

public class WanderState : Recyclable
{
    public Wander Wander { get; private set; } = null!;
    public int WanderLease { get; private set; }
    public int ElementLease { get; private set; }

    public bool IsStillValid()
        => Wander.IsStillValid(WanderLease)
        && Wander.Velocity?.Collider.IsStillValid(ElementLease) == true;

    public static WanderState Create(Wander w)
    {
        var s = WanderStatePool.Instance.Rent();
        s.Wander = w;
        s.WanderLease = w.Lease;
        s.ElementLease = w.Velocity.Collider.Lease;
        return s;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Wander = null!;
        ElementLease = 0;
        WanderLease = 0;
    }
}


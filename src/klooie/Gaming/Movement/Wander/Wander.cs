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
/// Potential-field based wanderer: attraction to goal, repulsion from obstacles,
/// plus inertia to prevent flip-flopping. Fully pooled, no per-angle scoring.
/// </summary>
public class Wander : Movement
{
    private const int DelayMs = 50;
    private const float GoalWeight = 1.0f;
    private const float ObstacleWeight = 2.5f;
    private const float InertiaWeight = 1.2f;

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
        // 1) If our lease is gone, complete the Task *only if it still exists*, then bail.
        if (!state.IsStillValid())
        {
            state.Dispose();
            Finish();
            return;
        }

        // 2) Normal “wander” logic…
        _obstacles = ObstacleBufferPool.Instance.Rent();
        Velocity.GetObstacles(_obstacles);
        var vis = _options.Visibility;
        var visSq = vis * vis;

        var loc = Element.Bounds.Center;
        var pos = new Vector2(loc.Left, loc.Top);
        var force = Vector2.Zero;

        var curiosity = _options.CuriousityPoint?.Invoke();
        if (curiosity != null)
        {
            var cLoc = curiosity.Bounds.Center;
            var tgt = new Vector2(cLoc.Left, cLoc.Top);
            var toGoal = tgt - pos;
            if (toGoal.LengthSquared() > 0f)
                force += Vector2.Normalize(toGoal) * GoalWeight;
        }

        foreach (var ob in _obstacles.WriteableBuffer)
        {
            var b = ob.Bounds;
            float cx = pos.X < b.Left ? b.Left : pos.X > b.Right ? b.Right : pos.X;
            float cy = pos.Y < b.Top ? b.Top : pos.Y > b.Bottom ? b.Bottom : pos.Y;
            var closest = new Vector2(cx, cy);

            var away = pos - closest;
            var d2 = away.LengthSquared();
            if (d2 > 0f && d2 <= visSq)
                force += Vector2.Normalize(away) * (ObstacleWeight / d2);
        }

        force += _lastDir * InertiaWeight;

        var heading = (force.LengthSquared() < float.Epsilon)
            ? _lastDir
            : Vector2.Normalize(force);

        float rad = MathF.Atan2(heading.Y, heading.X);
        float deg = rad * (180f / MathF.PI);
        float prec = _options.AnglePrecision;
        float quantDeg = MathF.Round(deg / prec) * prec;

        Velocity.Angle = (Angle)quantDeg;
        Velocity.Speed = Speed();

        float radQ = quantDeg * (MathF.PI / 180f);
        _lastDir = new Vector2(MathF.Cos(radQ), MathF.Sin(radQ));

        _obstacles.TryDispose();

        if (!state.IsStillValid())
        {
            state.Dispose();
            Finish();
            return;
        }
        ConsoleApp.Current.InnerLoopAPIs.Delay(DelayMs,state, MoveOnceStatic);
    }

    private static void MoveOnceStatic(object obj)
    {
        var state = (WanderState)obj;
        state.Wander.MoveOnce(state);
    }

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

    public bool IsStillValid() => Wander.IsStillValid(WanderLease);
    public static WanderState Create(Wander w)
    {
        var state = WanderStatePool.Instance.Rent();
        state.Wander = w;
        state.WanderLease = w.Lease;
        return state;
    }
}

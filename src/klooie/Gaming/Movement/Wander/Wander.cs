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
            var st = WanderState.Create(this);
            MoveOnce(st);
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

            // gather obstacles
            _obstacles = ObstacleBufferPool.Instance.Rent();
            Velocity.GetObstacles(_obstacles);

            float vis = _options.Visibility;
            float visSq = vis * vis;

            var loc = Element.Bounds.Center;
            var pos = new Vector2(loc.Left, loc.Top);
            var force = Vector2.Zero;

            // attraction to goal
            var curiosity = _options.CuriousityPoint?.Invoke();
            if (curiosity != null)
            {
                var cLoc = curiosity.Bounds.Center;
                var tgt = new Vector2(cLoc.Left, cLoc.Top);
                var toGoal = tgt - pos;
                if (toGoal.LengthSquared() > 0f)
                    force += Vector2.Normalize(toGoal) * GoalWeight;
            }

            // inverse-linear repulsion
            for (int i = 0; i < _obstacles.WriteableBuffer.Count; i++)
            {
                var ob = _obstacles.WriteableBuffer[i];
                var b = ob.Bounds;

                // clamp to obstacle AABB
                float cx = pos.X < b.Left ? b.Left : pos.X > b.Right ? b.Right : pos.X;
                float cy = pos.Y < b.Top ? b.Top : pos.Y > b.Bottom ? b.Bottom : pos.Y;
                var closest = new Vector2(cx, cy);

                var away = pos - closest;
                float d2 = away.LengthSquared();
                if (d2 == 0f)
                    continue;

                float d = MathF.Sqrt(d2);
                if (d > vis)
                    continue;

                // inverse-linear: strength ∝ (vis - d) / d
                float strength = ObstacleWeight * (vis - d) / d;
                force += Vector2.Normalize(away) * strength;
            }

            // inertia
            force += _lastDir * InertiaWeight;

            // optional tiny jitter (uncomment to use):
            //var θ = (float)(Random.Shared.NextDouble() * Math.PI * 2);
            //force += new Vector2(MathF.Cos(θ), MathF.Sin(θ)) * 0.1f;

            // compute heading
            Vector2 heading = force.LengthSquared() < float.Epsilon
                ? _lastDir
                : Vector2.Normalize(force);

            // quantize angle
            float rad = MathF.Atan2(heading.Y, heading.X);
            float deg = rad * (180f / MathF.PI);
            float prec = _options.AnglePrecision;
            float quantDeg = MathF.Round(deg / prec) * prec;

            Velocity.Angle = (Angle)quantDeg;
            Velocity.Speed = Speed();

            // store for next inertia
            float radQ = quantDeg * (MathF.PI / 180f);
            _lastDir = new Vector2(MathF.Cos(radQ), MathF.Sin(radQ));

            _obstacles.TryDispose();

            if (!state.IsStillValid())
            {
                state.Dispose();
                Finish();
                return;
            }

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

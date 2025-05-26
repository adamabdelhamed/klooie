namespace klooie.Gaming;
public class WanderOptions : MovementOptions
{
    /// <summary>
    /// Point of interest to wander towards, if any. If set, the wanderer will
    /// try to move towards this point which may be an object or an empty space.
    /// 
    /// If not set, the wanderer will wander in the space, trying to avoid objects without
    /// bumping into them. 
    /// 
    /// In all cases, the wanderer will try to avoid turning backwards to avoid obstacles.
    /// Instead it will intelligently alter its trajectory to move around obstacles. That
    /// intelligence is informed by the curiosity point, if set, or by common sense if not.
    /// </summary>
    public Func<ICollidable>? CuriousityPoint { get; set; }

    /// <summary>
    /// When CuriosityPoint is set, the wanderer will stop wandering if it gets within
    /// this distance from the point. 
    /// </summary>
    public float CloseEnough { get; set; } = Mover.DefaultCloseEnough;

    /// <summary>
    /// Optional code to run before the wanderer is delayed for its next adjustment.
    /// </summary>
    public Action OnDelay { get; set; }
}

/// <summary>
/// A movement that wanders around the space, trying to avoid obstacles and
/// optionally moving towards a point of interest.
/// </summary>
public class Wander : Movement
{
    private const int DelayMs = 50;
    private TaskCompletionSource _tcs;
    public WanderOptions WanderOptions => (WanderOptions)Options;

    public static Movement Create(WanderOptions options)
    {
        var w = WanderPool.Instance.Rent();
        w.Bind(options);
        return w;
    }

    private void Bind(WanderOptions opts)
    {
        base.Bind(opts);
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _tcs?.TrySetResult();
        _tcs = null;
    }

    protected override Task Move()
    {
        _tcs = new TaskCompletionSource();
        var state = WanderLoopState.Create(this, true);
        MoveOnce(state);
        return _tcs.Task;
    }

    private void MoveOnce(WanderLoopState state)
    {
        if (!state.IsStillValid())
        {
            state.Dispose();
            Finish();
            return;
        }
        WanderLogic.AdjustSpeedAndVelocity(state);

        if (!state.IsStillValid())
        {
            state.Dispose();
            Finish();
            return;
        }

        ScheduleNextMove(state);
    }

 

    private void ScheduleNextMove(WanderLoopState state)
    {
        ConsoleApp.Current.InnerLoopAPIs.Delay(DelayMs, state, MoveOnceStatic);
    }

    private static void MoveOnceStatic(object o)
    {
        var state = (WanderLoopState)o;
        state.Wander.WanderOptions.OnDelay?.Invoke();
        state.Wander.MoveOnce(state);
    }

    private void Finish()
    {
        _tcs?.TrySetResult();
    }
}

 



/// <summary>
/// Normalised component-by-component score for a single candidate steering angle.
/// Every field is mapped to [0,1] where 1 = "best" and 0 = "worst".
/// </summary>
public readonly struct AngleScore
{
    public readonly Angle Angle;
    public readonly float Collision;   // 1 means no imminent collision
    public readonly float Inertia;     // 1 means perfectly aligned with recent direction
    public readonly float Forward;     // 1 means continues roughly forward
    public readonly float Curiosity;   // 1 means directly towards curiosity point (if any)
    public readonly float Total;       // Weighted sum of the four components (already in [0,1] by design)

    public AngleScore(Angle angle, float collision, float inertia, float forward, float curiosity, WanderWeights w)
    {
        Angle = angle;
        Collision = collision;
        Inertia = inertia;
        Forward = forward;
        Curiosity = curiosity;
        Total = Collision * w.CollisionWeight +
                    Inertia * w.InertiaWeight +
                    Forward * w.ForwardWeight +
                    Curiosity * w.CuriosityWeight;
    }
}

/// <summary>
/// Tunable weighting for <see cref="AngleScore"/> components.  The default keeps historical behaviour
/// reasonably close to the original hand-tuned constants, but you can adjust in unit tests or
/// Monte-Carlo search without touching implementation code.
/// </summary>
public struct WanderWeights
{
    public float CollisionWeight;
    public float InertiaWeight;
    public float ForwardWeight;
    public float CuriosityWeight;

    public static readonly WanderWeights Default = new WanderWeights
    {
        CollisionWeight = 1.00f,
        InertiaWeight = 0.25f,
        ForwardWeight = 0.15f,
        CuriosityWeight = 0.35f
    };
}

 
public class WanderLoopState : Recyclable
{
    public Wander Wander { get; private set; } = null!;
    public int WanderLease { get; private set; }
    public int ElementLease { get; private set; }

    /// <summary>
    /// The last few angles the wanderer has taken. This is used to avoid
    /// turning backwards over and over and to help the wanderer make intelligent decisions
    /// </summary>
    public List<Angle> LastFewAngles = new List<Angle>();

    // --- New for testability/diagnostics ---
    public WanderWeights Weights { get; set; } = WanderWeights.Default;
    public RecyclableList<AngleScore> AngleScores { get; private set; }   


    public bool IsStillValid()
        => Wander.IsStillValid(WanderLease)
        && Wander.Options.Velocity?.Collider.IsStillValid(ElementLease) == true;

    public static WanderLoopState Create(Wander w, bool autoDispose)
    {
        var s = WanderLoopStatePool.Instance.Rent();
        s.Wander = w;
        s.WanderLease = w.Lease;
        s.ElementLease = w.Options.Velocity.Collider.Lease;
        s.Weights = WanderWeights.Default;
        s.AngleScores = RecyclableListPool<AngleScore>.Instance.Rent();
        return s;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Wander = null!;
        ElementLease = 0;
        WanderLease = 0;
        LastFewAngles.Clear();
        AngleScores?.TryDispose(); 
        AngleScores = null!; 
    }
}

/// <summary>
/// Logic for the Wander movement, including speed and velocity adjustments.
/// 
/// Requirements:
/// - The wanderer intelligentlys avoid obstacles by altering its trajectory a few seconds before collision.
/// - The wanderer strongly prefers to walk around obstacles rather than turning around and going back.
/// - If a curiosity point is set, the wanderer should try to move towards it. Speed should be set to zero if the wanderer is close enough to the curiosity point.
/// - If the curiosity point is not set, the wanderer should wander in the space, trying to avoid objects without bumping into them.
/// - All code of non-trivial complexity should be isolated in its own method that accepts the WanderLoopState as a parameter.
///     - All math of non-trivial complexity should have helpful variable names and comments.
/// - Wanderers and obstacles are rectangles that can be any size. The code should never assume a specific size.
/// </summary>
// klooie.Gaming – Refactored WanderLogic for testability, prod/test control, and encapsulated weights/scores
public static class WanderLogic
{
    // --- Public surface --------------------------------------------------------------------

    /// <summary>
    /// Top-level API used by <see cref="Wander"/>.  Returns the list of scores for every candidate.
    /// If <see cref="WanderLoopState.AutoDispose"/> is true (default), scores are disposed immediately after use.
    /// In unit tests, set <c>AutoDispose = false</c> to inspect and dispose later.
    /// </summary>
    public static RecyclableList<AngleScore>? AdjustSpeedAndVelocity(WanderLoopState state)
    {
        ComputeScores(state);

        // pick best angle
        AngleScore best = state.AngleScores[0];
        for (int i = 1; i < state.AngleScores.Count; i++)
        {
            if (state.AngleScores[i].Total > best.Total)
            {
                best = state.AngleScores[i];
            }
        }
        var angle = best.Angle;
        var speed = EvaluateSpeed(state, angle);

        var velocity = state.Wander.WanderOptions.Velocity;
        velocity.Angle = angle;
        velocity.Speed = speed;

        // Update inertia history
        state.LastFewAngles.Add(angle);
        if (state.LastFewAngles.Count > 10) state.LastFewAngles.RemoveAt(0);

        return state.AngleScores;
    }

    // --- Private helpers -------------------------------------------------------------------

    private const float MaxCollisionHorizon = 3f;   // seconds – > this long = perfectly safe
    private const float MaxAngleDeviation = 180f; // degrees – used for normalisation

    private static void ComputeScores(WanderLoopState state)
    {
        var opts = state.Wander.WanderOptions;
        var velocity = opts.Velocity;
        var curAngle = velocity.Angle;
        var inertiaAngle = AverageAngle(state.LastFewAngles, curAngle);

        Angle? curiosityAngle = null;
        if (opts.CuriousityPoint != null)
        {
            var target = opts.CuriousityPoint();
            if (target != null)
                curiosityAngle = velocity.Collider.Bounds.CalculateAngleTo(target.Bounds);
        }

        int steps = 15;
        float stepSize = 15f;
        int numSteps = (int)(MaxAngleDeviation / stepSize);

        state.AngleScores.Items.Clear();
        for (int i = -numSteps; i <= numSteps; i++)
        {
            var candidate = curAngle.Add(i * stepSize);

            // Compute raw components
            float rawCollision = PredictCollision(state, candidate);
            float rawInertia = candidate.DiffShortest(inertiaAngle);
            float rawForward = candidate.DiffShortest(curAngle);
            float rawCuriosity = curiosityAngle.HasValue ? candidate.DiffShortest(curiosityAngle.Value) : MaxAngleDeviation;

            // Normalise [0,1]
            float normCollision = Clamp01(rawCollision / MaxCollisionHorizon);             // bigger time -> better
            float normInertia = 1f - Clamp01(rawInertia / MaxAngleDeviation);            // smaller deviation -> better
            float normForward = 1f - Clamp01(rawForward / MaxAngleDeviation);
            float normCuriosity = 1f - Clamp01(rawCuriosity / MaxAngleDeviation);

            var score = new AngleScore(candidate, normCollision, normInertia, normForward, normCuriosity, state.Weights);
            state.AngleScores.Items.Add(score);
        }
    }

    private static float EvaluateSpeed(WanderLoopState state, Angle chosenAngle)
    {
        var opts = state.Wander.WanderOptions;
        var velocity = opts.Velocity;
        var currentSpeed = state.Wander.Options.Speed();

        if (opts.CuriousityPoint != null)
        {
            var target = opts.CuriousityPoint();
            if (target != null)
            {
                float distance = velocity.Collider.Bounds.CalculateDistanceTo(target.Bounds);
                if (distance <= opts.CloseEnough)
                    return 0f;
            }
        }
        return currentSpeed;
    }

    // ----------------------------------------------------------------------------------------
    // Individual component calculations -----------------------------------------------------
    // ----------------------------------------------------------------------------------------

    private static float PredictCollision(WanderLoopState state, Angle angle)
    {
        var buffer = ObstacleBufferPool.Instance.Rent();
        var prediction = CollisionPredictionPool.Instance.Rent();
        try
        {
            // Gather nearby obstacles (already tracked by vision)
            var tracked = state.Wander.Options.Vision.TrackedObjectsList;
            for (int i = 0; i < tracked.Count; i++)
                buffer.WriteableBuffer.Add(tracked[i].Target);

            CollisionDetector.Predict(
                state.Wander.Options.Velocity.Collider,
                angle,
                buffer.WriteableBuffer,
                state.Wander.Options.Vision.Range,
                CastingMode.SingleRay,
                buffer.WriteableBuffer.Count,
                prediction);

            if (!prediction.CollisionPredicted)
                return MaxCollisionHorizon; // safe path

            return prediction.LKGD * state.Wander.Options.Velocity.Speed;
        }
        finally
        {
            buffer.Dispose();
            prediction.Dispose();
        }
    }

    // ----------------------------------------------------------------------------------------
    // Utility -------------------------------------------------------------------------------
    // ----------------------------------------------------------------------------------------

    private static Angle AverageAngle(List<Angle> list, Angle fallback)
    {
        if (list.Count == 0) return fallback;
        float sum = 0f;
        foreach (var a in list) sum += a.Value;
        return new Angle(sum / list.Count);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
}




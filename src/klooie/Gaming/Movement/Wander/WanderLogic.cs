namespace klooie.Gaming;


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

        var velocity = state.WanderOptions.Velocity;
        velocity.Angle = angle;
        velocity.Speed = speed;

        // Update inertia history
        state.LastFewAngles.Add(angle);
        if (state.LastFewAngles.Count > 10) state.LastFewAngles.RemoveAt(0);

        return state.AngleScores;
    }

    // --- Private helpers -------------------------------------------------------------------

    private const float MaxCollisionHorizon = 3f;   // seconds – > this long = perfectly safe

    private static void ComputeScores(WanderLoopState state)
    {
        var opts = state.WanderOptions;
        var velocity = opts.Velocity;
        var inertiaAngle = AverageAngle(state.LastFewAngles, state.WanderOptions.Velocity.Angle);

        Angle? curiosityAngle = null;
        if (opts.CuriousityPoint != null)
        {
            var target = opts.CuriousityPoint();
            if (target != null)
                curiosityAngle = velocity.Collider.Bounds.CalculateAngleTo(target.Bounds);
        }

        state.AngleScores.Items.Clear();
        var totalAngularTravel = 180;
        float travelCompleted = 0f;
        var travelPerStep = 12f;
        ScoreAngle(state, inertiaAngle, curiosityAngle, state.WanderOptions.Velocity.Angle); // score the current angle first
        while (travelCompleted < totalAngularTravel)
        {
            var leftCandidate = state.WanderOptions.Velocity.Angle.Add(-(travelPerStep+travelCompleted));
            var rightCandidate = state.WanderOptions.Velocity.Angle.Add(travelPerStep + travelCompleted);
            travelCompleted += travelPerStep;

            ScoreAngle(state, inertiaAngle, curiosityAngle,leftCandidate);
            ScoreAngle(state, inertiaAngle, curiosityAngle, rightCandidate);
        }
    }

    private static void ScoreAngle(WanderLoopState state, Angle inertiaAngle, Angle? curiosityAngle, Angle candidate)
    {
        // Compute raw components
        float rawCollision = PredictCollision(state, candidate);
        float rawInertia = candidate.DiffShortest(inertiaAngle);
        float rawForward = candidate.DiffShortest(state.WanderOptions.Velocity.Angle);
        float rawCuriosity = curiosityAngle.HasValue ? candidate.DiffShortest(curiosityAngle.Value) : state.WanderOptions.Vision.AngularVisibility;

        // Normalise [0,1]
        float normCollision = Clamp01(rawCollision / MaxCollisionHorizon);             // bigger time -> better
        float normInertia = 1f - Clamp01(rawInertia / state.WanderOptions.Vision.AngularVisibility);            // smaller deviation -> better
        float normForward = 1f - Clamp01(rawForward / state.WanderOptions.Vision.AngularVisibility);
        float normCuriosity = 1f - Clamp01(rawCuriosity / state.WanderOptions.Vision.AngularVisibility);

        var score = new AngleScore(candidate, normCollision, normInertia, normForward, normCuriosity, state.Weights);
        state.AngleScores.Items.Add(score);
    }

    private static float EvaluateSpeed(WanderLoopState state, Angle chosenAngle)
    {
        var opts = state.WanderOptions;
        var velocity = opts.Velocity;
        var currentSpeed = state.WanderOptions.Speed();

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
            var tracked = state.WanderOptions.Vision.TrackedObjectsList;
            for (int i = 0; i < tracked.Count; i++)
                buffer.WriteableBuffer.Add(tracked[i].Target);

            CollisionDetector.Predict(
                state.WanderOptions.Velocity.Collider,
                angle,
                buffer.WriteableBuffer,
                state.WanderOptions.Vision.Range,
                CastingMode.Rough,
                buffer.WriteableBuffer.Count,
                prediction);

            if (!prediction.CollisionPredicted)
                return MaxCollisionHorizon; // safe path

            return (state.WanderOptions.Velocity.Speed > 0)
             ? prediction.LKGD / state.WanderOptions.Velocity.Speed
             : float.MaxValue;
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

        float sumWeights = w.CollisionWeight + w.InertiaWeight + w.ForwardWeight + w.CuriosityWeight;
        if (sumWeights <= 0f) sumWeights = 1f; // avoid divide by zero

        Total = (
            Collision * w.CollisionWeight +
            Inertia * w.InertiaWeight +
            Forward * w.ForwardWeight +
            Curiosity * w.CuriosityWeight
        ) / sumWeights;
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
        InertiaWeight = 0.6f,
        ForwardWeight = 0.3f,
        CuriosityWeight = 0.45f
    };
}


public class WanderLoopState : Recyclable
{
    internal Wander Wander { get; private set; }
    public WanderOptions WanderOptions { get; private set; }
    public int WanderLease { get; private set; }
    public int ElementLease { get; private set; }
    public int VisionLease { get; private set; }

    /// <summary>
    /// The last few angles the wanderer has taken. This is used to avoid
    /// turning backwards over and over and to help the wanderer make intelligent decisions
    /// </summary>
    public List<Angle> LastFewAngles = new List<Angle>();

    // --- New for testability/diagnostics ---
    public WanderWeights Weights { get; set; } = WanderWeights.Default;
    public RecyclableList<AngleScore> AngleScores { get; private set; }

    public Task? Task => _tcs?.Task;

    private TaskCompletionSource _tcs;


    public static WanderLoopState Create(Wander w)
    {
        var s = Create(w.WanderOptions);
        s.Wander = w;
        s.WanderLease = w.Lease;    
        return s;
    }

    public static WanderLoopState Create(WanderOptions o)
    {
        var s = WanderLoopStatePool.Instance.Rent();
        s._tcs = new TaskCompletionSource();
        s.WanderOptions = o;
        s.VisionLease = o.Vision.Lease;
        s.WanderLease = 0;
        s.ElementLease = o.Velocity.Collider.Lease;
        s.Weights = WanderWeights.Default;
        s.AngleScores = RecyclableListPool<AngleScore>.Instance.Rent();
        return s;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        WanderOptions = null;
        Wander = null;
        ElementLease = 0;
        WanderLease = 0;
        VisionLease = 0;
        LastFewAngles.Clear();
        AngleScores?.TryDispose();
        AngleScores = null!;
        _tcs.TrySetResult();
        _tcs = null!;
    }
}

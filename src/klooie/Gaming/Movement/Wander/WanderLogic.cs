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
    private static readonly BackgroundColorFilter DebugModeEmergencyFilter = new BackgroundColorFilter(RGB.Orange);
    // --- Public surface --------------------------------------------------------------------

    /// <summary>
    /// Top-level API used by <see cref="Wander"/>.  Returns the list of scores for every candidate.
    /// </summary>
    public static RecyclableList<AngleScore> AdjustSpeedAndVelocity(WanderLoopState state)
    {
        ComputeScores(state);
        ConsiderEmergencyMode(state);
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

       
        state.Influence.Angle = angle;
        state.Influence.DeltaSpeed = speed;

        // Update inertia history
        state.LastFewAngles.Items.Add(angle);
        if (state.LastFewAngles.Count > 10) state.LastFewAngles.Items.RemoveAt(0);

        var collider = state.WanderOptions.Velocity.Collider;
        var rounded = collider.Bounds.Round();

        state.LastFewRoundedBounds.Items.Add(rounded);
        if (state.LastFewRoundedBounds.Count > 10)
            state.LastFewRoundedBounds.Items.RemoveAt(0);

        return state.AngleScores;
    }

    // --- Private helpers -------------------------------------------------------------------

    private static void ConsiderEmergencyMode(WanderLoopState state)
    {
        // Emergency mode if stuck
        if (IsStuck(state))
        {
#if DEBUG
            if(state.WanderOptions.Velocity.Collider.Filters.Contains(DebugModeEmergencyFilter) == false)
            {
                state.WanderOptions.Velocity.Collider.Filters.Add(DebugModeEmergencyFilter);
            }
#endif
            var emergencyWeights = state.Weights;
            emergencyWeights.InertiaWeight = -0.2f;
            emergencyWeights.ForwardWeight = -0.2f;
            RescoreAnglesWithWeights(state, emergencyWeights);
        }
        else
        {
#if DEBUG
            if (state.WanderOptions.Velocity.Collider.Filters.Contains(DebugModeEmergencyFilter))
            {
                state.WanderOptions.Velocity.Collider.Filters.Remove(DebugModeEmergencyFilter);
            }
#endif
        }
    }

    private const float MaxCollisionHorizon = 1.25f;   // seconds – > this long = perfectly safe

    private static void ComputeScores(WanderLoopState state)
    {
        var opts = state.WanderOptions;
        var velocity = opts.Velocity;
        var inertiaAngle = AverageAngle(state.LastFewAngles.Items, state.WanderOptions.Velocity.Angle);

        Angle? curiosityAngle = null;
        if (opts.CuriousityPoint != null)
        {
            var target = opts.CuriousityPoint();
            if (target.HasValue)
                curiosityAngle = velocity.Collider.Bounds.CalculateAngleTo(target.Value);
        }

        state.AngleScores.Items.Clear();
        var totalAngularTravel = 180f;
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
        float normCollision = DangerClamp(rawCollision);
        float normInertia = 1f - Clamp01(rawInertia / 180);            // smaller deviation -> better
        float normForward = 1f - Clamp01(rawForward / 90);
        float normCuriosity = 1f - Clamp01(rawCuriosity / 180);

        float inertiaWeight = state.Weights.InertiaWeight;
        float curiosityWeight = state.Weights.CuriosityWeight;
        if (state.LastFewAngles.Count == 0)
        {
            normInertia = 0f;          // Could be any value—weight will be zero
            inertiaWeight = 0f;
        }

        if(curiosityAngle.HasValue == false)
        {
            normCuriosity = 0f;       // No curiosity point, so no curiosity score
            curiosityWeight = 0f; // No curiosity point, so no curiosity score
        }

        var score = new AngleScore(candidate, normCollision, normInertia, normForward, normCuriosity, new WanderWeights()
        {
            CollisionWeight = state.Weights.CollisionWeight,
            InertiaWeight = inertiaWeight,
            ForwardWeight = state.Weights.ForwardWeight,
            CuriosityWeight = curiosityWeight
        });
        state.AngleScores.Items.Add(score);
    }

    private static float DangerClamp(float timeToCollision)
    {
        const float SAFE = MaxCollisionHorizon; // 2
        const float DANGER = .2f;
        if (timeToCollision >= SAFE) return 1f;
        if (timeToCollision <= DANGER) return 0f;
        float t = (timeToCollision - DANGER) / (SAFE - DANGER); 
                                                           
        return t; 
    }


    private static void RescoreAnglesWithWeights(WanderLoopState state, WanderWeights weights)
    {
        for (int i = 0; i < state.AngleScores.Count; i++)
        {
            var prev = state.AngleScores[i];
            state.AngleScores[i] = new AngleScore(
                prev.Angle,
                prev.Collision,
                prev.Inertia,
                prev.Forward,
                prev.Curiosity,
                weights
            );
        }
    }

    // We are single threaded so it's safe
    private static HashSet<RectF> sharedUniqueModeHashSet = new HashSet<RectF>();
    private static bool IsStuck(WanderLoopState state, int window = 3, int maxUnique = 2)
    {
        if (state.LastFewRoundedBounds.Count < window) return false;
        sharedUniqueModeHashSet.Clear();
        for (int i = state.LastFewRoundedBounds.Count - window; i < state.LastFewRoundedBounds.Count; i++)
        {
            sharedUniqueModeHashSet.Add(state.LastFewRoundedBounds[i]);
        }
        return sharedUniqueModeHashSet.Count <= maxUnique;  
    }

    private static float EvaluateSpeed(WanderLoopState state, Angle chosenAngle)
    {
        var opts = state.WanderOptions;
        var velocity = opts.Velocity;
        var currentSpeed = state.WanderOptions.Speed();

        if (opts.CuriousityPoint != null)
        {
            var target = opts.CuriousityPoint();
            if (target.HasValue)
            {
                float distance = velocity.Collider.Bounds.CalculateDistanceTo(target.Value);
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
        for (int i = 0; i < list.Count; i++)
        {
            Angle a = list[i];
            sum += a.Value;
        }

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
    public readonly WanderWeights Weights; // Weights used to compute the score
    public AngleScore(Angle angle, float collision, float inertia, float forward, float curiosity, WanderWeights w)
    {
        Weights = w;
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
        CollisionWeight = 1.10f,
        InertiaWeight = 0.2f,
        ForwardWeight = 0.15f,
        CuriosityWeight = 0.7f
    };
}


public class WanderLoopState : Recyclable
{
    internal Wander Wander { get; private set; }
    public WanderOptions WanderOptions { get; private set; }
    public int WanderLease { get; private set; }
    public int ElementLease { get; private set; }
    public int VisionLease { get; private set; }
    public int VelocityLease { get; private set; }

    public MotionInfluence Influence { get;private set; }

    /// <summary>
    /// The last few angles the wanderer has taken. This is used to avoid
    /// turning backwards over and over and to help the wanderer make intelligent decisions
    /// </summary>
    public RecyclableList<Angle> LastFewAngles;

    public RecyclableList<RectF> LastFewRoundedBounds;

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
        s.VisionLease = w.WanderOptions.Vision.Lease;
        s.ElementLease = w.WanderOptions.Velocity.Collider.Lease;
        s.VelocityLease = w.WanderOptions.Velocity.Lease;
        s.LastFewAngles = RecyclableListPool<Angle>.Instance.Rent();
        s.LastFewRoundedBounds = RecyclableListPool<RectF>.Instance.Rent();
        s.Influence = new MotionInfluence();
        w.WanderOptions.Velocity.AddInfluence(s.Influence);
        s.OnDisposed(() => w.WanderOptions.Velocity.RemoveInfluence(s.Influence));
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
        s.VelocityLease = o.Velocity.Lease;
        s.Weights = WanderWeights.Default;
        s.AngleScores = RecyclableListPool<AngleScore>.Instance.Rent();
        s.LastFewAngles = RecyclableListPool<Angle>.Instance.Rent();
        s.LastFewRoundedBounds = RecyclableListPool<RectF>.Instance.Rent();
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
        LastFewAngles.TryDispose();
        LastFewAngles = null!;
        LastFewRoundedBounds.TryDispose();
        LastFewRoundedBounds = null!;
        AngleScores?.TryDispose();
        AngleScores = null!;
        _tcs.TrySetResult();
        _tcs = null!;
        Influence = null!; 
    }
}

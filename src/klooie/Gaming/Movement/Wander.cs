namespace klooie.Gaming;

public class Wander 
{
    public static void Execute(WanderMovementState state) => ConsoleApp.Current.InnerLoopAPIs.Delay(state.ScanOffset, state, StaticTick);
    private static void StaticTick(object o) => Tick((WanderMovementState)o);

    private static void Tick(WanderMovementState state)
    {
        if(state.IsStillValid(state.Lease) == false) throw new InvalidOperationException("WanderMovementState is not valid, but was passed to Wander.Tick");
        var lease = state.Lease;
        if (state.AreAllDependenciesValid == false)
        {          
            state.TryDispose(lease);
            return;
        }
        var scores = WanderLogic.AdjustSpeedAndVelocity(state);
        //onNewScoresAvailable?.Fire(scores.Items);


        ConsoleApp.Current.InnerLoopAPIs.DelayIfValid(state.DelayMs, state, StaticTick);
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
    /// </summary>
    public static RecyclableList<AngleScore> AdjustSpeedAndVelocity(WanderMovementState state)
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
#if DEBUG
        (state as WanderDebuggerState)?.ApplyMovingFreeFilter();
#endif


        // Update inertia history
        state.LastFewAngles.Items.Add(angle);
        if (state.LastFewAngles.Count > 10) state.LastFewAngles.Items.RemoveAt(0);

        var rounded = state.Eye.Bounds.Round();

        state.LastFewRoundedBounds.Items.Add(rounded);
        if (state.LastFewRoundedBounds.Count > 10)
            state.LastFewRoundedBounds.Items.RemoveAt(0);
#if DEBUG
        (state as WanderDebuggerState)?.RefreshLoopRunningFilter();
#endif

        return state.AngleScores;
    }

    // --- Private helpers -------------------------------------------------------------------

    private static void ConsiderEmergencyMode(WanderMovementState state)
    {
        return;
        if (state.StuckCooldownTicks > 0)
        {
            state.StuckCooldownTicks--;
            return;
        }

        // Emergency mode if stuck
        if (IsStuck(state))
        {
            state.StuckCooldownTicks = 5;
#if DEBUG
            (state as WanderDebuggerState)?.ApplyStuckFilter();
#endif
            var emergencyWeights = state.Weights;
            emergencyWeights.InertiaWeight = -0.2f;
            emergencyWeights.ForwardWeight = -0.2f;
            RescoreAnglesWithWeights(state, emergencyWeights);
        }
        else
        {
#if DEBUG
            (state as WanderDebuggerState)?.ClearStuckFilter();
#endif
        }
    }



    private const float MaxCollisionHorizon = 1.25f;   // seconds – > this long = perfectly safe

    private static void ComputeScores(WanderMovementState state)
    {
        var inertiaAngle = AverageAngle(state.LastFewAngles.Items, state.Velocity.Angle);

        Angle? curiosityAngle = null;
        if (state.CuriosityPoint != null)
        {
            var target = state.CuriosityPoint(state);
            if (target.HasValue)
                curiosityAngle = state.Eye.CalculateAngleTo(target.Value);
        }

        state.AngleScores.Items.Clear();
        var totalAngularTravel = 180f;
        float travelCompleted = 0f;
        var travelPerStep = 12f;
        ScoreAngle(state, inertiaAngle, curiosityAngle, state.Velocity.Angle); // score the current angle first
        while (travelCompleted < totalAngularTravel)
        {
            var leftCandidate = state.Velocity.Angle.Add(-(travelPerStep + travelCompleted));
            var rightCandidate = state.Velocity.Angle.Add(travelPerStep + travelCompleted);
            travelCompleted += travelPerStep;

            ScoreAngle(state, inertiaAngle, curiosityAngle, leftCandidate);
            ScoreAngle(state, inertiaAngle, curiosityAngle, rightCandidate);
        }
    }

    private static void ScoreAngle(WanderMovementState state, Angle inertiaAngle, Angle? curiosityAngle, Angle candidate)
    {
        // Compute raw components
        float rawCollision = PredictCollision(state, candidate);
        float rawInertia = candidate.DiffShortest(inertiaAngle);
        float rawForward = candidate.DiffShortest(state.Velocity.Angle);
        float rawCuriosity = curiosityAngle.HasValue ? candidate.DiffShortest(curiosityAngle.Value) : state.Vision.AngularVisibility;

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

        if (curiosityAngle.HasValue == false)
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


    private static void RescoreAnglesWithWeights(WanderMovementState state, WanderWeights weights)
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
    private static bool IsStuck(WanderMovementState state, int window = 5, int maxUnique = 4)
    {
        if (state.LastFewRoundedBounds.Count < window) return false;
        sharedUniqueModeHashSet.Clear();
        for (int i = state.LastFewRoundedBounds.Count - window; i < state.LastFewRoundedBounds.Count; i++)
        {
            sharedUniqueModeHashSet.Add(state.LastFewRoundedBounds[i]);
        }
        return sharedUniqueModeHashSet.Count <= maxUnique;
    }

    private static float EvaluateSpeed(WanderMovementState state, Angle chosenAngle)
    {
        if (state.Speed() == 0) throw new Exception("Zero Speed Yo");
        var pointOfInterest = state.CuriosityPoint == null ? null : state.CuriosityPoint.Invoke(state);
        state.IsCurrentlyCloseEnoughToPointOfInterest = pointOfInterest.HasValue && state.Eye.Bounds.CalculateNormalizedDistanceTo(pointOfInterest.Value) <= state.CloseEnough;
        return state.IsCurrentlyCloseEnoughToPointOfInterest ? 0 : state.Speed();
    }

    // ----------------------------------------------------------------------------------------
    // Individual component calculations -----------------------------------------------------
    // ----------------------------------------------------------------------------------------

    private static float PredictCollision(WanderMovementState state, Angle angle)
    {
        var buffer = ObstacleBufferPool.Instance.Rent();
        var prediction = CollisionPredictionPool.Instance.Rent();
        try
        {
            // Gather nearby obstacles (already tracked by vision)
            var tracked = state.Vision.TrackedObjectsList;
            for (int i = 0; i < tracked.Count; i++)
                buffer.WriteableBuffer.Add(tracked[i].Target);

            CollisionDetector.Predict(
                state.Eye,
                angle,
                buffer.WriteableBuffer,
                state.Vision.Range,
                CastingMode.Rough,
                buffer.WriteableBuffer.Count,
                prediction);

            if (!prediction.CollisionPredicted)
                return MaxCollisionHorizon; // safe path

            return (state.Velocity.Speed > 0)
             ? prediction.LKGD / state.Velocity.Speed
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


public class WanderMovementState : MovementState
{
    public MotionInfluence Influence { get; private set; }
    public RecyclableList<Angle> LastFewAngles;
    public RecyclableList<RectF> LastFewRoundedBounds;
    public WanderWeights Weights { get; set; } = WanderWeights.Default;
    public RecyclableList<AngleScore> AngleScores { get; private set; }
    public float CloseEnough { get; set; }
    public int StuckCooldownTicks = 0;
    public bool IsCurrentlyCloseEnoughToPointOfInterest;

    private static LazyPool<WanderMovementState> pool = new LazyPool<WanderMovementState>(() => new WanderMovementState());

    protected WanderMovementState() { }
    public static WanderMovementState Create(Targeting targeting, Func<MovementState, RectF?> curiosityPoint, Func<float> speed)
    {
        var state = pool.Value.Rent();
        state.Construct(targeting, curiosityPoint, speed);
        return state;
    }

    protected override void Construct(Targeting targeting, Func<MovementState, RectF?> curiosityPoint, Func<float> speed)
    {
        base.Construct(targeting, curiosityPoint, speed);
        Influence = new MotionInfluence();
        Weights = WanderWeights.Default;
        AngleScores = RecyclableListPool<AngleScore>.Instance.Rent();
        LastFewAngles = RecyclableListPool<Angle>.Instance.Rent();
        LastFewRoundedBounds = RecyclableListPool<RectF>.Instance.Rent();
        CloseEnough = 1;
        Velocity.AddInfluence(Influence);
        StuckCooldownTicks = 0;
        IsCurrentlyCloseEnoughToPointOfInterest = false;
    }

    protected override void OnReturn()
    {
        Velocity?.RemoveInfluence(Influence);
        base.OnReturn();
        LastFewAngles.TryDispose();
        LastFewAngles = null!;
        LastFewRoundedBounds.TryDispose();
        LastFewRoundedBounds = null!;
        AngleScores?.TryDispose();
        AngleScores = null!;
        Influence = null!;
        Weights = WanderWeights.Default;
        CloseEnough = 0;
        StuckCooldownTicks = 0;
        IsCurrentlyCloseEnoughToPointOfInterest = false;
    }
}





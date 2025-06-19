using System.Reflection;

namespace klooie.Gaming;

public class Wander : Movement
{
    private const float MaxCollisionHorizon = 1.25f;   // seconds – > this long = perfectly safe

    public MotionInfluence Influence { get; private set; }
    public RecyclableList<Angle> LastFewAngles;
    public RecyclableList<RectF> LastFewRoundedBounds;
    public WanderWeights Weights { get; set; } = WanderWeights.Default;
    public RecyclableList<AngleScore> AngleScores { get; private set; }
    public float CloseEnough { get; set; }
    public int StuckCooldownTicks = 0;
    public bool IsCurrentlyCloseEnoughToPointOfInterest;

    private static LazyPool<Wander> pool = new LazyPool<Wander>(() => new Wander());

    protected Wander() { }
    public static Wander Create(Vision vision, Func<Movement, RectF?> curiosityPoint, Func<float> speed, bool autoBindToVision = true)
    {
        var state = pool.Value.Rent();
        state.Construct(vision, curiosityPoint, speed, autoBindToVision);
        return state;
    }

    protected void Construct(Vision vision, Func<Movement, RectF?> curiosityPoint, Func<float> speed, bool autoBindToVision)
    {
        base.Construct(vision, curiosityPoint, speed);
        Influence = new MotionInfluence();
        Weights = WanderWeights.Default;
        AngleScores = RecyclableListPool<AngleScore>.Instance.Rent();
        LastFewAngles = RecyclableListPool<Angle>.Instance.Rent();
        LastFewRoundedBounds = RecyclableListPool<RectF>.Instance.Rent();
        CloseEnough = 1;
        Velocity.AddInfluence(Influence);
        StuckCooldownTicks = 0;
        IsCurrentlyCloseEnoughToPointOfInterest = false;
        if(autoBindToVision)
        {
            Vision.VisibleObjectsChanged.Subscribe(this, static (me) => me.Tick(), this);
        }
    }
    private void Tick()
    {
        var lease = Lease;
        if (AreAllDependenciesValid == false)
        {
            TryDispose(lease);
            return;
        }
        FrameDebugger.RegisterTask(nameof(Wander));
        var scores = AdjustSpeedAndVelocity();
        //onNewScoresAvailable?.Fire(scores.Items);
    }

    public  RecyclableList<AngleScore> AdjustSpeedAndVelocity()
    {
        ComputeScores();
        ConsiderEmergencyMode();
        // pick best angle
        AngleScore best = AngleScores[0];
        for (int i = 1; i < AngleScores.Count; i++)
        {
            if (AngleScores[i].Total > best.Total)
            {
                best = AngleScores[i];
            }
        }
        var angle = best.Angle;
        var speed = EvaluateSpeed( angle);

        Influence.Angle = angle;
        Influence.DeltaSpeed = speed;
#if DEBUG
        (this as WanderDebugger)?.ApplyMovingFreeFilter();
#endif


        // Update inertia history
        LastFewAngles.Items.Add(angle);
        if (LastFewAngles.Count > 10) LastFewAngles.Items.RemoveAt(0);

        var rounded = Eye.Bounds.Round();

        LastFewRoundedBounds.Items.Add(rounded);
        if (LastFewRoundedBounds.Count > 10)
            LastFewRoundedBounds.Items.RemoveAt(0);
#if DEBUG
        (this as WanderDebugger)?.RefreshLoopRunningFilter();
#endif

        return AngleScores;
    }

    // --- Private helpers -------------------------------------------------------------------

    private void ConsiderEmergencyMode()
    {
        return;
        if (StuckCooldownTicks > 0)
        {
            StuckCooldownTicks--;
            return;
        }

        // Emergency mode if stuck
        if (IsStuck())
        {
            StuckCooldownTicks = 5;
#if DEBUG
            (this as WanderDebugger)?.ApplyStuckFilter();
#endif
            var emergencyWeights = Weights;
            emergencyWeights.InertiaWeight = -0.2f;
            emergencyWeights.ForwardWeight = -0.2f;
            RescoreAnglesWithWeights(emergencyWeights);
        }
        else
        {
#if DEBUG
            (this as WanderDebugger)?.ClearStuckFilter();
#endif
        }
    }





    private void ComputeScores()
    {
        var inertiaAngle = AverageAngle(LastFewAngles.Items, Velocity.Angle);

        Angle? curiosityAngle = null;
        if (CuriosityPoint != null)
        {
            var target = CuriosityPoint(this);
            if (target.HasValue)
                curiosityAngle = Eye.CalculateAngleTo(target.Value);
        }

        AngleScores.Items.Clear();
        var totalAngularTravel = 180f;
        float travelCompleted = 0f;
        var travelPerStep = 12f;
        ScoreAngle(inertiaAngle, curiosityAngle, Velocity.Angle); // score the current angle first
        while (travelCompleted < totalAngularTravel)
        {
            var leftCandidate = Velocity.Angle.Add(-(travelPerStep + travelCompleted));
            var rightCandidate = Velocity.Angle.Add(travelPerStep + travelCompleted);
            travelCompleted += travelPerStep;

            ScoreAngle(inertiaAngle, curiosityAngle, leftCandidate);
            ScoreAngle(inertiaAngle, curiosityAngle, rightCandidate);
        }
    }

    private void ScoreAngle(Angle inertiaAngle, Angle? curiosityAngle, Angle candidate)
    {
        // Compute raw components
        float rawCollision = PredictCollision(candidate);
        float rawInertia = candidate.DiffShortest(inertiaAngle);
        float rawForward = candidate.DiffShortest(Velocity.Angle);
        float rawCuriosity = curiosityAngle.HasValue ? candidate.DiffShortest(curiosityAngle.Value) : Vision.AngularVisibility;

        // Normalise [0,1]
        float normCollision = DangerClamp(rawCollision);
        float normInertia = 1f - Clamp01(rawInertia / 180);            // smaller deviation -> better
        float normForward = 1f - Clamp01(rawForward / 90);
        float normCuriosity = 1f - Clamp01(rawCuriosity / 180);

        float inertiaWeight = Weights.InertiaWeight;
        float curiosityWeight = Weights.CuriosityWeight;
        if (LastFewAngles.Count == 0)
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
            CollisionWeight = Weights.CollisionWeight,
            InertiaWeight = inertiaWeight,
            ForwardWeight = Weights.ForwardWeight,
            CuriosityWeight = curiosityWeight
        });
        AngleScores.Items.Add(score);
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


    private void RescoreAnglesWithWeights(WanderWeights weights)
    {
        for (int i = 0; i < AngleScores.Count; i++)
        {
            var prev = AngleScores[i];
            AngleScores[i] = new AngleScore(
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
    private bool IsStuck(int window = 5, int maxUnique = 4)
    {
        if (LastFewRoundedBounds.Count < window) return false;
    
        for (int i = LastFewRoundedBounds.Count - window; i < LastFewRoundedBounds.Count; i++)
        {
            sharedUniqueModeHashSet.Add(LastFewRoundedBounds[i]);
        }
        var ret = sharedUniqueModeHashSet.Count <= maxUnique;
        sharedUniqueModeHashSet.Clear();
        return ret;
    }

    private float EvaluateSpeed(Angle chosenAngle)
    {
        if (Speed() == 0) throw new Exception("Zero Speed Yo");
        var pointOfInterest = CuriosityPoint == null ? null : CuriosityPoint.Invoke(this);
        IsCurrentlyCloseEnoughToPointOfInterest = pointOfInterest.HasValue && Eye.Bounds.CalculateNormalizedDistanceTo(pointOfInterest.Value) <= CloseEnough;
        return IsCurrentlyCloseEnoughToPointOfInterest ? 0 : Speed();
    }

    private float PredictCollision(Angle angle)
    {
        var buffer = ObstacleBufferPool.Instance.Rent();
        var prediction = CollisionPredictionPool.Instance.Rent();
        try
        {
            // Gather nearby obstacles (already tracked by vision)
            var tracked = Vision.TrackedObjectsList;
            for (int i = 0; i < tracked.Count; i++)
                buffer.WriteableBuffer.Add(tracked[i].Target);

            CollisionDetector.Predict(
                Eye,
                angle,
                buffer.WriteableBuffer,
                Vision.Range,
                CastingMode.Rough,
                buffer.WriteableBuffer.Count,
                prediction);

            if (!prediction.CollisionPredicted)
                return MaxCollisionHorizon; // safe path

            return (Velocity.Speed > 0)
             ? prediction.LKGD / Velocity.Speed
             : float.MaxValue;
        }
        finally
        {
            buffer.Dispose();
            prediction.Dispose();
        }
    }

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

    protected override void OnReturn()
    {
        Eye?.Velocity?.RemoveInfluence(Influence);
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

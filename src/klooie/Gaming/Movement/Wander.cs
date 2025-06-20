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

    protected bool IsStuck { get; private set; } 

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
        Influence = new MotionInfluence() { Name = "Wander Influence", IsExclusive = true, };
        Weights = WanderWeights.Default;
        AngleScores = RecyclableListPool<AngleScore>.Instance.Rent();
        LastFewAngles = RecyclableListPool<Angle>.Instance.Rent();
        LastFewRoundedBounds = RecyclableListPool<RectF>.Instance.Rent();
        CloseEnough = 1;
        Velocity.AddInfluence(Influence);
        IsStuck = false; // Reset stuck state
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
        var speed = EvaluateSpeed(angle, best.Total);

        Influence.Angle = angle;
        Influence.DeltaSpeed = speed;
#if DEBUG

        if(Velocity.ContainsInfluence(Influence) == false) throw new InvalidOperationException("Wander Influence not found in Velocity influences. This is a bug in the code, please report it.");
        (this as WanderDebugger)?.HighlightAngle("BestAngle", () => angle, RGB.Green);
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
        // Emergency mode if stuck
        if (CalculateIsStuck())
        {
            IsStuck = true;
#if DEBUG
            (this as WanderDebugger)?.ApplyStuckFilter();
#endif
            LastFewAngles.Items.Clear();
            LastFewRoundedBounds.Items.Clear();
        }
        else
        {
            IsStuck = false;
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
            {
                curiosityAngle = Eye.CalculateAngleTo(target.Value);
#if DEBUG
                (this as WanderDebugger)?.HighlightAngle("CuriosityAngle", () => curiosityAngle, RGB.Magenta);
#endif
            }
            else
            {
#if DEBUG
                (this as WanderDebugger)?.HighlightAngle("CuriosityAngle", () => null, RGB.Magenta);
#endif
            }
        }

        AngleScores.Items.Clear();
        var totalAngularTravel = 180f;
        float travelCompleted = 0f;

        Angle baseAngle = Velocity.Angle;
        if (curiosityAngle.HasValue && !IsCurrentlyCloseEnoughToPointOfInterest)
        {
            baseAngle = curiosityAngle.Value;
        }

        ScoreAngle(inertiaAngle, curiosityAngle, baseAngle); // score the current angle first

        float travelPerStep = 12f;
        travelPerStep = 6f + (18f - 6f) * (AngleScores.Count > 0 ? AngleScores[0].Total : 0.5f);

        while (travelCompleted < totalAngularTravel)
        {
            var leftCandidate = baseAngle.Add(-(travelPerStep + travelCompleted));
            var rightCandidate = baseAngle.Add(travelPerStep + travelCompleted);
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
        float repulsionPenalty = 1f;
        var predictedPos = PredictRoundedPosition(candidate);
        for (int i = 0; i < LastFewRoundedBounds.Count; i++)
        {
            if (LastFewRoundedBounds[i].Equals(predictedPos))
            {
                repulsionPenalty = 0.75f; // Tweak as needed
                break;
            }
        }
        normCollision *= repulsionPenalty;
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

    // We are single threaded so it's safe
    private static HashSet<RectF> sharedUniqueModeHashSet = new HashSet<RectF>();
    private bool CalculateIsStuck(int window = 5, int maxUnique = 4)
    {
        if (IsCurrentlyCloseEnoughToPointOfInterest) return false;
        if (LastFewRoundedBounds.Count < window) return false;
    
        for (int i = LastFewRoundedBounds.Count - window; i < LastFewRoundedBounds.Count; i++)
        {
            sharedUniqueModeHashSet.Add(LastFewRoundedBounds[i]);
        }
        var ret = sharedUniqueModeHashSet.Count <= maxUnique;
        sharedUniqueModeHashSet.Clear();
        return ret;
    }

    private float EvaluateSpeed(Angle chosenAngle, float confidence)
    {
        if (Speed() == 0) throw new Exception("Zero Speed Yo");
        var pointOfInterest = CuriosityPoint == null ? null : CuriosityPoint.Invoke(this);
        IsCurrentlyCloseEnoughToPointOfInterest = pointOfInterest.HasValue && Eye.Bounds.CalculateNormalizedDistanceTo(pointOfInterest.Value) <= CloseEnough;

        if (IsCurrentlyCloseEnoughToPointOfInterest) return 0;

        float minFactor = 0.2f;
        return Speed() * (minFactor + (1f - minFactor) * Clamp01(confidence));
    }

    private RectF PredictRoundedPosition(Angle angle)
    {

        var futurePosition = Eye.Bounds.RadialOffset(angle, Eye.Bounds.Hypotenous);
        return futurePosition.Round();
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

        float totalWeight = 0f;
        float weightedSum = 0f;
        for (int i = 0; i < list.Count; i++)
        {
            float weight = (i + 1); // More recent angles = heavier
            weightedSum += list[i].Value * weight;
            totalWeight += weight;
        }
        return new Angle(weightedSum / totalWeight);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    protected override void OnReturn()
    {
        if(Eye != null && Eye.Velocity != null && Eye.Velocity.ContainsInfluence(Influence) == false)
        {
            throw new InvalidOperationException($"Wander Influence not found in Eye.Velocity influences. This is a bug in the code, please report it.");
        }
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

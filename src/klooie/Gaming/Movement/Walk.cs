using System.Reflection;

namespace klooie.Gaming;

public class Walk : Movement
{
    private const float MaxCollisionHorizon = 1.25f;   // seconds – > this long = perfectly safe
    private const int MaxHistory = 10; // How many angles to keep in history for inertia calculation
    public MotionInfluence Influence { get; private set; }
    public RecyclableList<Angle> LastFewAngles;
    public RecyclableList<RectF> LastFewRoundedBounds;
    public WanderWeights Weights { get; set; } = WanderWeights.Default;
    public RecyclableList<AngleScore> AngleScores { get; private set; }
    public float CloseEnough { get; set; }

    protected bool IsStuck { get; private set; } 

    public bool IsCurrentlyCloseEnoughToPointOfInterest;

    private static LazyPool<Walk> pool = new LazyPool<Walk>(() => new Walk());

    private RectF? currentPointOfInterest;

    public virtual RectF? GetPointOfInterest() => null;
    protected Walk() { }
    public static Walk Create(Vision vision, float baseSpeed, bool autoBindToVision = true)
    {
        var state = pool.Value.Rent();
        state.Construct(vision, baseSpeed, autoBindToVision);
        return state;
    }

    protected void Construct(Vision vision, float baseSpeed, bool autoBindToVision)
    {
        base.Construct(vision, baseSpeed);
        Influence = MotionInfluence.Create("Wander Influence", true);
        Weights = WanderWeights.Default;
        AngleScores = RecyclableListPool<AngleScore>.Instance.Rent(100);
        LastFewAngles = RecyclableListPool<Angle>.Instance.Rent(20);
        LastFewRoundedBounds = RecyclableListPool<RectF>.Instance.Rent(20);
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
        if (AreAllDependenciesValid == false)
        {
            TryDispose();
            return;
        }
        FrameDebugger.RegisterTask(nameof(Walk));
        var scores = AdjustSpeedAndVelocity(out _);
    }

    public  RecyclableList<AngleScore> AdjustSpeedAndVelocity(out WanderWeights weights)
    {
        currentPointOfInterest = GetPointOfInterest();
        ComputeScores();
        weights = OptimizeWeights();
        ConsiderEmergencyMode();
        // pick best angle
        AngleScore best = AngleScores[0];
        for (int i = 1; i < AngleScores.Count; i++)
        {
            if (AngleScores[i].GetTotal(weights) > best.GetTotal(weights))
            {
                best = AngleScores[i];
            }
        }
        var angle = best.Angle;
        var speed = EvaluateSpeed(angle, best.GetTotal(weights));
        Influence.Angle = angle;
        Influence.DeltaSpeed = speed;
#if DEBUG

        if (Velocity.ContainsInfluence(Influence) == false) throw new InvalidOperationException("Wander Influence not found in Velocity influences. This is a bug in the code, please report it.");
        (this as DebuggableWalk)?.HighlightAngle("BestAngle", () => angle, RGB.Green);
        (this as DebuggableWalk)?.ApplyMovingFreeFilter();
#endif


        // Update inertia history
        LastFewAngles.Items.Add(angle);
        if (LastFewAngles.Count > MaxHistory) LastFewAngles.Items.RemoveAt(0);

        var rounded = Eye.Bounds.Round();
        LastFewRoundedBounds.Items.Add(rounded);
        if (LastFewRoundedBounds.Count > MaxHistory) LastFewRoundedBounds.Items.RemoveAt(0);
#if DEBUG
        (this as DebuggableWalk)?.RefreshLoopRunningFilter();
#endif

        return AngleScores;
    }

    private WanderWeights OptimizeWeights()
    {
        WanderWeights weights;
        float minCollision = float.MaxValue, maxCollision = float.MinValue, sumCollision = 0f;
        for (int i = 0; i < AngleScores.Count; i++)
        {
            float c = AngleScores[i].Collision;
            if (c < minCollision) minCollision = c;
            if (c > maxCollision) maxCollision = c;
            sumCollision += c;
        }
        float avgCollision = sumCollision / AngleScores.Count;
        float collisionSpread = maxCollision - minCollision;

        weights = Weights; 

        if(currentPointOfInterest.HasValue == false)
        {
            weights.PointOfInterestWeight = 0f; // No point of interest
        }

        if (minCollision > 0.8f && collisionSpread < 0.1f)
        {
            // if there is little risk of collision then reduce its weight
            weights.CollisionWeight = 0f;
            weights.PointOfInterestWeight *= 2;
        }

        weights.InertiaWeight = weights.InertiaWeight * LastFewAngles.Count / (float)MaxHistory; // More inertia if we have more history
        return weights;
    }

    // --- Private helpers -------------------------------------------------------------------

    private void ConsiderEmergencyMode()
    {
        // Emergency mode if stuck
        if (CalculateIsStuck())
        {
            IsStuck = true;
#if DEBUG
            (this as DebuggableWalk)?.ApplyStuckFilter();
#endif
            LastFewAngles.Items.Clear();
            LastFewRoundedBounds.Items.Clear();
        }
        else
        {
            IsStuck = false;
#if DEBUG
            (this as DebuggableWalk)?.ClearStuckFilter();
#endif
        }
    }





    private void ComputeScores()
    {
        var inertiaAngle = AverageAngle(LastFewAngles.Items, Velocity.Angle);

        Angle? pointOfInterestAngle = null;

        if (currentPointOfInterest.HasValue)
        {
            pointOfInterestAngle = Eye.CalculateAngleTo(currentPointOfInterest.Value);
#if DEBUG
            (this as DebuggableWalk)?.HighlightAngle("PointOfInterestAngle", () => pointOfInterestAngle, RGB.Magenta);
#endif
        }
        else
        {
#if DEBUG
            (this as DebuggableWalk)?.HighlightAngle("PointOfInterestAngle", () => null, RGB.Magenta);
#endif
        }
        

        AngleScores.Items.Clear();
        var totalAngularTravel = 225f;
        float travelCompleted = 0f;

        Angle baseAngle = Velocity.Angle;
        if (pointOfInterestAngle.HasValue && !IsCurrentlyCloseEnoughToPointOfInterest)
        {
            baseAngle = pointOfInterestAngle.Value;
        }

        ScoreAngle(inertiaAngle, pointOfInterestAngle, baseAngle); // score the current angle first

        float travelPerStep = 2f;

        while (travelCompleted < totalAngularTravel)
        {
            var leftCandidate = baseAngle.Add(-(travelPerStep + travelCompleted));
            var rightCandidate = baseAngle.Add(travelPerStep + travelCompleted);
            travelCompleted += travelPerStep;

            ScoreAngle(inertiaAngle, pointOfInterestAngle, leftCandidate);
            ScoreAngle(inertiaAngle, pointOfInterestAngle, rightCandidate);
        }
    }

    private void ScoreAngle(Angle inertiaAngle, Angle? pointOfInterestAngle, Angle candidate)
    {
        // Compute raw components
        float rawCollision = PredictCollision(candidate);
        float rawInertia = candidate.DiffShortest(inertiaAngle);
        float rawForward = candidate.DiffShortest(Velocity.Angle);
        float rawPointOfInterest = pointOfInterestAngle.HasValue ? candidate.DiffShortest(pointOfInterestAngle.Value) : Vision.AngularVisibility;

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
        float normPointOfInterest = 1f - Clamp01(rawPointOfInterest / 180);

        var score = new AngleScore(candidate, normCollision, normInertia, normForward, normPointOfInterest);
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
    private bool CalculateIsStuck(int window = 5, int maxUnique = 3)
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
        if (BaseSpeed == 0) throw new Exception("Zero Speed Yo");
        IsCurrentlyCloseEnoughToPointOfInterest = currentPointOfInterest.HasValue && Eye.Bounds.CalculateNormalizedDistanceTo(currentPointOfInterest.Value) <= CloseEnough;

        if (IsCurrentlyCloseEnoughToPointOfInterest) return 0;

        float minFactor = 0.2f;
        return BaseSpeed * (minFactor + (1f - minFactor) * Clamp01(confidence));
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
                CastingMode.Precise,
                buffer.WriteableBuffer.Count,
                prediction);

            if (!prediction.CollisionPredicted)
                return MaxCollisionHorizon; // safe path

            return Velocity.Speed > 0 ? prediction.LKGD / Velocity.Speed : 0;
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
        Influence.Dispose();
        Influence = null!;
        base.OnReturn();
        LastFewAngles.TryDispose();
        LastFewAngles = null!;
        LastFewRoundedBounds.TryDispose();
        LastFewRoundedBounds = null!;
        AngleScores?.TryDispose();
        AngleScores = null!;
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
    public readonly float Collision, Inertia, Forward, PointOfInterest;
    public AngleScore(Angle angle, float collision, float inertia, float forward, float pointOfInterest)
    {
        Angle = angle;
        Collision = collision;
        Inertia = inertia;
        Forward = forward;
        PointOfInterest = pointOfInterest;
    }
    public float GetTotal(WanderWeights w)
    {
        float sumWeights = w.CollisionWeight + w.InertiaWeight + w.ForwardWeight + w.PointOfInterestWeight;
        if (sumWeights <= 0f) sumWeights = 1f;
        return (
            Collision * w.CollisionWeight +
            Inertia * w.InertiaWeight +
            Forward * w.ForwardWeight +
            PointOfInterest * w.PointOfInterestWeight
        ) / sumWeights;
    }

    public override string ToString()
    {
        return $"Angle: {Angle}, Collision: {Collision}, Inertia: {Inertia}, Forward: {Forward}, PointOfInterest: {PointOfInterest}";
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
    public float PointOfInterestWeight;

    public static readonly WanderWeights Default = new WanderWeights
    {
        CollisionWeight = 2.5f,
        InertiaWeight = 0.1f,
        ForwardWeight = 0.05f,
        PointOfInterestWeight = 0.3f
    };
}

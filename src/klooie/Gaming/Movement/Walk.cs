using System.Runtime.CompilerServices;

namespace klooie.Gaming;

public class Walk : Movement
{
    // -------------------- Tunables / Perf knobs --------------------
    private const float MaxCollisionHorizon = 1.25f;   // seconds – > this long = perfectly safe
    private const int MaxHistory = 10;                 // inertia memory

    // Angle sampling: coarse -> refine
    private const float TotalAngularTravelDeg = 225f;  // search half-cone to each side of base
    private const float CoarseStepDeg = 30f;           // coarse stride
    private const int CoarseRefineTopK = 2;            // how many coarse peaks to refine
    private const float FineSpanDeg = 24f;             // refine window around a coarse peak
    private const float FineStepDeg = 2f;              // refine stride

    // DangerClamp constants (precompute reciprocal to avoid divides)
    private const float DangerClampDanger = 0.2f;
    private static readonly float DangerClampScale = 1f / (MaxCollisionHorizon - DangerClampDanger);

    // Cached reciprocals for normalization
    private const float Inv180 = 1f / 180f;
    private const float Inv090 = 1f / 90f;

    public virtual bool FearCollision(GameCollider collider) => true;

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
    private RectF? currentHazard;

    public virtual RectF? GetPointOfInterest() => null;

    // NEW: optional hazard (e.g., threat, trap, explosion)
    public virtual RectF? GetHazard() => null;

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
        IsStuck = false;
        IsCurrentlyCloseEnoughToPointOfInterest = false;

        if (autoBindToVision)
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
        _ = AdjustSpeedAndVelocity(out _);
    }

    public RecyclableList<AngleScore> AdjustSpeedAndVelocity(out WanderWeights weights)
    {
        currentPointOfInterest = GetPointOfInterest();
        currentHazard = GetHazard();

        // Populate AngleScores using the optimized scorer
        ComputeScores();

        // Dynamic reweighting stays as-is, but now includes hazard logic
        weights = OptimizeWeights();

        ConsiderEmergencyMode();

        // Pick the best angle using the (possibly adjusted) weights
        AngleScore best = AngleScores[0];
        float bestTotal = best.GetTotal(weights);
        for (int i = 1, n = AngleScores.Count; i < n; i++)
        {
            float t = AngleScores[i].GetTotal(weights);
            if (t > bestTotal)
            {
                best = AngleScores[i];
                bestTotal = t;
            }
        }

        var angle = best.Angle;
        var speed = EvaluateSpeed(angle, bestTotal);
        Influence.Angle = angle;
        Influence.DeltaSpeed = speed;

#if DEBUG
        if (Velocity.ContainsInfluence(Influence) == false)
            throw new InvalidOperationException("Wander Influence not found in Velocity influences. This is a bug in the code, please report it.");
        (this as DebuggableWalk)?.HighlightAngle("BestAngle", () => angle, RGB.Green);
        (this as DebuggableWalk)?.ApplyMovingFreeFilter();
        (this as DebuggableWalk)?.RefreshLoopRunningFilter();
#endif

        // Update inertia history (simple ring behavior)
        LastFewAngles.Items.Add(angle);
        if (LastFewAngles.Count > MaxHistory) LastFewAngles.Items.RemoveAt(0);

        var rounded = Eye.Bounds.Round();
        LastFewRoundedBounds.Items.Add(rounded);
        if (LastFewRoundedBounds.Count > MaxHistory) LastFewRoundedBounds.Items.RemoveAt(0);

        return AngleScores;
    }

    private WanderWeights OptimizeWeights()
    {
        WanderWeights weights;

        float minC = float.MaxValue, maxC = float.MinValue, sumC = 0f;
        for (int i = 0, n = AngleScores.Count; i < n; i++)
        {
            float c = AngleScores[i].Collision; // normalized [0..1]
            if (c < minC) minC = c;
            if (c > maxC) maxC = c;
            sumC += c;
        }
        float avgC = sumC / AngleScores.Count;
        float spread = maxC - minC;

        weights = Weights;

        if (!currentPointOfInterest.HasValue)
            weights.PointOfInterestWeight = 0f;

        // NEW: disable hazard weight if there’s no hazard
        if (!currentHazard.HasValue)
            weights.HazardWeight = 0f;

        if (minC > 0.8f && spread < 0.1f)
        {
            // Low risk everywhere -> collision weight can relax a lot
            weights.CollisionWeight = 0f;
            weights.PointOfInterestWeight *= 2;
            // Keep hazard weight as-is if present; still want to avoid it.
        }

        // Scale inertia by how much history we have
        weights.InertiaWeight = weights.InertiaWeight * LastFewAngles.Count / (float)MaxHistory;
        _ = avgC; // reserved for future heuristics if you want
        return weights;
    }

    // -------------------- Hot path scoring (optimized) --------------------

    private void ComputeScores()
    {
        AngleScores.Items.Clear();

        // Precompute inertial, POI, and hazard angles as degrees to avoid Angle ops in inner loops
        float inertiaDeg = AverageAngle(LastFewAngles.Items, Velocity.Angle).Value;
        float? poiDeg = currentPointOfInterest.HasValue ? Eye.CalculateAngleTo(currentPointOfInterest.Value).Value : (float?)null;
        float? hazardDeg = currentHazard.HasValue ? Eye.CalculateAngleTo(currentHazard.Value).Value : (float?)null;

#if DEBUG
        (this as DebuggableWalk)?.HighlightAngle(
            "PointOfInterestAngle",
            () => currentPointOfInterest.HasValue ? new Angle?(new Angle(poiDeg!.Value)) : null,
            RGB.Magenta);
        (this as DebuggableWalk)?.HighlightAngle(
            "HazardAngle",
            () => currentHazard.HasValue ? new Angle?(new Angle(hazardDeg!.Value)) : null,
            RGB.Red);
#endif

        // Base angle: toward POI unless we’re already close; otherwise away from hazard if present; else keep velocity
        float baseDeg = Velocity.Angle.Value;

        bool closeToPoi = currentPointOfInterest.HasValue &&
                          Eye.Bounds.CalculateNormalizedDistanceTo(currentPointOfInterest.Value) <= CloseEnough;

        if (poiDeg.HasValue && !closeToPoi)
            baseDeg = poiDeg.Value;
        else if (hazardDeg.HasValue)
            baseDeg = WrapDeg(hazardDeg.Value + 180f);

        // Reuse one obstacles buffer across all candidates (huge win)
        var buffer = ObstacleBufferPool.Instance.Rent();
        var tracked = Vision.TrackedObjectsList;
        var obstacles = buffer.WriteableBuffer;
        for (int i = 0, n = tracked.Count; i < n; i++)
        {
            if (FearCollision(tracked[i].Target) == false) continue;
            obstacles.Add(tracked[i].Target);
        }

        // Reuse one CollisionPrediction object across all candidates (overwrite each time)
        var prediction = CollisionPredictionPool.Instance.Rent();

        try
        {
            // 1) Score the base angle first (coarse, no repulsion)
            ScoreAngle_NoRepulsion(inertiaDeg, poiDeg, hazardDeg, baseDeg, obstacles, prediction);

            // 2) Coarse sweep around base; keep top-K coarse picks
            Span<CoarseTop> topAngles = stackalloc CoarseTop[GetTopSize(CoarseRefineTopK)];
            float travelCompleted = 0f;

            while (travelCompleted < TotalAngularTravelDeg)
            {
                float delta = travelCompleted + CoarseStepDeg;
                travelCompleted += CoarseStepDeg;

                float leftDeg = WrapDeg(baseDeg - delta);
                float rightDeg = WrapDeg(baseDeg + delta);

                float leftScore = ScoreAngle_NoRepulsion(inertiaDeg, poiDeg, hazardDeg, leftDeg, obstacles, prediction);
                float rightScore = ScoreAngle_NoRepulsion(inertiaDeg, poiDeg, hazardDeg, rightDeg, obstacles, prediction);

                TryPushTop(topAngles, leftDeg, leftScore);
                TryPushTop(topAngles, rightDeg, rightScore);
            }

            // 3) Refine around the best coarse angles (including base)
            TryPushTop(topAngles, baseDeg, 0); // ensure base gets refined too

            // Refine window half-span
            float halfSpan = FineSpanDeg * 0.5f;

            // Avoid refining near-duplicate centers
            Span<float> refinedCenters = stackalloc float[CoarseRefineTopK + 1];
            int refinedCount = 0;

            for (int i = 0; i < topAngles.Length; i++)
            {
                if (!topAngles[i].Valid) continue;
                float center = topAngles[i].AngleDeg;

                bool dup = false;
                for (int j = 0; j < refinedCount; j++)
                {
                    if (AbsShortestDiff(center, refinedCenters[j]) < FineStepDeg) { dup = true; break; }
                }
                if (dup) continue;

                refinedCenters[refinedCount++] = center;

                // Fine sweep with repulsion check ON
                for (float d = -halfSpan; d <= halfSpan; d += FineStepDeg)
                {
                    float cand = WrapDeg(center + d);
                    ScoreAngle_WithRepulsion(inertiaDeg, poiDeg, hazardDeg, cand, obstacles, prediction);
                }
            }
        }
        finally
        {
            prediction.Dispose();
            buffer.Dispose();
        }
    }

    // --- Two-stage scoring helpers ----------------------------------------------------------

    // Coarse: skip repulsion penalty (no RadialOffset call).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ScoreAngle_NoRepulsion(float inertiaDeg, float? poiDeg, float? hazardDeg, float candidateDeg,
                                         List<GameCollider> obstacles,
                                         CollisionPrediction prediction)
    {
        float normCollision = DangerClamp(PredictCollision(candidateDeg, obstacles, prediction));
        float normInertia = 1f - Clamp01(AbsShortestDiff(candidateDeg, inertiaDeg) * Inv180);
        float normForward = 1f - Clamp01(AbsShortestDiff(candidateDeg, Velocity.Angle.Value) * Inv090);
        float normPoi = 1f - Clamp01(AbsShortestDiff(candidateDeg, poiDeg.HasValue ? poiDeg.Value : Vision.AngularVisibility) * Inv180);

        // NEW: prefer angles AWAY from the hazard (0 when pointing at it, 1 when 180° away)
        float normHazard = hazardDeg.HasValue
            ? Clamp01(AbsShortestDiff(candidateDeg, hazardDeg.Value) * Inv180)
            : 1f;

        var score = new AngleScore(new Angle(candidateDeg), normCollision, normInertia, normForward, normPoi, normHazard);
        AngleScores.Items.Add(score);

        // Return an unweighted composite to pick coarse peaks quickly.
        return (normCollision + normInertia + normForward + normPoi + normHazard);
    }

    // Fine: include repulsion (predict future position, rounded), only for shortlisted angles.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ScoreAngle_WithRepulsion(float inertiaDeg, float? poiDeg, float? hazardDeg, float candidateDeg,
                                           List<GameCollider> obstacles,
                                           CollisionPrediction prediction)
    {
        float normCollision = DangerClamp(PredictCollision(candidateDeg, obstacles, prediction));

        float repulsionPenalty = 1f;
        var predictedPos = PredictRoundedPosition(new Angle(candidateDeg));
        for (int i = 0, n = LastFewRoundedBounds.Count; i < n; i++)
        {
            if (LastFewRoundedBounds[i].Equals(predictedPos))
            {
                repulsionPenalty = 0.75f; // same as before
                break;
            }
        }
        normCollision *= repulsionPenalty;

        float normInertia = 1f - Clamp01(AbsShortestDiff(candidateDeg, inertiaDeg) * Inv180);
        float normForward = 1f - Clamp01(AbsShortestDiff(candidateDeg, Velocity.Angle.Value) * Inv090);
        float normPoi = 1f - Clamp01(AbsShortestDiff(candidateDeg, poiDeg.HasValue ? poiDeg.Value : Vision.AngularVisibility) * Inv180);

        // NEW: away-from-hazard component
        float normHazard = hazardDeg.HasValue
            ? Clamp01(AbsShortestDiff(candidateDeg, hazardDeg.Value) * Inv180)
            : 1f;

        var score = new AngleScore(new Angle(candidateDeg), normCollision, normInertia, normForward, normPoi, normHazard);
        AngleScores.Items.Add(score);

        return (normCollision + normInertia + normForward + normPoi + normHazard);
    }

    // Predicts time-to-collision in seconds (capped by MaxCollisionHorizon)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float PredictCollision(float angleDeg, List<GameCollider> obstacles, CollisionPrediction prediction)
    {
        // Single heavy call; reuse 'prediction' each time.
        CollisionDetector.Predict(
            Eye,
            new Angle(angleDeg),
            obstacles,
            Vision.Range,
            CastingMode.Precise,
            obstacles.Count,
            prediction);

        if (!prediction.CollisionPredicted)
            return MaxCollisionHorizon;

        float speed = Velocity.Speed;
        if (speed <= 0f) return 0f;

        // time = distance / speed
        return prediction.LKGD / speed;
    }

    private RectF PredictRoundedPosition(Angle angle)
    {
        var futurePosition = Eye.Bounds.RadialOffset(angle, Eye.Bounds.Hypotenous);
        return futurePosition.Round();
    }

    // -------------------- Misc logic (unchanged behavior) --------------------

    private void ConsiderEmergencyMode()
    {
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

    private static HashSet<RectF> sharedUniqueModeHashSet = new HashSet<RectF>();
    private bool CalculateIsStuck(int window = 5, int maxUnique = 3)
    {
        if (IsCurrentlyCloseEnoughToPointOfInterest) return false;
        if (LastFewRoundedBounds.Count < window) return false;

        for (int i = LastFewRoundedBounds.Count - window; i < LastFewRoundedBounds.Count; i++)
            sharedUniqueModeHashSet.Add(LastFewRoundedBounds[i]);

        bool ret = sharedUniqueModeHashSet.Count <= maxUnique;
        sharedUniqueModeHashSet.Clear();
        return ret;
    }

    private float EvaluateSpeed(Angle chosenAngle, float confidence)
    {
        if (BaseSpeed == 0) throw new Exception("Zero Speed Yo");

        IsCurrentlyCloseEnoughToPointOfInterest =
            currentPointOfInterest.HasValue &&
            Eye.Bounds.CalculateNormalizedDistanceTo(currentPointOfInterest.Value) <= CloseEnough;

        if (IsCurrentlyCloseEnoughToPointOfInterest) return 0;

        const float minFactor = 0.2f;
        return BaseSpeed * (minFactor + (1f - minFactor) * Clamp01(confidence));
    }

    private static Angle AverageAngle(List<Angle> list, Angle fallback)
    {
        if (list.Count == 0) return fallback;

        float totalWeight = 0f;
        float weightedSum = 0f;
        for (int i = 0, n = list.Count; i < n; i++)
        {
            float w = (i + 1);
            weightedSum += list[i].Value * w;
            totalWeight += w;
        }
        return new Angle(weightedSum / totalWeight);
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DangerClamp(float timeToCollision)
    {
        if (timeToCollision >= MaxCollisionHorizon) return 1f;
        if (timeToCollision <= DangerClampDanger) return 0f;
        float t = (timeToCollision - DangerClampDanger) * DangerClampScale;
        return t < 0f ? 0f : (t > 1f ? 1f : t);
    }

    // Fast wrap to [0,360)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float WrapDeg(float d)
    {
        d %= 360f;
        if (d < 0f) d += 360f;
        return d;
    }

    // Absolute shortest angular difference in degrees
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float AbsShortestDiff(float a, float b)
    {
        float d = a - b;
        d %= 360f;
        if (d < -180f) d += 360f; else if (d > 180f) d -= 360f;
        return d < 0f ? -d : d;
    }

    protected override void OnReturn()
    {
        if (Eye != null && Eye.Velocity != null && Eye.Velocity.ContainsInfluence(Influence) == false)
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
        currentPointOfInterest = null;
        currentHazard = null;
    }

    private struct CoarseTop
    {
        public float AngleDeg;
        public float Score;
        public bool Valid;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int GetTopSize(int k) => k switch
    {
        1 => 1,
        2 => 2,
        3 => 3,
        4 => 4,
        _ => 5 // cap; adjust if you increase CoarseRefineTopK
    };

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void TryPushTop(Span<CoarseTop> top, float angleDeg, float score)
    {
        // If any slot is empty, fill it
        for (int i = 0; i < top.Length; i++)
        {
            if (!top[i].Valid)
            {
                top[i].AngleDeg = angleDeg;
                top[i].Score = score;
                top[i].Valid = true;
                return;
            }
        }

        // Replace the worst if this is better
        int worstIdx = 0;
        float worstScore = float.MaxValue;
        for (int i = 0; i < top.Length; i++)
        {
            if (top[i].Score < worstScore)
            {
                worstScore = top[i].Score;
                worstIdx = i;
            }
        }

        if (score > worstScore)
        {
            top[worstIdx].AngleDeg = angleDeg;
            top[worstIdx].Score = score;
            top[worstIdx].Valid = true;
        }
    }
}

/// <summary>
/// Normalised component-by-component score for a single candidate steering angle.
/// Every field is mapped to [0,1] where 1 = "best" and 0 = "worst".
/// </summary>
public readonly struct AngleScore
{
    public readonly Angle Angle;
    public readonly float Collision, Inertia, Forward, PointOfInterest, Hazard;
    public AngleScore(Angle angle, float collision, float inertia, float forward, float pointOfInterest, float hazard)
    {
        Angle = angle;
        Collision = collision;
        Inertia = inertia;
        Forward = forward;
        PointOfInterest = pointOfInterest;
        Hazard = hazard;
    }
    public float GetTotal(WanderWeights w)
    {
        float sumW = w.CollisionWeight + w.InertiaWeight + w.ForwardWeight + w.PointOfInterestWeight + w.HazardWeight;
        if (sumW <= 0f) sumW = 1f;
        return (
            Collision * w.CollisionWeight +
            Inertia * w.InertiaWeight +
            Forward * w.ForwardWeight +
            PointOfInterest * w.PointOfInterestWeight +
            Hazard * w.HazardWeight
        ) / sumW;
    }

    public override string ToString()
    {
        return $"Angle: {Angle}, Collision: {Collision}, Inertia: {Inertia}, Forward: {Forward}, PointOfInterest: {PointOfInterest}, Hazard: {Hazard}";
    }
}

/// <summary>
/// Tunable weighting for <see cref="AngleScore"/> components.
/// </summary>
public struct WanderWeights
{
    public float CollisionWeight;
    public float InertiaWeight;
    public float ForwardWeight;
    public float PointOfInterestWeight;
    public float HazardWeight; // NEW

    public static readonly WanderWeights Default = new WanderWeights
    {
        CollisionWeight = 2.5f,
        InertiaWeight = 0.1f,
        ForwardWeight = 0.05f,
        PointOfInterestWeight = 0.3f,
        HazardWeight = 0.8f
    };
}

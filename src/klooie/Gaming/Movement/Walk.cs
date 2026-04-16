using System.Runtime.CompilerServices;

namespace klooie.Gaming;

public class Walk : Movement
{
    private static LazyPool<Walk> pool = new LazyPool<Walk>(() => new Walk());
    protected Walk() { }
    private WalkCalculationState state = new WalkCalculationState();
    private MotionInfluence influence;
    private Action<WalkMotionCommand>? externalMotionSink;

    // Persistent per-movement histories (moved off of state so we don’t nuke them each tick)
    private RecyclableList<float> lastGoalDistances;
    private RecyclableList<Angle> lastFewAngles;
    private RecyclableList<RectF> lastFewRoundedBounds;


    public float CloseEnough { get; protected set; }

    public virtual RectF? GetPointOfInterest() => null;
    public virtual RectF? GetHazard() => null;
    public virtual bool FearCollision(GameCollider collider) => true;

    public static Walk Create(Vision vision, float baseSpeed, Action<WalkMotionCommand>? externalMotionSink = null)
    {
        var state = pool.Value.Rent();
        state.Construct(vision, baseSpeed, externalMotionSink);
        return state;
    }

    protected void Construct(Vision vision, float baseSpeed, Action<WalkMotionCommand>? externalMotionSink = null)
    {
        base.Construct(vision, baseSpeed);
        this.externalMotionSink = externalMotionSink;
        CloseEnough = 1;
        influence = MotionInfluence.Create("Wander Influence", true);
        if (this.externalMotionSink == null)
        {
            Velocity.AddInfluence(influence);
        }

        // Init persistent histories for this movement
        lastGoalDistances = RecyclableListPool<float>.Instance.Rent(20);
        lastFewAngles = RecyclableListPool<Angle>.Instance.Rent(20);
        lastFewRoundedBounds = RecyclableListPool<RectF>.Instance.Rent(20);
        Vision.VisibleObjectsChanged.Subscribe(this, static (me) => me.Tick(), this);
    }

    public void EvaluateNow() => Tick();

    private void Tick()
    {
        if (AreAllDependenciesValid == false)
        {
            TryDispose("Walk Tick");
            return;
        }
        FrameDebugger.RegisterTask(nameof(Walk));
        state.Hydrate(this);
        state.Sync(this);
        var commandedSpeed = influence.DeltaSpeed;
        var commandedAngle = influence.Angle;
        var outcome = WalkCalculation.AdjustSpeedAndVelocity(state, ref commandedSpeed, ref commandedAngle);
        if (externalMotionSink == null)
        {
            influence.DeltaSpeed = commandedSpeed;
            influence.Angle = commandedAngle;
        }
        else
        {
            externalMotionSink(new WalkMotionCommand(commandedAngle, commandedSpeed, BaseSpeed));
        }
        WalkInspectionHooks.Current?.Capture(this, state, outcome);
        state.Dehydrate();
    }

    public static readonly Event OnWanderInfluenceNotFoundErrorHandler = Event.Create();
    protected override void OnReturn()
    {
        if (Eye != null && Eye.Velocity != null && externalMotionSink == null && Eye.Velocity.ContainsInfluence(influence) == false)
        {
            if (OnWanderInfluenceNotFoundErrorHandler.HasSubscriptions)
            {
                OnWanderInfluenceNotFoundErrorHandler.Fire();
            }
            else
            {
                throw new InvalidOperationException($"Wander Influence not found in Eye.Velocity influences. This is a bug in the code, please report it.");
            }
        }

        if (Eye?.Velocity?.ContainsInfluence(influence) == true)
        {
            Eye.Velocity.RemoveInfluence(influence);
        }
        influence.Dispose("external/klooie/src/klooie/Gaming/Movement/Walk.cs:1");
        influence = null!;
        externalMotionSink = null;
        CloseEnough = 0;

        // Return persistent histories
        lastGoalDistances?.Dispose("external/klooie/src/klooie/Gaming/Movement/Walk.cs:1");
        lastGoalDistances = null!;
        lastFewAngles?.Dispose("external/klooie/src/klooie/Gaming/Movement/Walk.cs:1");
        lastFewAngles = null!;
        lastFewRoundedBounds?.Dispose("external/klooie/src/klooie/Gaming/Movement/Walk.cs:1");
        lastFewRoundedBounds = null!;

        base.OnReturn();
    }

    // Expose persistent histories to the state
    internal RecyclableList<float> Hist_LastGoalDistances => lastGoalDistances;
    internal RecyclableList<Angle> Hist_LastFewAngles => lastFewAngles;
    internal RecyclableList<RectF> Hist_LastFewRoundedBounds => lastFewRoundedBounds;
}

public class WalkCalculationState
{
    public RectF EyeBounds;
    public RectF? PointOfInterest;
    public RectF? Hazard;

    // NOTE: These now reference the Walk’s persistent lists; do NOT dispose in Dehydrate().
    public RecyclableList<float> LastGoalDistances;
    public RecyclableList<Angle> LastFewAngles;
    public RecyclableList<RectF> LastFewRoundedBounds;

    // Per-tick scratch
    public RecyclableList<AngleScore> AngleScores;
    public ObstacleBuffer Obstacles;

    // Weights are now provided by Walk and can be adapted; we will write back after tick.
    public WanderWeights Weights;

    public bool IsStuck;
    public float BaseSpeed;
    public Angle CurrentAngle;
    public float CloseEnough;
    public float Visibility;
    public bool HasLineOfSightToPointOfInterest;
    public bool IsCurrentlyCloseEnoughToPointOfInterest;

    public void Hydrate(Walk walk)
    {
        // Per-tick scratch allocations/rents only
        AngleScores = RecyclableListPool<AngleScore>.Instance.Rent(100);
        Obstacles = ObstacleBufferPool.Instance.Rent();

        // Per-tick reset
        IsStuck = false;
    }

    public void Sync(Walk walk)
    {
        // Persistent histories: borrow references from Walk (do not allocate here)
        LastFewAngles = walk.Hist_LastFewAngles;
        LastFewRoundedBounds = walk.Hist_LastFewRoundedBounds;
        LastGoalDistances = walk.Hist_LastGoalDistances;

        // Other live fields
        EyeBounds = walk.Eye.Bounds;
        CurrentAngle = walk.Velocity.Angle;
        Visibility = walk.Vision.Visibility;
        BaseSpeed = walk.BaseSpeed;
        PointOfInterest = walk.GetPointOfInterest();
        Hazard = walk.GetHazard();
        CloseEnough = walk.CloseEnough;
        HasLineOfSightToPointOfInterest = false;
        IsCurrentlyCloseEnoughToPointOfInterest = false;

        // Adaptive weights start from movement’s current weights
        Weights = WanderWeights.Default;

        Obstacles.WriteableBuffer.Clear();
        walk.Eye.GetObstacles(Obstacles);
        for (var i = Obstacles.WriteableBuffer.Count - 1; i >= 0; i--)
        {
            var c = Obstacles.WriteableBuffer[i];
            if (walk.FearCollision(c) == false)
            {
                Obstacles.WriteableBuffer.RemoveAt(i);
            }
        }

        if (PointOfInterest.HasValue && EyeBounds.CalculateNormalizedDistanceTo(PointOfInterest.Value) <= CloseEnough)
        {
            HasLineOfSightToPointOfInterest = WalkCalculation.HasLineOfSightTo(this, PointOfInterest.Value);
            IsCurrentlyCloseEnoughToPointOfInterest = HasLineOfSightToPointOfInterest;
        }
    }

    public void Dehydrate()
    {
        // Only dispose per-tick scratch
        AngleScores?.Dispose("external/klooie/src/klooie/Gaming/Movement/Walk.cs:1");
        AngleScores = null!;
        Obstacles?.Dispose("external/klooie/src/klooie/Gaming/Movement/Walk.cs:1");
        Obstacles = null!;

        // Do NOT dispose persistent histories (owned by Walk)
        LastFewAngles = null!;
        LastFewRoundedBounds = null!;
        LastGoalDistances = null!;
        HasLineOfSightToPointOfInterest = false;
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

public static class WalkCalculation
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

    private static HashSet<RectF> sharedUniqueModeHashSet = new HashSet<RectF>();
    private static ColliderBox sharedColliderBox;

    public static WalkDecisionOutcome AdjustSpeedAndVelocity(WalkCalculationState input, ref float newSpeed, ref Angle newAngle)
    {
        UpdateGoalDistanceHistory(input);
        ComputeScores(input);
        ConsiderEmergencyMode(input);
        if (TryWalkDirectlyTowardsPointOfInterest(input, ref newSpeed, ref newAngle))
        {
            return new WalkDecisionOutcome(true, newAngle, newSpeed, 1f, -1);
        }
        OptimizeWeights(input);
        var winner = SelectBestAngleScore(input, out float bestScore, out int bestScoreIndex);
        newAngle = winner.Angle;
        newSpeed = EvaluateSpeed(input, winner.Angle, bestScore);
        UpdateInertiaAndPositionHistory(input, winner);
        return new WalkDecisionOutcome(false, newAngle, newSpeed, bestScore, bestScoreIndex);
    }


    private static void UpdateInertiaAndPositionHistory(WalkCalculationState input, AngleScore best)
    {
        input.LastFewAngles.Items.Add(best.Angle);
        if (input.LastFewAngles.Count > MaxHistory) input.LastFewAngles.Items.RemoveAt(0);
        var rounded = input.EyeBounds.Round();
        input.LastFewRoundedBounds.Items.Add(rounded);
        if (input.LastFewRoundedBounds.Count > MaxHistory) input.LastFewRoundedBounds.Items.RemoveAt(0);
    }

    private static AngleScore SelectBestAngleScore(WalkCalculationState input, out float bestTotal, out int bestIndex)
    {
        AngleScore best = default;
        bestTotal = float.MinValue;
        bestIndex = -1;
        for (int i = 0; i < input.AngleScores.Count; i++)
        {
            float t = input.AngleScores[i].GetTotal(input.Weights);
            if (t > bestTotal)
            {
                best = input.AngleScores[i];
                bestTotal = t;
                bestIndex = i;
            }
        }
        return best;
    }

    private static void UpdateGoalDistanceHistory(WalkCalculationState input)
    {
        if (input.PointOfInterest.HasValue)
        {
            float d = input.EyeBounds.Center.CalculateDistanceTo(input.PointOfInterest.Value.Center);
            input.LastGoalDistances.Items.Add(d);
            if (input.LastGoalDistances.Count > MaxHistory) input.LastGoalDistances.Items.RemoveAt(0);
        }
        else
        {
            input.LastGoalDistances.Items.Clear();
        }
    }

    private static bool TryWalkDirectlyTowardsPointOfInterest(WalkCalculationState input, ref float newSpeed, ref Angle newAngle)
    {
        if (input.PointOfInterest.HasValue == false || input.HasLineOfSightToPointOfInterest == false) return false;

        var direct = input.EyeBounds.CalculateAngleTo(input.PointOfInterest.Value);
        var s = EvaluateSpeed(input, direct, 1f);
        newAngle = direct;
        newSpeed = s;
        input.LastFewAngles.Items.Add(direct);
        if (input.LastFewAngles.Count > MaxHistory) input.LastFewAngles.Items.RemoveAt(0);
        var r = input.EyeBounds.Round();
        input.LastFewRoundedBounds.Items.Add(r);
        if (input.LastFewRoundedBounds.Count > MaxHistory) input.LastFewRoundedBounds.Items.RemoveAt(0);
        return true;
    }

    private static void OptimizeWeights(WalkCalculationState input)
    {
        float minC = float.MaxValue, maxC = float.MinValue, sumC = 0f;
        for (int i = 0, n = input.AngleScores.Count; i < n; i++)
        {
            float c = input.AngleScores[i].Collision; // normalized [0..1]
            if (c < minC) minC = c;
            if (c > maxC) maxC = c;
            sumC += c;
        }
        float avgC = sumC / input.AngleScores.Count;
        float spread = maxC - minC;

        if (!input.PointOfInterest.HasValue) input.Weights.PointOfInterestWeight = 0f;
        if (!input.Hazard.HasValue) input.Weights.HazardWeight = 0f;

        if (minC > 0.8f && spread < 0.1f)
        {
            // Low risk everywhere -> collision weight can relax a lot
            input.Weights.CollisionWeight = 0f;
            input.Weights.PointOfInterestWeight *= 2;
            // Keep hazard weight as-is if present; still want to avoid it.
        }

        if (input.IsStuck)
        {
            input.Weights.InertiaWeight = 0f;
            input.Weights.HazardWeight *= 0.3f;  // don’t let “fear” block the only exit
            input.Weights.CollisionWeight *= 0.6f; // still avoid, just less timid
            input.Weights.ForwardWeight *= 1.5f;  // prefer continuing motion
        }

        // Scale inertia by how much history we have
        input.Weights.InertiaWeight = input.Weights.InertiaWeight * input.LastFewAngles.Count / (float)MaxHistory;
    }
    private static readonly Dictionary<float, float> collisionCache = new Dictionary<float, float>(32);
    private static void ComputeScores(WalkCalculationState input)
    {
        input.AngleScores.Items.Clear();
        collisionCache.Clear();

        // Precompute inertial, POI, and hazard angles as degrees to avoid Angle ops in inner loops
        float inertiaDeg = AverageAngle(input.LastFewAngles.Items, input.CurrentAngle).Value;
        float? poiDeg = input.PointOfInterest.HasValue ? input.EyeBounds.CalculateAngleTo(input.PointOfInterest.Value).Value : (float?)null;
        Angle? hazardDeg = input.Hazard.HasValue ? input.EyeBounds.CalculateAngleTo(input.Hazard.Value) : (float?)null;


        // Base angle: toward POI unless we’re already close; otherwise away from hazard if present; else keep velocity
        var baseDeg = input.CurrentAngle;

        bool closeToPoi = input.IsCurrentlyCloseEnoughToPointOfInterest;

        if (poiDeg.HasValue && !closeToPoi) baseDeg = poiDeg.Value;
        else if (hazardDeg.HasValue) baseDeg = hazardDeg.Value.Add(180);

        // Reuse one CollisionPrediction object across all candidates (overwrite each time)
        var prediction = CollisionPredictionPool.Instance.Rent();

        try
        {
            // 1) Score the base angle first (coarse, no repulsion)
            ScoreAngle_NoRepulsion(input, inertiaDeg, poiDeg, hazardDeg, baseDeg, prediction);

            float sweepTotal = input.IsStuck ? 360f : TotalAngularTravelDeg;
            float coarseStep = input.IsStuck ? 15f : CoarseStepDeg;
            float fineSpan = input.IsStuck ? 36f : FineSpanDeg;
            float fineStep = input.IsStuck ? 1f : FineStepDeg;
            int refineTopK = input.IsStuck ? Math.Min(4, CoarseRefineTopK + 2) : CoarseRefineTopK;

            // 2) Coarse sweep around base; keep top-K coarse picks
            Span<CoarseTop> topAngles = stackalloc CoarseTop[GetTopSize(CoarseRefineTopK)];
            float travelCompleted = 0f;

            while (travelCompleted < sweepTotal)
            {
                float delta = travelCompleted + coarseStep;
                travelCompleted += coarseStep;

                var leftDeg = baseDeg.Add(-delta);
                var rightDeg = baseDeg.Add(delta);

                float leftScore = ScoreAngle_NoRepulsion(input, inertiaDeg, poiDeg, hazardDeg, leftDeg, prediction);
                float rightScore = ScoreAngle_NoRepulsion(input, inertiaDeg, poiDeg, hazardDeg, rightDeg, prediction);

                TryPushTop(topAngles, leftDeg, leftScore);
                TryPushTop(topAngles, rightDeg, rightScore);
            }

            // 3) Refine around the best coarse angles (including base)
            TryPushTop(topAngles, baseDeg, 0); // ensure base gets refined too

            // Refine window half-span
            var halfSpan = fineSpan * 0.5f;

            // Avoid refining near-duplicate centers
            Span<Angle> refinedCenters = stackalloc Angle[CoarseRefineTopK + 1];
            int refinedCount = 0;

            for (int i = 0; i < topAngles.Length; i++)
            {
                if (!topAngles[i].Valid) continue;
                var center = topAngles[i].AngleDeg;

                bool dup = false;
                for (int j = 0; j < refinedCount; j++)
                {
                    if (center.DiffShortest(refinedCenters[j]) < fineStep) { dup = true; break; }
                }
                if (dup) continue;

                refinedCenters[refinedCount++] = center;

                // Fine sweep with repulsion check ON
                for (float d = -halfSpan; d <= halfSpan; d += fineStep)
                {
                    var cand = center.Add(d);
                    ScoreAngle_WithRepulsion(input, inertiaDeg, poiDeg, hazardDeg, cand, prediction);
                }
            }
        }
        finally
        {
            prediction.Dispose("external/klooie/src/klooie/Gaming/Movement/Walk.cs:1");
        }
    }

    // --- Two-stage scoring helpers ----------------------------------------------------------

    // Coarse: skip repulsion penalty (no RadialOffset call).
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ScoreAngle_NoRepulsion(WalkCalculationState input, float inertiaDeg, Angle? poiDeg, Angle? hazardDeg, Angle candidateDeg, CollisionPrediction prediction)
    {
        float normCollision = DangerClamp(PredictCollision(input, candidateDeg, prediction));
        float normInertia = 1f - Clamp01(candidateDeg.DiffShortest(inertiaDeg) * Inv180);
        float normForward = 1f - Clamp01(candidateDeg.DiffShortest(input.CurrentAngle.Value) * Inv090);
        float normPoi = poiDeg.HasValue
            ? 1f - Clamp01(candidateDeg.DiffShortest(poiDeg.Value) * Inv180)
            : 1f;
        float normHazard = hazardDeg.HasValue
            ? Clamp01(candidateDeg.DiffShortest(hazardDeg.Value) * Inv180)
            : 1f;

        var score = new AngleScore(candidateDeg, normCollision, normInertia, normForward, normPoi, normHazard);
        input.AngleScores.Items.Add(score);

        return CoarseRank(normCollision, normInertia, normForward, normPoi, normHazard);
    }

    // Fine: include repulsion (predict future position, rounded), only for shortlisted angles.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ScoreAngle_WithRepulsion(WalkCalculationState input, Angle inertiaDeg, Angle? poiDeg, Angle? hazardDeg, Angle candidateDeg, CollisionPrediction prediction)
    {
        float normCollision = DangerClamp(PredictCollision(input, candidateDeg, prediction));

        float repulsionPenalty = 1f;
        if (!input.IsStuck)
        {
            var predictedPos = PredictRoundedPosition(input, candidateDeg);
            for (int i = 0, n = input.LastFewRoundedBounds.Count; i < n; i++)
            {
                if (input.LastFewRoundedBounds[i].Equals(predictedPos)) { repulsionPenalty = 0.75f; break; }
            }
        }
        normCollision *= repulsionPenalty;

        float normInertia = 1f - Clamp01(candidateDeg.DiffShortest(inertiaDeg) * Inv180);
        float normForward = 1f - Clamp01(candidateDeg.DiffShortest(input.CurrentAngle.Value) * Inv090);
        float normPoi = poiDeg.HasValue
            ? 1f - Clamp01(candidateDeg.DiffShortest(poiDeg.Value) * Inv180)
            : 1f;
        float normHazard = hazardDeg.HasValue
            ? Clamp01(candidateDeg.DiffShortest(hazardDeg.Value) * Inv180)
            : 1f;

        var score = new AngleScore(candidateDeg, normCollision, normInertia, normForward, normPoi, normHazard);
        input.AngleScores.Items.Add(score);

        return CoarseRank(normCollision, normInertia, normForward, normPoi, normHazard);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float CoarseRank(float normCollision, float normInertia, float normForward, float normPoi, float normHazard)
    {
        return normCollision * 3f
             + (normHazard + normPoi) * 0.75f
             + (normForward + normInertia) * 0.25f;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float PredictCollision(WalkCalculationState input, Angle angleDeg, CollisionPrediction prediction)
    {
        if (collisionCache.TryGetValue(angleDeg.Value, out var cachedTimeToCollision))
        {
            return cachedTimeToCollision;
        }

        if (sharedColliderBox == null) sharedColliderBox = new ColliderBox(input.EyeBounds);
        else sharedColliderBox.Bounds = input.EyeBounds;
        CollisionDetector.Predict(sharedColliderBox, angleDeg, input.Obstacles.WriteableBuffer, input.Visibility, CastingMode.Precise, input.Obstacles.WriteableBuffer.Count, prediction);
        if (!prediction.CollisionPredicted)
        {
            collisionCache.Add(angleDeg.Value, MaxCollisionHorizon);
            return MaxCollisionHorizon;
        }
        if (input.BaseSpeed <= 0f) throw new Exception("Base speed can't be 0");
        var timeToCollision = prediction.LKGD / input.BaseSpeed;
        collisionCache.Add(angleDeg.Value, timeToCollision);
        return timeToCollision;
    }

    private static RectF PredictRoundedPosition(WalkCalculationState input, Angle angle) => input.EyeBounds.RadialOffset(angle, input.EyeBounds.Hypotenous).Round();

    private static void ConsiderEmergencyMode(WalkCalculationState input)
    {
        if (CalculateIsStuck(input))
        {
            input.IsStuck = true;
            input.LastFewAngles.Items.Clear();
            input.LastFewRoundedBounds.Items.Clear();
        }
        else
        {
            input.IsStuck = false;
        }
    }

    private static bool CalculateIsStuck(WalkCalculationState input, int window = 5, int maxUnique = 3)
    {
        if (input.IsCurrentlyCloseEnoughToPointOfInterest) return false;
        if (input.LastFewRoundedBounds.Count < window) return false;
        sharedUniqueModeHashSet.Clear();
        for (int i = input.LastFewRoundedBounds.Count - window; i < input.LastFewRoundedBounds.Count; i++)
        {
            sharedUniqueModeHashSet.Add(input.LastFewRoundedBounds[i]);
        }
        bool ret = sharedUniqueModeHashSet.Count <= maxUnique;
        sharedUniqueModeHashSet.Clear();
        return ret;
    }

    private static float EvaluateSpeed(WalkCalculationState input, Angle chosenAngle, float confidence)
    {
        if (input.BaseSpeed == 0) throw new Exception("Zero Speed Yo");
        if (input.IsCurrentlyCloseEnoughToPointOfInterest)
        {
            return 0;
        }

        const float minFactor = 0.2f;
        float factor = Smoothstep01(confidence);
        if (input.IsStuck) factor = MathF.Max(factor, 0.6f);
        return input.BaseSpeed * (minFactor + (1f - minFactor) * factor);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool HasLineOfSightTo(WalkCalculationState input, in RectF target)
    {
        var prediction = CollisionPredictionPool.Instance.Rent();
        try
        {
            var toTarget = input.EyeBounds.CalculateAngleTo(target);
            if (sharedColliderBox == null) sharedColliderBox = new ColliderBox(input.EyeBounds);
            else sharedColliderBox.Bounds = input.EyeBounds;
            CollisionDetector.Predict(sharedColliderBox, toTarget, input.Obstacles.WriteableBuffer, input.Visibility, CastingMode.Precise, input.Obstacles.WriteableBuffer.Count, prediction);

            if (!prediction.CollisionPredicted) return true;

            float distToTarget = input.EyeBounds.Center.CalculateDistanceTo(target.Center);
            // If the first hit is beyond the target, treat as clear
            return prediction.LKGD >= distToTarget - input.EyeBounds.Hypotenous * 0.5f;
        }
        finally
        {
            prediction.Dispose("external/klooie/src/klooie/Gaming/Movement/Walk.cs:1");
        }
    }

    private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Smoothstep01(float t) { t = Clamp01(t); return t * t * (3f - 2f * t); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DangerClamp(float timeToCollision)
    {
        if (timeToCollision >= MaxCollisionHorizon) return 1f;
        if (timeToCollision < 0) return 0f;
        return timeToCollision / MaxCollisionHorizon;
    }

    private struct CoarseTop
    {
        public Angle AngleDeg;
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
    private static void TryPushTop(Span<CoarseTop> top, Angle angleDeg, float score)
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

public readonly struct WalkDecisionOutcome
{
    public bool UsedDirectWalk { get; }
    public Angle ChosenAngle { get; }
    public float ChosenSpeed { get; }
    public float BestScore { get; }
    public int BestScoreIndex { get; }

    public WalkDecisionOutcome(bool usedDirectWalk, Angle chosenAngle, float chosenSpeed, float bestScore, int bestScoreIndex)
    {
        UsedDirectWalk = usedDirectWalk;
        ChosenAngle = chosenAngle;
        ChosenSpeed = chosenSpeed;
        BestScore = bestScore;
        BestScoreIndex = bestScoreIndex;
    }
}

public readonly struct WalkMotionCommand
{
    public Angle Angle { get; }
    public float Speed { get; }
    public float BaseSpeed { get; }

    public WalkMotionCommand(Angle angle, float speed, float baseSpeed)
    {
        Angle = angle;
        Speed = speed;
        BaseSpeed = baseSpeed;
    }

    public float NormalizedSpeed => BaseSpeed <= 0 ? 0 : Math.Clamp(Speed / BaseSpeed, 0f, 1f);
}

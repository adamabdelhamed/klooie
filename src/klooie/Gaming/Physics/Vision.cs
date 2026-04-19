using System.Runtime.CompilerServices;

using System.Collections.Generic;

namespace klooie.Gaming;
public class Vision : Recyclable, IFrameTask
{
    public const float DefaultVisibility = 20;
    public const float DefaultAngularVisibility = 60;
    private const int RetainedSharedRayCandidateCapacity = 256;
    private static Event<Vision>? _visionInitiated;
    private static readonly List<RayCandidate> sharedRayCandidates = new List<RayCandidate>(32);
    public static Event<Vision> VisionInitiated => _visionInitiated ??= Event<Vision>.Create();

    private Event? _visibleObjectsChanged;
    public Event VisibleObjectsChanged => _visibleObjectsChanged ??= Event.Create();


    private VisionFilterContext targetFilterContext = new VisionFilterContext();
    private readonly Dictionary<GameCollider, VisuallyTrackedObject> trackedObjectsMap = new Dictionary<GameCollider, VisuallyTrackedObject>(10);
    private Event<VisionFilterContext>? _targetBeingEvaluated;
    public List<VisuallyTrackedObject> TrackedObjectsList { get; private set; } = new List<VisuallyTrackedObject>(10);
    public Event<VisionFilterContext> TargetBeingEvaluated => _targetBeingEvaluated ?? (_targetBeingEvaluated = Event<VisionFilterContext>.Create());
    public GameCollider Eye { get; private set; } = null!;
    public float Visibility { get; set; } 
    public float AngularVisibility { get; set; } 
    public CastingMode CastingMode { get; set; }
    public float AngleStep {get;set;}
    public int AngleFuzz { get; set; }
    public  TimeSpan MaxMemoryTime { get; set; }
    public Vision() { }
    public static Vision Create(FrameTaskScheduler scheduler, GameCollider eye, bool autoScan = true)
    {
        var vision = VisionPool.Instance.Rent();
        vision.Eye = eye;
        vision.AngleStep = 5;
        vision.AngleFuzz = 2;
        vision.CastingMode = CastingMode.SingleRay;
        vision.MaxMemoryTime = TimeSpan.FromSeconds(2);
        _visionInitiated?.Fire(vision);
        scheduler.Enqueue(vision);
        return vision;
    }

    protected override void OnInit()
    {
        base.OnInit();
        Visibility = DefaultVisibility;
        AngularVisibility = DefaultAngularVisibility;
    }

    public Angle FieldOfViewStart => Eye.Velocity.Angle.Add(-AngularVisibility / 2f);
    public Angle FieldOfViewEnd => Eye.Velocity.Angle.Add(AngularVisibility / 2f);

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    public void Scan()
    {
        RemoveStaleTrackedObjects();
        if (Eye.IsVisible == false)
        {
            ReleaseScanScratch();
            return;
        }
        var buffer = ObstacleBufferPool.Instance.Rent();
        try
        {
            Eye.ColliderGroup.SpacialIndex.Query(ResolveQueryBounds(), buffer);
            FilterObstacles(buffer);
            if (TryPerfestScan(buffer) == false)
            {
                ApproximateScan(buffer);
            }
        }
        finally
        {
            ReleaseScanScratch();
            buffer.Dispose("external/klooie/src/klooie/Gaming/Physics/Vision.cs:1");
        }
        _visibleObjectsChanged?.Fire();
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private RectF ResolveQueryBounds()
    {
        if (AngularVisibility >= 180)
        {
            var queryDiameter = MathF.Max(Visibility * 2.4f, Eye.Bounds.Hypotenous);
            return Eye.Bounds.Grow(queryDiameter, queryDiameter);
        }

        return Eye.Bounds.SweptAABB(Eye.Bounds.Grow(.5f).RadialOffset(Eye.Velocity.Angle, Visibility * 1.2f));
    }

    private bool TryPerfestScan(ObstacleBuffer buffer)
    {
        if (AngleStep > 1) return false;
        if(MaxMemoryTime > TimeSpan.Zero) throw new InvalidOperationException($"When {nameof(AngleStep)} is <= 1 then MaxMemoryTime must be <= TimeSpan.Zero.");
        if(TrackedObjectsList.Count > 0) throw new InvalidOperationException($"When {nameof(AngleStep)} is <= 1 then TrackedObjectsList must be empty."); 

        PopulateRayCandidates(buffer);
        var lineOfSightBuffer = ObstacleBufferPool.Instance.Rent();
        try
        {
            for (var i = 0; i < buffer.WriteableBuffer.Count; i++)
            {
                var candidate = buffer.WriteableBuffer[i];
                if (candidate == Eye) continue; 
                var distance = Eye.CalculateNormalizedDistanceTo(candidate);
                if (distance > Visibility) continue;
                var angle = Eye.CalculateAngleTo(candidate);

                if (distance > 2f)
                {
                    var angleDiff = Eye.Velocity.Angle.DiffShortest(angle);
                    if (angleDiff > AngularVisibility / 2f) continue;
                }

                PopulatePerfectScanObstacles(angle, distance, candidate, lineOfSightBuffer);
                var prediction = CollisionPredictionPool.Instance.Rent();
                var obstruction = CollisionDetector.GetLineOfSightObstruction(Eye, candidate, lineOfSightBuffer.WriteableBuffer, CastingMode.SingleRay, prediction) as GameCollider;
                VisuallyTrackedObject? target = null;
                if (obstruction != null || TryIgnorePotentialTargetIgnorable(candidate, out target))
                {
                    if (target != null) throw new InvalidOperationException($"Target was present in visible objects, but should have been marked as stale.");
                    prediction.TryDispose("external/klooie/src/klooie/Gaming/Physics/Vision.cs:121");
                    continue;
                }

                AddTrackedObject(VisuallyTrackedObject.Create(candidate, prediction, prediction.LKGD, angle));
            }
        }
        finally
        {
            lineOfSightBuffer.Dispose("external/klooie/src/klooie/Gaming/Physics/Vision.cs:1");
        }
        return true;
    }

    private void ApproximateScan(ObstacleBuffer buffer)
    {
        PopulateRayCandidates(buffer);
        var castBuffer = ObstacleBufferPool.Instance.Rent();
        try
        {
            var directlyAheadResult = Cast(Eye.Velocity.Angle, castBuffer);
            if (directlyAheadResult != null)
            {
                AddTrackedObject(directlyAheadResult);
            }

            var currentAngle = FieldOfViewStart;
            var totalTravel = 0f;

            while (totalTravel <= AngularVisibility)
            {
                var castAngle = currentAngle.Add(AngleFuzz == 0 ? 0 : Random.Shared.Next(-AngleFuzz, AngleFuzz));
                if (castAngle.DiffShortest(Eye.Velocity.Angle) > 0.001f)
                {
                    var visibleObject = Cast(castAngle, castBuffer);
                    if (visibleObject != null)
                    {
                        AddTrackedObject(visibleObject);
                    }
                }

                totalTravel += AngleStep;
                currentAngle = currentAngle.Add(AngleStep);
            }
        }
        finally
        {
            castBuffer.Dispose("external/klooie/src/klooie/Gaming/Physics/Vision.cs:1");
        }
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private void RemoveStaleTrackedObjects()
    {
        if(MaxMemoryTime <= TimeSpan.Zero)
        {
            UntrackAll();
            return;
        }

        for (var i = TrackedObjectsList.Count - 1; i >= 0; i--)
        {
            var trackedObject = TrackedObjectsList[i];
            if (trackedObject.IsTargetStillValid == false)
            {
                UnTrackAtIndex(i);
                continue;
            }

            if (trackedObject.TimeSinceLastSeen > MaxMemoryTime)
            {
                UnTrackAtIndex(i);
                continue;
            }

            if(trackedObject.Target.CalculateNormalizedDistanceTo(Eye) > Visibility)
            {
                UnTrackAtIndex(i);
                continue;
            }
        }
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private void UnTrackAtIndex(int index)
    {
        var trackedObject = TrackedObjectsList[index];
        trackedObjectsMap.Remove(trackedObject.Target);
        TrackedObjectsList.RemoveAt(index);
        trackedObject.TryDispose("external/klooie/src/klooie/Gaming/Physics/Vision.cs:210");
    }

    private void UntrackAll()
    {
        for(var i = 0; i < TrackedObjectsList.Count; i++)
        {
            TrackedObjectsList [i].TryDispose("external/klooie/src/klooie/Gaming/Physics/Vision.cs:217");
        }
        TrackedObjectsList.Clear();
        trackedObjectsMap.Clear();
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private VisuallyTrackedObject? Cast(Angle angle, ObstacleBuffer castBuffer)
    {
        PopulateCastObstacles(angle, castBuffer);
        var singleRay = CollisionPredictionPool.Instance.Rent();
        CollisionDetector.Predict(Eye, angle, castBuffer.WriteableBuffer, Visibility, CastingMode, castBuffer.WriteableBuffer.Count, singleRay);
        var potentialTarget = singleRay.ColliderHit as GameCollider;

        if (TryIgnorePotentialTargetIgnorable(potentialTarget, out var target))
        {
            if (target != null)
            {
                target.LastSeenTime = Game.Current.MainColliderGroup.ScaledNow;
                target.Distance = Eye.CalculateNormalizedDistanceTo(potentialTarget);
            }
            singleRay.TryDispose("external/klooie/src/klooie/Gaming/Physics/Vision.cs:238");
            return null;
        }

        return VisuallyTrackedObject.Create(potentialTarget,singleRay,singleRay.LKGD,angle);
    }

    private bool TryIgnorePotentialTargetIgnorable(GameCollider? potentialTarget, out VisuallyTrackedObject existing)
    {
        VisuallyTrackedObject existingTarget = null;
        var alreadyTracked = potentialTarget != null && trackedObjectsMap.TryGetValue(potentialTarget, out existingTarget);
        var shouldBeIgnored = alreadyTracked || potentialTarget == null || potentialTarget?.Velocity == null || potentialTarget.IsVisible == false;
        existing = existingTarget;
        return shouldBeIgnored;
    }

    public bool TryGetValue(GameCollider key, out VisuallyTrackedObject ret)
        => trackedObjectsMap.TryGetValue(key, out ret);

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    public static LocF ClosestPointOnRect(RectF rect, LocF point)
    {
        // Clamp the point to the rectangle's bounds
        float x = Math.Clamp(point.Left, rect.Left, rect.Right);
        float y = Math.Clamp(point.Top, rect.Top, rect.Bottom);
        return new LocF(x, y);
    }


    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private void FilterObstacles(ObstacleBuffer buffer)
    {
        var writeIndex = 0;
        for (var i = 0; i < buffer.WriteableBuffer.Count; i++)
        {
            var obstacle = buffer.WriteableBuffer[i];
            if (IsIgnoredByFilter(obstacle) == false)
            {
                buffer.WriteableBuffer[writeIndex++] = obstacle;
            }
        }

        if (writeIndex < buffer.WriteableBuffer.Count)
        {
            buffer.WriteableBuffer.RemoveRange(writeIndex, buffer.WriteableBuffer.Count - writeIndex);
        }
    }

    string IFrameTask.Name => nameof(Vision);
    void IFrameTask.Execute() => Scan();

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private bool IsIgnoredByFilter(GameCollider potentialTarget)
    {
        if(potentialTarget == Eye)return true;
        if(Eye.CanCollideWith(potentialTarget) == false || potentialTarget.CanCollideWith(Eye) == false)return true;
        targetFilterContext.Reset(potentialTarget);
        _targetBeingEvaluated?.Fire(targetFilterContext);
        return targetFilterContext.Ignored;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReleaseScanScratch()
    {
        sharedRayCandidates.Clear();
        if (sharedRayCandidates.Capacity > RetainedSharedRayCandidateCapacity)
        {
            sharedRayCandidates.Capacity = RetainedSharedRayCandidateCapacity;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AddTrackedObject(VisuallyTrackedObject trackedObject)
    {
        TrackedObjectsList.Add(trackedObject);
        trackedObjectsMap[trackedObject.Target] = trackedObject;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopulateCastObstacles(Angle angle, ObstacleBuffer castBuffer)
    {
        castBuffer.WriteableBuffer.Clear();
        for (var i = 0; i < sharedRayCandidates.Count; i++)
        {
            var candidate = sharedRayCandidates[i];
            if (DiffShortestDegrees(angle.Value, candidate.CenterAngle) > candidate.AngularReach) continue;
            castBuffer.WriteableBuffer.Add(candidate.Collider);
        }
    }

    private void PopulateRayCandidates(ObstacleBuffer buffer)
    {
        sharedRayCandidates.Clear();
        var eyeBounds = Eye.Bounds;
        var eyeRadius = eyeBounds.Hypotenous * 0.5f;
        const float angularPadding = 1.5f;
        const float minDistance = .001f;
        const float radToDeg = 57.29578f;

        for (var i = 0; i < buffer.WriteableBuffer.Count; i++)
        {
            var obstacle = buffer.WriteableBuffer[i];
            var obstacleBounds = obstacle.Bounds;
            var centerAngle = eyeBounds.CalculateAngleTo(obstacleBounds).Value;
            var centerDistance = eyeBounds.CalculateDistanceTo(obstacleBounds);
            var obstacleRadius = obstacleBounds.Hypotenous * 0.5f;
            var sweepRadius = eyeRadius + obstacleRadius + .5f;
            var angularReach = centerDistance <= minDistance
                ? 180f
                : MathF.Min(180f, MathF.Atan2(sweepRadius, centerDistance) * radToDeg + angularPadding);
            var minPossibleDistance = MathF.Max(0, centerDistance - sweepRadius);
            sharedRayCandidates.Add(new RayCandidate(obstacle, centerAngle, angularReach, minPossibleDistance));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PopulatePerfectScanObstacles(Angle angle, float targetDistance, GameCollider target, ObstacleBuffer castBuffer)
    {
        castBuffer.WriteableBuffer.Clear();
        for (var i = 0; i < sharedRayCandidates.Count; i++)
        {
            var candidate = sharedRayCandidates[i];
            if (candidate.Collider == target) continue;
            if (candidate.MinDistance > targetDistance) continue;
            if (DiffShortestDegrees(angle.Value, candidate.CenterAngle) > candidate.AngularReach) continue;
            castBuffer.WriteableBuffer.Add(candidate.Collider);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float DiffShortestDegrees(float a, float b)
    {
        var c = MathF.Abs(a - b);
        return c <= 180f ? c : 360f - c;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Eye = null!;
        Visibility = DefaultVisibility;
        AngularVisibility = DefaultAngularVisibility;
        _targetBeingEvaluated?.TryDispose("external/klooie/src/klooie/Gaming/Physics/Vision.cs:383");
        _targetBeingEvaluated = null;

        for (var i = TrackedObjectsList.Count - 1; i >= 0; i--)
        {
            UnTrackAtIndex(i);
        }

        TrackedObjectsList.Clear();
        trackedObjectsMap.Clear();
        ReleaseScanScratch();
        _visibleObjectsChanged?.TryDispose("external/klooie/src/klooie/Gaming/Physics/Vision.cs:395");
        _visibleObjectsChanged = null;
    }
}

internal readonly struct RayCandidate
{
    public readonly GameCollider Collider;
    public readonly float CenterAngle;
    public readonly float AngularReach;
    public readonly float MinDistance;

    public RayCandidate(GameCollider collider, float centerAngle, float angularReach, float minDistance)
    {
        Collider = collider;
        CenterAngle = centerAngle;
        AngularReach = angularReach;
        MinDistance = minDistance;
    }
}

public class VisionFilterContext
{
    internal bool Ignored { get; private set; }
    public GameCollider PotentialTarget { get; internal set; }
    public void IgnoreTargeting() => Ignored = true;

    internal void Reset(GameCollider toBind)
    {
        Ignored = false;
        PotentialTarget = toBind;
    }
}

public class VisionDependencyState : DelayState
{
    public Vision Vision { get; private set; }

    public static VisionDependencyState Create(Vision v)
    {
        var state = VisionDependencyStatePool.Instance.Rent();
        state.Vision = v;
        state.AddDependency(v);
        state.AddDependency(v.Eye);
        state.AddDependency(v.Eye.Velocity);
        return state;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Vision = null!;
    }
}

public class VisuallyTrackedObject : Recyclable
{
    private int targetLease;
    public GameCollider Target { get; private set; }
    public TimeSpan LastSeenTime { get; set; }
    public CollisionPrediction RayCastResult { get; set; }
    public float Distance { get; set; }
    public Angle Angle { get; set; }

    public TimeSpan TimeSinceLastSeen => Game.Current.MainColliderGroup.ScaledNow - LastSeenTime;

    public bool IsTargetStillValid => Target != null && Target.IsStillValid(targetLease);

    public VisuallyTrackedObject() { }

    public static VisuallyTrackedObject Create(GameCollider target, CollisionPrediction rayCastResult, float distance, Angle angle)
    {
        var trackedObject = VisuallyTrackedObjectPool.Instance.Rent();
        trackedObject.targetLease = target.Lease;
        trackedObject.Target = target;
        trackedObject.LastSeenTime = Game.Current.MainColliderGroup.ScaledNow;
        trackedObject.RayCastResult = rayCastResult;
        trackedObject.Distance = distance;
        trackedObject.Angle = angle;
        return trackedObject;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Target = null;
        LastSeenTime = TimeSpan.Zero;
        RayCastResult?.TryDispose("external/klooie/src/klooie/Gaming/Physics/Vision.cs:482");
        RayCastResult = null!;
        Distance = default;
        Angle = default;
        targetLease = default;
    }
}

public class VisionFilter : IConsoleControlFilter
{
    private static readonly RGB NotSeen = new RGB(30, 30, 30);
    private static readonly RGB SeenNow = RGB.Green;
    public ConsoleControl Control { get; set; }
    public ConsoleBitmap ParentBitmap { get; set; }

    public Vision Vision { get; set; } 
    public VisionFilter(Vision vision)
    {
        Vision = vision;
    }

    public void Filter(ConsoleBitmap bitmap)
    {
        if(Control is GameCollider collider == false || Vision.TryGetValue(collider, out VisuallyTrackedObject vto) == false)
        {
            bitmap.Fill(NotSeen);
            return;
        }

        var ageSeconds = (float)vto.TimeSinceLastSeen.TotalSeconds;
        var staleness = Vision.MaxMemoryTime == TimeSpan.Zero ? 0 :  ageSeconds / (float)Vision.MaxMemoryTime.TotalSeconds;

        bitmap.Fill(SeenNow.ToOther(NotSeen, staleness)); 
    }
}

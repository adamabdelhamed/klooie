using System.Runtime.CompilerServices;

namespace klooie.Gaming;
public class Vision : Recyclable
{
    private static Event<Vision>? _visionInitiated;
    public static Event<Vision> VisionInitiated => _visionInitiated ??= EventPool<Vision>.Instance.Rent();


    private Event? _visibleObjectsChanged;
    public Event VisibleObjectsChanged => _visibleObjectsChanged ??= EventPool.Instance.Rent();

    private const float DelayMs = 667;
    private int eyeLease = 0;
    private VisionFilterContext targetFilterContext = new VisionFilterContext();
    private Event<VisionFilterContext>? _targetBeingEvaluated;
    public List<VisuallyTrackedObject> TrackedObjectsList { get; private set; } = new List<VisuallyTrackedObject>();
    public Dictionary<GameCollider, VisuallyTrackedObject> TrackedObjectsDictionary { get; private set; } = new Dictionary<GameCollider, VisuallyTrackedObject>();
    public Event<VisionFilterContext> TargetBeingEvaluated => _targetBeingEvaluated ?? (_targetBeingEvaluated = EventPool<VisionFilterContext>.Instance.Rent());
    public GameCollider Eye { get; private set; } = null!;
    public float Range { get; set; } = 100;
    public float AngularVisibility { get; set; } = 60;
    public float ScanOffset { get; private set; }
    public Vision() { }

    private static Random random = new Random();
    public static Vision Create(GameCollider eye)
    {
        var vision = VisionPool.Instance.Rent();
        vision.Eye = eye;
        vision.eyeLease = eye.Lease;

        vision.ScanOffset = random.Next(0, (int)DelayMs);

        _visionInitiated?.Fire(vision);
        Game.Current.InnerLoopAPIs.Delay(vision.ScanOffset, vision, ScanLoopBody);
        return vision;
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private static void ScanLoopBody(object obj)
    {
        var vision = (Vision)obj;
        if(vision.Eye.IsStillValid(vision.eyeLease) == false)
        {
            vision.TryDispose();
            return;
        }
        
        vision.Scan();
        Game.Current.InnerLoopAPIs.Delay(DelayMs + vision.ScanOffset, vision, ScanLoopBody);
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private void Scan()
    {
        RemoveStaleTrackedObjects();
        if (Eye.IsVisible == false) return;
        var buffer = ObstacleBufferPool.Instance.Rent();
        Eye.GetObstacles(buffer);
        FilterObstacles(buffer);
        try
        {
            for (int i = 0; i < buffer.WriteableBuffer.Count; i++)
            {
                GameCollider? element = buffer.WriteableBuffer[i];
                var visibleObject = TryTrack(element, buffer);
                if (visibleObject == null) continue;
                TrackedObjectsDictionary.Add(element,visibleObject);
                TrackedObjectsList.Add(visibleObject);
            }
        }
        finally
        {
            buffer.Dispose();
        }
        _visibleObjectsChanged?.Fire();
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private void RemoveStaleTrackedObjects()
    {
        for (var i = TrackedObjectsList.Count - 1; i >= 0; i--)
        {
            var trackedObject = TrackedObjectsList[i];
            if (trackedObject.IsTargetStillValid == false)
            {
                UnTrackAtIndex(i);
                continue;
            }

            if (trackedObject.Age > TimeSpan.FromSeconds(2))
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
        TrackedObjectsDictionary.Remove(trackedObject.Target);
        TrackedObjectsList.RemoveAt(index);
        trackedObject.TryDispose();
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private VisuallyTrackedObject? TryTrack(GameCollider potentialTarget, ObstacleBuffer buffer)
    {
        if(TrackedObjectsDictionary.ContainsKey(potentialTarget)) return null;
        if (potentialTarget.Velocity == null) return null;
        if (potentialTarget.CanCollideWith(Eye) == false && Eye.CanCollideWith(potentialTarget) == false) return null;
        if (potentialTarget.IsVisible == false) return null;

        var distance = Eye.CalculateNormalizedDistanceTo(potentialTarget);
        if (Range != float.MaxValue && distance > Range) return null;
        if (IsWithinFieldOfView(potentialTarget, out Angle angle) == false) return null;
        if (HasUnobstructedLineOfSight(potentialTarget, angle, buffer, out CollisionPrediction rayCastResult) == false) return null;

        return VisuallyTrackedObject.Create(potentialTarget, rayCastResult, distance, angle);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool HasUnobstructedLineOfSight(GameCollider potentialTarget, Angle angle, ObstacleBuffer buffer, out CollisionPrediction rayCastResult)
    {
        // 1. Fast path: cheap SingleRay test
        var singleRay = CollisionPredictionPool.Instance.Rent();
        CollisionDetector.Predict(Eye, angle, buffer.WriteableBuffer, Range * 2, CastingMode.SingleRay, buffer.WriteableBuffer.Count, singleRay);
        var elementHit = singleRay.ColliderHit as GameCollider;
        singleRay.Dispose();
        if (elementHit != potentialTarget)
        {
            // Blocked by something else (fast fail)
            rayCastResult = null!;
            return false;
        }
        
        // 2. Only if possibly visible, do the full (slow) test
        rayCastResult = CollisionPredictionPool.Instance.Rent();
        CollisionDetector.Predict(Eye, angle, buffer.WriteableBuffer, Range * 2, CastingMode.Precise, buffer.WriteableBuffer.Count, rayCastResult);
        var preciseHit = rayCastResult.ColliderHit as GameCollider;
        var ret = preciseHit == potentialTarget;
        if (!ret) rayCastResult.TryDispose();
        return ret;
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private bool IsWithinFieldOfView(GameCollider potentialTarget, out Angle angle)
    {
        angle = Eye.CalculateAngleTo(potentialTarget.Bounds);
        var delta = Eye.Velocity.Angle.DiffShortest(angle);
        return delta <= AngularVisibility;
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private void FilterObstacles(ObstacleBuffer buffer)
    {
        for (var i = 0; i < buffer.WriteableBuffer.Count; i++)
        {
            var obstacle = buffer.WriteableBuffer[i];
            if (IsIgnoredByFilter(obstacle))
            {
                buffer.WriteableBuffer.RemoveAt(i);
                i--;
            }
        }
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private bool IsIgnoredByFilter(GameCollider potentialTarget)
    {
        targetFilterContext.Reset(potentialTarget);
        _targetBeingEvaluated?.Fire(targetFilterContext);
        return targetFilterContext.Ignored;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Eye = null!;
        eyeLease = 0;
        Range = 100;
        AngularVisibility = 60;
        _targetBeingEvaluated?.Dispose();
        _targetBeingEvaluated = null;
        TrackedObjectsDictionary.Clear();
        TrackedObjectsList.Clear();
        _visibleObjectsChanged?.TryDispose();
        _visibleObjectsChanged = null;
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

public class VisuallyTrackedObject : Recyclable
{
    private int targetLease;
    public GameCollider Target { get; private set; }
    public TimeSpan CreatedTime { get; private set; }
    public CollisionPrediction RayCastResult { get; set; }
    public float Distance { get; set; }
    public Angle Angle { get; set; }

    public TimeSpan Age => Game.Current.MainColliderGroup.Now - CreatedTime;

    public bool IsTargetStillValid => Target.IsStillValid(targetLease);

    public VisuallyTrackedObject() { }

    public static VisuallyTrackedObject Create(GameCollider target, CollisionPrediction rayCastResult, float distance, Angle angle)
    {
        var trackedObject = VisuallyTrackedObjectPool.Instance.Rent();
        trackedObject.targetLease = target.Lease;
        trackedObject.Target = target;
        trackedObject.CreatedTime = Game.Current.MainColliderGroup.Now;
        trackedObject.RayCastResult = rayCastResult;
        trackedObject.Distance = distance;
        trackedObject.Angle = angle;
        return trackedObject;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Target = null;
        CreatedTime = TimeSpan.Zero;
        RayCastResult?.TryDispose();
        RayCastResult = null!;
        Distance = default;
        Angle = default;
        targetLease = default;
    }
}
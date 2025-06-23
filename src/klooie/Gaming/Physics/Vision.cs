using System.Runtime.CompilerServices;

namespace klooie.Gaming;
public class Vision : Recyclable, IFrameTask
{
    public const float DefaultRange = 20;
    public const float DefaultAngularVisibility = 60;
    private static Event<Vision>? _visionInitiated;
    public static Event<Vision> VisionInitiated => _visionInitiated ??= Event<Vision>.Create();
    private static FrameTaskScheduler? Scheduler;

    private static void EnsureScheduler()
    {
        if (Scheduler != null) return;
        Scheduler = FrameTaskScheduler.Create(667f);
        ConsoleApp.Current.OnDisposed(static () =>
        {
            Scheduler?.TryDispose();
            Scheduler = null;
        });
    }


    private Event? _visibleObjectsChanged;
    public Event VisibleObjectsChanged => _visibleObjectsChanged ??= Event.Create();


    private VisionFilterContext targetFilterContext = new VisionFilterContext();
    private Event<VisionFilterContext>? _targetBeingEvaluated;
    public List<VisuallyTrackedObject> TrackedObjectsList { get; private set; } = new List<VisuallyTrackedObject>();
    public Dictionary<GameCollider, VisuallyTrackedObject> TrackedObjectsDictionary { get; private set; } = new Dictionary<GameCollider, VisuallyTrackedObject>();
    public Event<VisionFilterContext> TargetBeingEvaluated => _targetBeingEvaluated ?? (_targetBeingEvaluated = Event<VisionFilterContext>.Create());
    public GameCollider Eye { get; private set; } = null!;
    public float Range { get; set; } 
    public float AngularVisibility { get; set; } 
    public CastingMode CastingMode { get; set; }
    public float AutoScanFrequency { get; set; }
    public float AngleStep {get;set;}
    public int AngleFuzz { get; set; }
    public  TimeSpan MaxMemoryTime { get; set; }
    public TimeSpan LastExecutionTime { get; set; }
    public Vision() { }

    private static Random random = new Random();
    public static Vision Create(GameCollider eye, bool autoScan = true)
    {
        var vision = VisionPool.Instance.Rent();
        vision.Eye = eye;
        vision.AutoScanFrequency = 667f;
        vision.AngleStep = 5;
        vision.AngleFuzz = 2;
        vision.CastingMode = CastingMode.SingleRay;
        vision.MaxMemoryTime = TimeSpan.FromSeconds(2);
        _visionInitiated?.Fire(vision);

        if (autoScan)
        {
            EnsureScheduler();
            Scheduler!.Enqueue(vision);
        }
        return vision;
    }

    protected override void OnInit()
    {
        base.OnInit();
        Range = DefaultRange;
        AngularVisibility = DefaultAngularVisibility;
    }

    public Angle FieldOfViewStart => Eye.Velocity.Angle.Add(-AngularVisibility / 2f);
    public Angle FieldOfViewEnd => Eye.Velocity.Angle.Add(AngularVisibility / 2f);

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    public void Scan()
    {
        RemoveStaleTrackedObjects();
        if (Eye.IsVisible == false) return;
        var buffer = ObstacleBufferPool.Instance.Rent();
        Eye.ColliderGroup.SpacialIndex.Query(Eye.Bounds.SweptAABB(Eye.Bounds.RadialOffset(Eye.Velocity.Angle, Range)), buffer);
        FilterObstacles(buffer);
        try
        {
            if (TryPerfestScan(buffer) == false)
            {
                ApproximateScan(buffer);
            }
        }
        finally
        {
            buffer.Dispose();
        }
        _visibleObjectsChanged?.Fire();
    }

    private bool TryPerfestScan(ObstacleBuffer buffer)
    {
        if (AngleStep > 1) return false;
        if(MaxMemoryTime > TimeSpan.Zero) throw new InvalidOperationException($"When {nameof(AngleStep)} is <= 1 then MaxMemoryTime must be <= TimeSpan.Zero.");
        if(TrackedObjectsList.Count > 0) throw new InvalidOperationException($"When {nameof(AngleStep)} is <= 1 then TrackedObjectsList must be empty."); 
        
        for (var i = 0; i < buffer.WriteableBuffer.Count; i++)
        {
            var distance = Eye.CalculateNormalizedDistanceTo(buffer.WriteableBuffer[i]);
            if (distance > Range) continue;

            var angle = Eye.CalculateAngleTo(buffer.WriteableBuffer[i]);
            var angleDiff = Eye.Velocity.Angle.DiffShortest(angle);
            if (angleDiff > AngularVisibility) continue;

            var prediction = CollisionPredictionPool.Instance.Rent();
            var obstruction = CollisionDetector.GetLineOfSightObstruction(Eye, buffer.WriteableBuffer[i], buffer.WriteableBuffer, CastingMode.Precise, prediction) as GameCollider;
            VisuallyTrackedObject? target = null;
            if (obstruction != null || TryIgnorePotentialTargetIgnorable(buffer.WriteableBuffer[i], out target))
            {
                if (target != null)
                {
                    throw new InvalidOperationException($"Target was present in visible objects, but should have been marked as stale.");
                }
                prediction.TryDispose();
                continue;
            }

            var newItem =  VisuallyTrackedObject.Create(buffer.WriteableBuffer[i], prediction, prediction.LKGD, distance);
            TrackedObjectsDictionary.Add(newItem.Target, newItem);
            TrackedObjectsList.Add(newItem);
        }
        return true;
    }

    private void ApproximateScan(ObstacleBuffer buffer)
    {
        var directlyAheadResult = Cast(Eye.Velocity.Angle, buffer);
        if (directlyAheadResult != null)
        {
            TrackedObjectsDictionary.Add(directlyAheadResult.Target, directlyAheadResult);
            TrackedObjectsList.Add(directlyAheadResult);
        }

        var currentAngle = FieldOfViewStart;
        var totalTravel = 0f;

        while (totalTravel <= AngularVisibility)
        {
            var visibleObject = Cast(currentAngle.Add(AngleFuzz == 0 ? 0 : Random.Shared.Next(-AngleFuzz, AngleFuzz)), buffer);
            if (visibleObject != null)
            {
                TrackedObjectsDictionary.Add(visibleObject.Target, visibleObject);
                TrackedObjectsList.Add(visibleObject);
            }
            totalTravel += AngleStep;
            currentAngle = currentAngle.Add(AngleStep);
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

            if(trackedObject.Target.CalculateNormalizedDistanceTo(Eye) > Range)
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

    private void UntrackAll()
    {
        for(var i = 0; i < TrackedObjectsList.Count; i++)
        {
            TrackedObjectsList [i].TryDispose();
        }
        TrackedObjectsList.Clear();
        TrackedObjectsDictionary.Clear();
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    private VisuallyTrackedObject? Cast(Angle angle, ObstacleBuffer buffer)
    {
        var singleRay = CollisionPredictionPool.Instance.Rent();
        CollisionDetector.Predict(Eye, angle, buffer.WriteableBuffer, Range, CastingMode, buffer.WriteableBuffer.Count, singleRay);
        var potentialTarget = singleRay.ColliderHit as GameCollider;

        if (TryIgnorePotentialTargetIgnorable(potentialTarget, out var target))
        {
            if (target != null)
            {
                target.LastSeenTime = Game.Current.MainColliderGroup.Now;
                target.Distance = Eye.CalculateNormalizedDistanceTo(potentialTarget);
            }
            singleRay.TryDispose();
            return null;
        }

        return VisuallyTrackedObject.Create(potentialTarget,singleRay,singleRay.LKGD,angle);
    }

    private bool TryIgnorePotentialTargetIgnorable(GameCollider? potentialTarget, out VisuallyTrackedObject existing)
    {
        VisuallyTrackedObject existingTarget = null;
        var alreadyTracked = potentialTarget != null && TrackedObjectsDictionary.TryGetValue(potentialTarget, out existingTarget);
        var shouldBeIgnored = alreadyTracked || potentialTarget == null || potentialTarget?.Velocity == null || potentialTarget.IsVisible == false || potentialTarget.CanCollideWith(Eye) == false || Eye.CanCollideWith(potentialTarget) == false;
        existing = existingTarget;
        return shouldBeIgnored;
    }

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

    string IFrameTask.Name => nameof(Vision);
    void IFrameTask.Execute() => Scan();

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
        Range = DefaultRange;
        AngularVisibility = DefaultAngularVisibility;
        _targetBeingEvaluated?.TryDispose();
        _targetBeingEvaluated = null;
        TrackedObjectsDictionary.Clear();

        for (var i = TrackedObjectsList.Count - 1; i >= 0; i--)
        {
            UnTrackAtIndex(i);
        }

        TrackedObjectsList.Clear();
        _visibleObjectsChanged?.TryDispose();
        _visibleObjectsChanged = null;
        LastExecutionTime = TimeSpan.Zero;
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

    public TimeSpan TimeSinceLastSeen => Game.Current.MainColliderGroup.Now - LastSeenTime;

    public bool IsTargetStillValid => Target != null && Target.IsStillValid(targetLease);

    public VisuallyTrackedObject() { }

    public static VisuallyTrackedObject Create(GameCollider target, CollisionPrediction rayCastResult, float distance, Angle angle)
    {
        var trackedObject = VisuallyTrackedObjectPool.Instance.Rent();
        trackedObject.targetLease = target.Lease;
        trackedObject.Target = target;
        trackedObject.LastSeenTime = Game.Current.MainColliderGroup.Now;
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
        RayCastResult?.TryDispose();
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

    public Vision Vision { get; set; } 
    public VisionFilter(Vision vision)
    {
        Vision = vision;
    }

    public void Filter(ConsoleBitmap bitmap)
    {
        if(Control is GameCollider collider == false || Vision.TrackedObjectsDictionary.TryGetValue(collider, out VisuallyTrackedObject vto) == false)
        {
            bitmap.Fill(NotSeen);
            return;
        }

        var ageSeconds = (float)vto.TimeSinceLastSeen.TotalSeconds;
        var staleness =  ageSeconds / (float)Vision.MaxMemoryTime.TotalSeconds;

        bitmap.Fill(SeenNow.ToOther(NotSeen, staleness)); 
    }
}
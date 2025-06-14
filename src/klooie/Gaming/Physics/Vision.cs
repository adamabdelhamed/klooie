﻿using System.Runtime.CompilerServices;

namespace klooie.Gaming;
public class Vision : Recyclable
{
    public const float DefaultRange = 20;
    public const float DefaultAngularVisibility = 60;
    private static Event<Vision>? _visionInitiated;
    public static Event<Vision> VisionInitiated => _visionInitiated ??= Event<Vision>.Create();


    private Event? _visibleObjectsChanged;
    public Event VisibleObjectsChanged => _visibleObjectsChanged ??= Event.Create();

    private const float AutoScanFrequency = 667;

    private VisionFilterContext targetFilterContext = new VisionFilterContext();
    private Event<VisionFilterContext>? _targetBeingEvaluated;
    public List<VisuallyTrackedObject> TrackedObjectsList { get; private set; } = new List<VisuallyTrackedObject>();
    public Dictionary<GameCollider, VisuallyTrackedObject> TrackedObjectsDictionary { get; private set; } = new Dictionary<GameCollider, VisuallyTrackedObject>();
    public Event<VisionFilterContext> TargetBeingEvaluated => _targetBeingEvaluated ?? (_targetBeingEvaluated = Event<VisionFilterContext>.Create());
    public GameCollider Eye { get; private set; } = null!;
    public float Range { get; set; } 
    public float AngularVisibility { get; set; } 
    public float ScanOffset { get; set; }
    public Vision() { }

    private static Random random = new Random();
    private static int NextScanOffsetBase = 0;
    private const int MinScanSpacing = 30; // ms (set as desired)
    public static Vision Create(GameCollider eye, bool autoScan = true)
    {
        var vision = VisionPool.Instance.Rent();
        vision.Eye = eye;
        vision.ScanOffset = (NextScanOffsetBase++ * MinScanSpacing) % AutoScanFrequency;

        _visionInitiated?.Fire(vision);
        if (autoScan)
        {
            Game.Current.InnerLoopAPIs.DelayIfValid(vision.ScanOffset, VisionDependencyState.Create(vision), ScanLoopBody);
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
    private static void ScanLoopBody(object obj)
    {
        var state = (VisionDependencyState)obj;
        if(state.AreAllDependenciesValid == false)
        {
            state.Dispose();
            return;
        }
        FrameDebugger.RegisterTask(nameof(Vision));
        state.Vision.Scan();
        Game.Current.InnerLoopAPIs.Delay(AutoScanFrequency + state.Vision.ScanOffset, state, ScanLoopBody);
    }

    [method: MethodImpl(MethodImplOptions.NoInlining)]
    public void Scan()
    {
        RemoveStaleTrackedObjects();
        if (Eye.IsVisible == false) return;
        var buffer = ObstacleBufferPool.Instance.Rent();
        Eye.GetObstacles(buffer);
        FilterObstacles(buffer);
        try
        {
            var directlyAheadResult = Cast(Eye.Velocity.Angle, buffer);   
            if (directlyAheadResult != null)
            {
                TrackedObjectsDictionary.Add(directlyAheadResult.Target, directlyAheadResult);
                TrackedObjectsList.Add(directlyAheadResult);
            }

            var currentAngle = FieldOfViewStart;
            var totalTravel = 0f;
            var angleStep = 5f;
            var angleFuzz = 2;
            while(totalTravel <= AngularVisibility)
            {
                var visibleObject = Cast(currentAngle.Add(Random.Shared.Next(-angleFuzz, angleFuzz)), buffer);
                if (visibleObject != null)
                {
                    TrackedObjectsDictionary.Add(visibleObject.Target, visibleObject);
                    TrackedObjectsList.Add(visibleObject);
                }
                totalTravel += angleStep;
                currentAngle = currentAngle.Add(angleStep);
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

            if (trackedObject.TimeSinceLastSeen > VisuallyTrackedObject.MaxMemoryTime)
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
    private VisuallyTrackedObject? Cast(Angle angle, ObstacleBuffer buffer)
    {
        var singleRay = CollisionPredictionPool.Instance.Rent();
        CollisionDetector.Predict(Eye, angle, buffer.WriteableBuffer, Range, CastingMode.SingleRay, buffer.WriteableBuffer.Count, singleRay);
        var potentialTarget = singleRay.ColliderHit as GameCollider;
   
        if (potentialTarget != null && TrackedObjectsDictionary.TryGetValue(potentialTarget, out var target))
        {
            target.LastSeenTime = Game.Current.MainColliderGroup.Now;
            singleRay.TryDispose();
            return null;
        }

        if(potentialTarget == null || 
            potentialTarget?.Velocity == null ||
            potentialTarget.IsVisible == false || 
            potentialTarget.CanCollideWith(Eye) == false || 
            Eye.CanCollideWith(potentialTarget) == false)
        {
            singleRay.TryDispose();
            return null;
        }

        return VisuallyTrackedObject.Create(potentialTarget,singleRay,singleRay.LKGD,angle);
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
        v.Eye.OnDisposed(v,Recyclable.TryDisposeMe);
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
    public static readonly TimeSpan MaxMemoryTime = TimeSpan.FromSeconds(2);
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
        var staleness =  ageSeconds / (float)VisuallyTrackedObject.MaxMemoryTime.TotalSeconds;

        bitmap.Fill(SeenNow.ToOther(NotSeen, staleness)); 
    }
}
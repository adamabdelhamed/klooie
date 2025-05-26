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
    public static Vision Create(GameCollider eye, bool autoScan = true)
    {
        var vision = VisionPool.Instance.Rent();
        vision.Eye = eye;
        vision.eyeLease = eye.Lease;

        vision.ScanOffset = random.Next(0, (int)DelayMs);

        _visionInitiated?.Fire(vision);
        if (autoScan)
        {
            Game.Current.InnerLoopAPIs.Delay(vision.ScanOffset, vision, ScanLoopBody);
        }
        return vision;
    }

    public Angle FieldOfViewStart => Eye.Velocity.Angle.Add(-AngularVisibility / 2f);
    public Angle FieldOfViewEnd => Eye.Velocity.Angle.Add(AngularVisibility / 2f);

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
            while(totalTravel <= AngularVisibility)
            {
                var visibleObject = Cast(currentAngle, buffer);
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
    private VisuallyTrackedObject? Cast(Angle angle, ObstacleBuffer buffer)
    {
        var singleRay = CollisionPredictionPool.Instance.Rent();
        CollisionDetector.Predict(Eye, angle, buffer.WriteableBuffer, Range, CastingMode.SingleRay, buffer.WriteableBuffer.Count, singleRay);
        var potentialTarget = singleRay.ColliderHit as GameCollider;
   
        if(potentialTarget == null || 
            potentialTarget?.Velocity == null ||
            potentialTarget.IsVisible == false || 
            TrackedObjectsDictionary.ContainsKey(potentialTarget) || 
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

    private IEnumerable<Edge> GetClosestEdges(RectF rect, LocF point, float epsilon = 0.01f)
    {
        if (Math.Abs(point.Left - rect.Left) < epsilon)
            yield return rect.LeftEdge;
        if (Math.Abs(point.Left - rect.Right) < epsilon)
            yield return rect.RightEdge;
        if (Math.Abs(point.Top - rect.Top) < epsilon)
            yield return rect.TopEdge;
        if (Math.Abs(point.Top - rect.Bottom) < epsilon)
            yield return rect.BottomEdge;
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

public class VisionFilter : IConsoleControlFilter
{
    public ConsoleControl Control { get; set; }

    public Vision Vision { get; set; } 
    public VisionFilter(Vision vision)
    {
        Vision = vision;
    }

    public void Filter(ConsoleBitmap bitmap)
    {
        if(Control is GameCollider collider == false || Vision.TrackedObjectsDictionary.ContainsKey(collider) == false)
        {
            bitmap.Fill(RGB.Gray);
            return;
        }
                
        bitmap.Fill(RGB.Green); 
    }
}
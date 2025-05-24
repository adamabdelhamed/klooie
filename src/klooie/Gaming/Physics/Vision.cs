namespace klooie.Gaming;
public class Vision : Recyclable
{
    private static Event<Vision>? _visionInitiated;
    public static Event<Vision> VisionInitiated => _visionInitiated ??= EventPool<Vision>.Instance.Rent();


    private Event? _visibleObjectsChanged;
    public Event VisibleObjectsChanged => _visibleObjectsChanged ??= EventPool.Instance.Rent();

    private const float DelayMs = 333;
    private int eyeLease = 0;
    private VisionFilterContext targetFilterContext = new VisionFilterContext();
    private Event<VisionFilterContext>? _targetBeingEvaluated;
    public List<VisuallyTrackedObject> TrackedObjects { get; private set; } = new List<VisuallyTrackedObject>();
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

    private void Scan()
    {
        TrackedObjects.Clear();
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
                TrackedObjects.Add(visibleObject);
            }
        }
        finally
        {
            buffer.Dispose();
        }
        _visibleObjectsChanged?.Fire();
    }

    private VisuallyTrackedObject? TryTrack(GameCollider potentialTarget, ObstacleBuffer buffer)
    {
        if (potentialTarget.Velocity == null) return null;
        if (potentialTarget.CanCollideWith(Eye) == false && Eye.CanCollideWith(potentialTarget) == false) return null;
        if (potentialTarget.IsVisible == false) return null;

        var distance = Eye.CalculateNormalizedDistanceTo(potentialTarget);
        if (Range != float.MaxValue && distance > Range) return null;
        if (IsWithinFieldOfView(potentialTarget, out Angle angle) == false) return null;
        if (HasUnobstructedLineOfSight(potentialTarget, angle, buffer, out CollisionPrediction rayCastResult) == false) return null;

        return VisuallyTrackedObject.Create(potentialTarget, rayCastResult, distance, angle);
    }

    private bool HasUnobstructedLineOfSight(GameCollider potentialTarget, Angle angle, ObstacleBuffer buffer, out CollisionPrediction rayCastResult)
    {
        rayCastResult = CollisionPredictionPool.Instance.Rent();
        CollisionDetector.Predict(Eye, angle, buffer.WriteableBuffer, Range * 2, CastingMode.Precise, buffer.WriteableBuffer.Count, rayCastResult);
        var elementHit = rayCastResult.ColliderHit as GameCollider;
        var ret = elementHit == potentialTarget;
        if(ret == false) rayCastResult.TryDispose();
        return ret;
    }

    private bool IsWithinFieldOfView(GameCollider potentialTarget, out Angle angle)
    {
        angle = Eye.CalculateAngleTo(potentialTarget.Bounds);
        var delta = Eye.Velocity.Angle.DiffShortest(angle);
        return delta <= AngularVisibility;
    }

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
        TrackedObjects.Clear();
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
    public GameCollider Target { get; private set; }
    public TimeSpan LastSeenTime { get; private set; }
    private CollisionPrediction RayCastResult { get; set; }
    public float Distance { get; set; }
    public Angle Angle { get; set; }

    public VisuallyTrackedObject() { }

    public static VisuallyTrackedObject Create(GameCollider target, CollisionPrediction rayCastResult, float distance, Angle angle)
    {
        var trackedObject = VisuallyTrackedObjectPool.Instance.Rent();
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
    }
}
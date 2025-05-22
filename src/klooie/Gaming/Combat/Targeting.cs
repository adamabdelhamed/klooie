namespace klooie.Gaming;
public class TargetingOptions
{
    public bool HighlightTargets { get; set; }
    public required GameCollider Source { get; set; }
    public string? TargetTag { get; set; }
    public float AngularVisibility { get; set; } = 60;
    public float Range { get; set; } = Targeting.MaxVisibility;
    public float Delay { get; set; } = 500;
}

public class TargetFilterContext
{
    internal bool Ignored { get; private set; }
    public GameCollider PotentialTarget { get; internal set; }

    internal void Reset(GameCollider toBind)
    {
        Ignored = false;
        PotentialTarget = toBind;
    }

    public void IgnoreTargeting() => Ignored = true;
}

public class Targeting : Recyclable
{
    public const float MaxVisibility = 1000;

    private TargetFilterContext targetFilterContext = new TargetFilterContext();

    private Event<GameCollider?>? _targetChanged;
    private Event<GameCollider>? _targetAcquired;
    private Event<TargetFilterContext>? _targetBeingEvaluated;
    private static Event<Targeting>? _targetingInitiated;
    private Recyclable? currentTargetLifetime;
    private static TargetFilter targetFilter = new TargetFilter();



    public Event<GameCollider?> TargetChanged => _targetChanged ?? (_targetChanged = EventPool<GameCollider?>.Instance.Rent());
    public Event<GameCollider> TargetAcquired => _targetAcquired ?? (_targetAcquired = EventPool<GameCollider>.Instance.Rent());

    public static Event<Targeting> TargetingInitiated => _targetingInitiated ?? (_targetingInitiated = EventPool<Targeting>.Instance.Rent());

    public Event<TargetFilterContext> TargetBeingEvaluated => _targetBeingEvaluated ?? (_targetBeingEvaluated = EventPool<TargetFilterContext>.Instance.Rent());

    public GameCollider? Target { get; private set; }

    public TargetingOptions Options { get; private set; }

    public GameCollider Source => Options.Source;

    public ILifetime? CurrentTargetLifetime => currentTargetLifetime;

    protected override void OnInit()
    {
        base.OnInit();
        _targetingInitiated?.Fire(this);
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        if (_targetChanged != null) _targetChanged.TryDispose();
        if (_targetAcquired != null) _targetAcquired.TryDispose();
        if (_targetBeingEvaluated != null) _targetBeingEvaluated.TryDispose();
        if (_targetingInitiated != null) _targetingInitiated.TryDispose();
        if (currentTargetLifetime != null) currentTargetLifetime.TryDispose();


        _targetChanged = null;
        _targetAcquired = null;
        _targetBeingEvaluated = null;
        _targetingInitiated = null;
        currentTargetLifetime = null;
    }

    public void Bind(TargetingOptions options, TargetingQueue queue)
    {
        this.Options = options;
        options.Source.OnDisposed(this, DisposeMe);
        queue.Add(this);
    }

    private static void DisposeMe(object me) => (me as Targeting)?.TryDispose();
 

    /// <summary>
    /// This method now processes the evaluation immediately (once dequeued).
    /// </summary>
    public void Evaluate()
    {
        if (Options.Range > MaxVisibility)
            throw new ArgumentException($"Range cannot exceed {MaxVisibility}");
        if (Options.Source.IsVisible == false) return;

        var buffer = ObstacleBufferPool.Instance.Rent();
        Options.Source.GetObstacles(buffer);
        try
        {
            GameCollider? newTarget = null;
            float closestPotentialTargetDistance = float.MaxValue;
            for (int i = 0; i < buffer.WriteableBuffer.Count; i++)
            {
                GameCollider? element = buffer.WriteableBuffer[i];
                if (MeetsTargetingCriteria(element, buffer) == false) continue;
                var potentialTargetDistance = Options.Source.CalculateNormalizedDistanceTo(element);
                if (potentialTargetDistance >= closestPotentialTargetDistance) continue;
                closestPotentialTargetDistance = potentialTargetDistance;
                newTarget = element;
            }

            OnTargetChanged(newTarget);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    private bool MeetsTargetingCriteria(GameCollider potentialTarget, ObstacleBuffer buffer)
    {
        var lease = Lease;
        if (potentialTarget.Velocity == null) return false;
        if (potentialTarget.CanCollideWith(this.Options.Source) == false &&
            this.Options.Source.CanCollideWith(potentialTarget) == false) return false;
        if (Options.TargetTag != null && potentialTarget.HasSimpleTag(Options.TargetTag) == false) return false;
        if (potentialTarget.IsVisible == false) return false;
        if (Options.Range != float.MaxValue &&
         potentialTarget.CalculateNormalizedDistanceTo(Options.Source) > Options.Range) return false;

        targetFilterContext.Reset(potentialTarget);
        _targetBeingEvaluated?.Fire(targetFilterContext); // todo - Peek immunity, filtering out weapon elements, and concealment can be untangled from this class using this event
        if (targetFilterContext.Ignored) return false;

        var angle = Options.Source.CalculateAngleTo(potentialTarget.Bounds);
        var delta = Options.Source.Velocity.Angle.DiffShortest(angle);
        if (delta >= Options.AngularVisibility) return false;

        var lineOfSightCast = CollisionPredictionPool.Instance.Rent();

        for (var i = 0; i < buffer.WriteableBuffer.Count; i++)
        {
            var obstacle = buffer.WriteableBuffer[i];
            targetFilterContext.Reset(obstacle);
            _targetBeingEvaluated?.Fire(targetFilterContext);
            if (targetFilterContext.Ignored)
            {
                buffer.WriteableBuffer.RemoveAt(i);
                i--;
            }
        }
        if (IsStillValid(lease) == false) return false;
        CollisionDetector.Predict(Options.Source, angle, buffer.WriteableBuffer, Options.Range * 3, CastingMode.Precise, buffer.WriteableBuffer.Count, lineOfSightCast);
        var elementHit = lineOfSightCast.ColliderHit as GameCollider;
        lineOfSightCast.Dispose();
        if (elementHit != potentialTarget) return false;

        return true;
    }

    private void OnTargetChanged(GameCollider? newTarget)
    {
        if (newTarget == Target) return;
        currentTargetLifetime?.TryDispose();
        currentTargetLifetime = newTarget == null ? null : DefaultRecyclablePool.Instance.Rent();
        newTarget?.IsVisibleChanged.Subscribe(this, OnTargetVisibilityChanged, currentTargetLifetime);
        newTarget?.Velocity.Group.Removed.Subscribe(this, OnPotentialTargetRemoved, currentTargetLifetime);

        ProcessHighlightFiltering(newTarget);
        Target = newTarget;
        _targetChanged?.Fire(newTarget);
        if (newTarget == null) return;
        _targetAcquired?.Fire(newTarget);
    }

    private void ProcessHighlightFiltering(GameCollider? newTarget)
    {
        if (Options.HighlightTargets == false) return;

        if (Target?.HasFilters == true)
        {
            for (var i = 0; i < Target.Filters.Count; i++)
            {
                if (ReferenceEquals(targetFilter, Target.Filters[i]))
                {
                    Target.Filters.RemoveAt(i);
                    break;
                }
            }
        }

        if (newTarget != null && (newTarget.HasFilters == false || newTarget.Filters.Contains(targetFilter) == false))
        {
            newTarget.Filters.Add(targetFilter);
        }
    }

    private static void OnTargetVisibilityChanged(object me)
    {
        var _this = (Targeting)me;
        if (_this.Target?.IsVisible == true) return;
        _this.OnTargetChanged(null);
    }

    private static void OnPotentialTargetRemoved(object me, object colliderObj)
    {

        var collider = (GameCollider)colliderObj;
        var _this = (me as Targeting)!;
        if (ReferenceEquals(_this.Target, collider) == false) return;
        _this.OnTargetChanged(null);
    }
}


public class TargetFilter : IConsoleControlFilter
{
    public ConsoleControl Control { get; set; }

    public void Filter(ConsoleBitmap bitmap)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                var pix = bitmap.GetPixel(x, y);
                bitmap.SetPixel(x, y, new ConsoleCharacter(pix.Value, pix.ForegroundColor, pix.ForegroundColor.Darker));
            }
        }
    }
}

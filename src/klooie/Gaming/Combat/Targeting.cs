namespace klooie.Gaming;

public class TargetingOptions
{
    public bool HighlightTargets { get; set; }
    public required GameCollider Source { get; set; }
    public required Vision Vision { get; set; }
    public string? TargetTag { get; set; }
}

public class Targeting : Recyclable
{
    private static readonly TargetFilter targetFilter = new TargetFilter();

    public Recyclable? CurrentTargetLifetime { get; private set; }

    private Event<GameCollider?>? _targetChanged;
    private Event<GameCollider>? _targetAcquired;

    private int visionLease = 0; // Lease for the Vision instance at subscription time.
    private int colliderLease = 0; // Lease for the Source GameCollider at subscription time.

    public Event<GameCollider?> TargetChanged => _targetChanged ??= EventPool<GameCollider?>.Instance.Rent();
    public Event<GameCollider> TargetAcquired => _targetAcquired ??= EventPool<GameCollider>.Instance.Rent();

    public GameCollider? Target { get; private set; }
    public TargetingOptions Options { get; private set; } = null!;

    public void Bind(TargetingOptions options)
    {
        Options = options;
        colliderLease = options.Source.Lease;
        visionLease = options.Vision.Lease;

        options.Source.OnDisposed(this, DisposeMe);

        // Subscribe to vision's event; this = lifetime for safe cleanup
        options.Vision.VisibleObjectsChanged.Subscribe(this, OnVisionChanged, this);
    }

    private static void DisposeMe(object me) => (me as Targeting)?.TryDispose();

    private static void OnVisionChanged(object me)
    {
        var targeting = (Targeting)me;
        // Defensive: Only act if Vision and Collider are still valid (don't check self)
        if (!targeting.Options.Vision.IsStillValid(targeting.visionLease)) return;
        if (!targeting.Options.Source.IsStillValid(targeting.colliderLease)) return;
        targeting.Evaluate();
    }

    public void Evaluate()
    {
        // Defensive: Only act if Vision and Collider are still valid
        if (!Options.Vision.IsStillValid(visionLease)) return;
        if (!Options.Source.IsStillValid(colliderLease)) return;

        if (!Options.Source.IsVisible)
            return;

        VisuallyTrackedObject? best = null;
        float closestDistance = float.MaxValue;
        foreach (var tracked in Options.Vision.TrackedObjects)
        {
            var tgt = tracked.Target;
            if (Options.TargetTag != null && !tgt.HasSimpleTag(Options.TargetTag))
                continue;

            // Add more custom per-target logic here if needed

            if (tracked.Distance < closestDistance)
            {
                closestDistance = tracked.Distance;
                best = tracked;
            }
        }

        OnTargetChanged(best?.Target);
    }

    private void OnTargetChanged(GameCollider? newTarget)
    {
        if (newTarget == Target) return;

        // Remove highlight filter from old target if needed
        if (Options.HighlightTargets && Target != null && Target.HasFilters)
        {
            for (int i = 0; i < Target.Filters.Count; i++)
            {
                if (ReferenceEquals(targetFilter, Target.Filters[i]))
                {
                    Target.Filters.RemoveAt(i);
                    break;
                }
            }
        }

        // Clean up previous
        CurrentTargetLifetime?.TryDispose();

        Target = newTarget;
        CurrentTargetLifetime = newTarget == null ? null : DefaultRecyclablePool.Instance.Rent();

        // Add highlight filter to new target if needed
        if (Options.HighlightTargets && newTarget != null)
        {
            if (!newTarget.HasFilters || !newTarget.Filters.Contains(targetFilter))
                newTarget.Filters.Add(targetFilter);
        }

        _targetChanged?.Fire(newTarget);
        if (newTarget != null)
            _targetAcquired?.Fire(newTarget);
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        _targetChanged?.TryDispose();
        _targetAcquired?.TryDispose();
        _targetChanged = null;
        _targetAcquired = null;
        Target = null;
        Options = null!;
        visionLease = 0;
        colliderLease = 0;
        CurrentTargetLifetime?.TryDispose();
        CurrentTargetLifetime = null;
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
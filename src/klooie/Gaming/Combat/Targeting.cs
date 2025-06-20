namespace klooie.Gaming;


public class Targeting : Recyclable
{
    private static readonly TargetFilter targetFilter = new TargetFilter();
    public Recyclable? CurrentTargetLifetime { get; private set; }

    private Event<GameCollider?>? _targetChanged;
    private Event<GameCollider>? _targetAcquired;

    public Event<GameCollider?> TargetChanged => _targetChanged ??= Event<GameCollider?>.Create();
    public Event<GameCollider> TargetAcquired => _targetAcquired ??= Event<GameCollider>.Create();

    public GameCollider? Target { get; private set; }
    public bool HighlightTargets { get; set; }
    public Vision Vision { get; set; }
    public string[]? TargetTags { get; set; }

    public void Bind(Vision v, string[] targetTags, bool highlightTargets)
    {
        Vision = v ?? throw new ArgumentNullException(nameof(v));
        TargetTags = targetTags;
        HighlightTargets = highlightTargets;

        Vision.VisibleObjectsChanged.Subscribe(this, static me => me.Evaluate(), this);

        var ownershipTracker = LeaseHelper.TrackOwnerRelationship(this, Vision);
        Vision.OnDisposed(ownershipTracker, static tr =>
        {
            if(tr.IsOwnerValid) tr.TryDisposeOwner();
            tr.Dispose();
        });
    }

    private void Evaluate()
    {
        if (!Vision.Eye.IsVisible) return;

        VisuallyTrackedObject? best = null;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < Vision.TrackedObjectsList.Count; i++)
        {
            VisuallyTrackedObject? tracked = Vision.TrackedObjectsList[i];
            var tgt = tracked.Target;
            if (!IsPotentialTarget(tgt)) continue;

            if (tracked.Distance < closestDistance)
            {
                closestDistance = tracked.Distance;
                best = tracked;
            }
        }

        OnTargetChanged(best?.Target);
    }

    public bool IsPotentialTarget(GameCollider candidate)
    {
        if (candidate == null) return false;
        if (candidate.Velocity == this.Vision.Eye.Velocity) return false; // Don't target self

        if (TargetTags != null && TargetTags.Length > 0)
        {
            bool hasTag = false;
            foreach (var tag in TargetTags)
            {
                if (candidate.HasSimpleTag(tag))
                {
                    hasTag = true;
                    break;
                }
            }
            if (!hasTag) return false;
        }

        // You might want to add "not self" or other checks here if needed.
        return true;
    }

    private void OnTargetChanged(GameCollider? newTarget)
    {
        if (newTarget == Target) return;

        // Remove highlight filter from old target if needed
        ClearHighlightFilterFromCurrentTarget();

        // Clean up previous
        CurrentTargetLifetime?.TryDispose();

        Target = newTarget;
        CurrentTargetLifetime = newTarget == null ? null : DefaultRecyclablePool.Instance.Rent();

        // Add highlight filter to new target if needed
        if (HighlightTargets && newTarget != null)
        {
            if (!newTarget.HasFilters || !newTarget.Filters.Contains(targetFilter))
                newTarget.Filters.Add(targetFilter);
        }

        _targetChanged?.Fire(newTarget);
        if (newTarget != null)
            _targetAcquired?.Fire(newTarget);
    }

    private void ClearHighlightFilterFromCurrentTarget()
    {
        if (Target != null && Target.HasFilters)
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
    }

    public void RemoveNonTargets(ObstacleBuffer buffer)
    {
        for (var i = buffer.WriteableBuffer.Count - 1; i >= 0; i--)
        {
            if (IsPotentialTarget(buffer.WriteableBuffer[i]) == false)
            {
                buffer.WriteableBuffer.RemoveAt(i);
            }
        }
    }

    protected override void OnReturn()
    {
        ClearHighlightFilterFromCurrentTarget();
        _targetChanged?.TryDispose();
        _targetAcquired?.TryDispose();
        _targetChanged = null;
        _targetAcquired = null;
        Target = null;
        Vision = null;
        TargetTags = null;
        HighlightTargets = false;
        CurrentTargetLifetime?.TryDispose();
        CurrentTargetLifetime = null;
        base.OnReturn();
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
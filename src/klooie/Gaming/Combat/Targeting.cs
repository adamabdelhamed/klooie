namespace klooie.Gaming;


public class Targeting : Recyclable
{
    public Recyclable? CurrentTargetLifetime { get; private set; }

    private Event<GameCollider?>? _targetChanged;
    private Event<GameCollider>? _targetAcquired;

    public Event<GameCollider?> TargetChanged => _targetChanged ??= Event<GameCollider?>.Create();
    public Event<GameCollider> TargetAcquired => _targetAcquired ??= Event<GameCollider>.Create();

    public GameCollider? Target { get; private set; }
    public bool HighlightTargets { get; set; }
    public Vision Vision { get; set; }
    public string[]? TargetTags { get; private set; }

    public Angle TargetAngle => Target != null ? Vision.Eye.Center().CalculateAngleTo(Target.Center()) : Vision.Eye.Velocity.Angle;

    public void Bind(Vision v, string[] targetTags, bool highlightTargets)
    {
        Vision = v ?? throw new ArgumentNullException(nameof(v));
        TargetTags = targetTags;
        HighlightTargets = highlightTargets;

        Vision.VisibleObjectsChanged.Subscribe(this, static me => me.Evaluate(), this);
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

    public bool IsPotentialTarget(GameCollider? candidate)
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

        // Clean up previous
        CurrentTargetLifetime?.TryDispose();

        Target = newTarget;
        CurrentTargetLifetime = newTarget == null ? null : DefaultRecyclablePool.Instance.Rent();


        _targetChanged?.Fire(newTarget);
        if (newTarget != null)
            _targetAcquired?.Fire(newTarget);
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

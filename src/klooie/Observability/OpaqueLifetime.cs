namespace klooie;

/// <summary>
/// Wraps a lifetime so callers can observe it without getting access to the underlying implementation.
/// </summary>
public sealed class OpaqueLifetime : ILifetime
{
    private readonly ILifetime inner;
    private readonly int lease;

    public int Lease => lease;

    public OpaqueLifetime(ILifetime inner)
    {
        if (inner == null) throw new ArgumentNullException(nameof(inner));
        if(inner.IsStillValid(inner.Lease) == false) throw new ArgumentException("Inner lifetime must be valid at the time of construction", nameof(inner));
        this.inner = inner;
        lease = inner.Lease;
    }

    public bool IsStillValid(int lease) => lease == this.lease && inner.IsStillValid(this.lease);

    public void OnDisposed(Action cleanupCode)
    {
        if (cleanupCode == null) throw new ArgumentNullException(nameof(cleanupCode));

        if (IsStillValid(lease))
        {
            inner.OnDisposed(cleanupCode);
        }
        else
        {
            cleanupCode();
        }
    }

    public void OnDisposed<T>(T scope, Action<T> cleanupCode)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        if (cleanupCode == null) throw new ArgumentNullException(nameof(cleanupCode));

        if (IsStillValid(lease))
        {
            inner.OnDisposed(scope, cleanupCode);
        }
        else
        {
            cleanupCode(scope);
        }
    }
}

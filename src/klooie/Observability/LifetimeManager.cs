namespace klooie;

/// <summary>
/// An interface that defined the contract for associating cleanup
/// code with a lifetime
/// </summary>
public interface ILifetimeManager
{
    /// <summary>
    /// Registers the given cleanup code to run when the lifetime being
    /// managed by this manager ends
    /// </summary>
    /// <param name="cleanupCode">the code to run</param>
    /// <returns>a Task that resolves after the cleanup code runs</returns>
    void OnDisposed(Action cleanupCode);

    /// <summary>
    /// Registers the given disposable to dispose when the lifetime being
    /// managed by this manager ends
    /// </summary>
    /// <param name="obj">the object to dispose</param>
    /// <returns>a Task that resolves after the object is disposed</returns>
    void OnDisposed(IDisposable obj);

    void OnDisposed(Subscription obj);

    /// <summary>
    /// returns true if expired
    /// </summary>
    bool IsExpired { get; }

    /// <summary>
    /// returns true if expiring
    /// </summary>
    bool IsExpiring { get; }

    /// <summary>
    /// true if the lifetime is not expired or expiring
    /// </summary>
    bool ShouldContinue { get; }

    /// <summary>
    /// false if the lifetime is not expired or expiring
    /// </summary>
    bool ShouldStop { get; }
}

/// <summary>
/// Extension methods for lifetime managers
/// </summary>
public static class ILifetimeManagerEx
{
    /// <summary>
    /// Delays until this lifetime is complete
    /// </summary>
    /// <returns>an async task</returns>
    public static Task AsTask(this ILifetimeManager manager)
    {
        var tcs = new TaskCompletionSource();
        manager.OnDisposed(() => tcs.SetResult());
        return tcs.Task;
    }
}

/// <summary>
/// An implementation of ILifetimeManager
/// </summary>
internal sealed class LifetimeManager : ILifetimeManager
{
    private List<Subscription> toNotify;
    private List<Subscription> toDisposeOf;

    /// <summary>
    /// returns true if expired
    /// </summary>
    public bool IsExpired { get; internal set; }
    public bool IsExpiring { get; internal set; }
    public bool ShouldContinue => IsExpired == false && IsExpiring == false;

    public bool ShouldStop => !ShouldContinue;

    internal void Finish()
    {
        if(toDisposeOf != null)
        {
            foreach (var sub in toDisposeOf) SubscriptionPool.Return(sub);
            toDisposeOf.Clear();
        }
        if(toNotify != null)
        {
            NotificationBufferPool.Notify(toNotify);
            foreach (var sub in toNotify) SubscriptionPool.Return(sub);
            toNotify.Clear();
        }
    }

    private List<Subscription> ToNotify => toNotify ??= new List<Subscription>();
    private List<Subscription> ToDisposeOf => toDisposeOf ??= new List<Subscription>();

    /// <summary>
    /// Registers the given disposable to dispose when the lifetime being
    /// managed by this manager ends
    /// </summary>
    /// <param name="obj">the object to dispose</param>
    public void OnDisposed(IDisposable obj) => ToNotify.Add(SubscriptionPool.Rent(obj.Dispose, this));
    

    /// <summary>
    /// Registers the given cleanup code to run when the lifetime being
    /// managed by this manager ends
    /// </summary>
    /// <param name="cleanupCode">the code to run</param>
    public void OnDisposed(Action cleanupCode) => ToNotify.Add(SubscriptionPool.Rent(cleanupCode, this));

    public void OnDisposed(Subscription toDispose) => ToDisposeOf.Add(toDispose);
}
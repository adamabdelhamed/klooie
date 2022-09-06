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
    public static Task ToTask(this ILifetimeManager manager)
    {
        var tcs = new TaskCompletionSource();
        manager.OnDisposed(() => tcs.SetResult());
        return tcs.Task;
    }
}

/// <summary>
/// An implementation of ILifetimeManager
/// </summary>
internal class LifetimeManager : ILifetimeManager
{
    private List<Subscription> subscribers = new List<Subscription>();
    private List<SubscriptionWithParam> subscribersWithParams = new List<SubscriptionWithParam>();

    /// <summary>
    /// returns true if expired
    /// </summary>
    public bool IsExpired { get; internal set; }
    public bool IsExpiring { get; internal set; }
    public bool ShouldContinue => IsExpired == false && IsExpiring == false;

    internal void Finish() => NotificationBufferPool.Notify(subscribers, subscribersWithParams);

    /// <summary>
    /// Registers the given disposable to dispose when the lifetime being
    /// managed by this manager ends
    /// </summary>
    /// <param name="obj">the object to dispose</param>
    public void OnDisposed(IDisposable obj)
    {
        subscribers.Add(new Subscription()
        {
            Callback = () => obj.Dispose(),
            Lifetime = this,
        });
    }

    /// <summary>
    /// Registers the given cleanup code to run when the lifetime being
    /// managed by this manager ends
    /// </summary>
    /// <param name="cleanupCode">the code to run</param>
    public void OnDisposed(Action cleanupCode)
    {
        subscribers.Add(new Subscription()
        {
            Callback = cleanupCode,
            Lifetime = this,
        });
    }

    /// <summary>
    /// Registers the given cleanup code to run when the lifetime being
    /// managed by this manager ends
    /// </summary>
    /// <param name="cleanupCode">the code to run</param>
    /// <param name="param">the parameter to pass</param>
    public void OnDisposed(Action<object> cleanupCode, object param)
    {
        subscribersWithParams.Add(new SubscriptionWithParam()
        {
            Callback = cleanupCode,
            Lifetime = this,
            Param = param
        });
    }
}
using System.Collections.Concurrent;

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

    void OnDisposed(object scope, Action<object> cleanupCode);

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
public sealed class LifetimeManager : ILifetimeManager
{
    private List<Subscription>? toNotify;
    private List<Subscription>? toDisposeOf;

    /// <summary>
    /// returns true if expired
    /// </summary>
    public bool IsExpired { get; internal set; }
    public bool IsExpiring { get; internal set; }
    public bool ShouldContinue => IsExpired == false && IsExpiring == false;

    public bool ShouldStop => !ShouldContinue;

    private bool hasFinished;

    public LifetimeManager()
    {
        Initialize();
    }

    internal void Initialize()
    {
        IsExpired = false;
        IsExpiring = false;
        hasFinished = false;
        toNotify = null;
        toDisposeOf = null;
    }

    public void Finish()
    {
        if (hasFinished) return;
        hasFinished = true;
        if (toDisposeOf != null)
        {
            foreach (var sub in toDisposeOf)
            {
                SubscriptionPool.Return(sub);
            }
            toDisposeOf.Clear();
            SubscriptionListPool.Return(toDisposeOf);
            toDisposeOf = null;
        }
        if(toNotify != null)
        {
            NotificationBufferPool.Notify(toNotify);
            foreach (var sub in toNotify) SubscriptionPool.Return(sub);
            toNotify.Clear();
            SubscriptionListPool.Return(toNotify);
            toNotify = null;
        }
    }

    private List<Subscription> ToNotify => toNotify ??= SubscriptionListPool.Rent();
    private List<Subscription> ToDisposeOf => toDisposeOf ??= SubscriptionListPool.Rent();

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

    /// <summary>
    /// Registers the given cleanup code to run when the lifetime being 
    /// managed by this manager ends. The cleanup code will be passed the
    /// scope object that was passed to the OnDisposed method.
    /// </summary>
    /// <param name="scope">your state used to avoid capturing a local</param>
    /// <param name="cleanupCode">the code to run</param>
    public void OnDisposed(object scope, Action<object> cleanupCode) => ToNotify.Add(SubscriptionPool.Rent(scope, cleanupCode, this));

    public void OnDisposed(Subscription toDispose) => ToDisposeOf.Add(toDispose);
}


public static class SubscriptionListPool
{
#if DEBUG
    public static int Created { get; private set; }
    public static int Rented { get; private set; }
    public static int Returned { get; private set; }
    public static int AllocationsSaved => Rented - Created;

#endif
    private static readonly ConcurrentBag<List<Subscription>> _pool = new ConcurrentBag<List<Subscription>>();

    internal static List<Subscription> Rent()
    {
#if DEBUG
        Rented++;
#endif
        if (_pool.TryTake(out var list))
        {
            list.Clear();
            return list;
        }

#if DEBUG
        Created++;
#endif

        return new List<Subscription>();
    }

    internal static void Return(List<Subscription> subscriptions)
    {
#if DEBUG
        Returned++;
#endif
        _pool.Add(subscriptions);
    }
}

public static class LifetimeManagerPool
{
#if DEBUG
    public static int Created { get; private set; }
    public static int Rented { get; private set; }
    public static int Returned { get; private set; }
    public static int AllocationsSaved => Rented - Created;

#endif
    private static readonly ConcurrentBag<LifetimeManager> _pool = new ConcurrentBag<LifetimeManager>();

    public static LifetimeManager Rent()
    {
#if DEBUG
        Rented++;
#endif
        if (_pool.TryTake(out var ltm))
        {
            ltm.Initialize();
            return ltm;
        }

#if DEBUG
        Created++;
#endif

        return new LifetimeManager();
    }

    public static void Return(LifetimeManager ltm)
    {
#if DEBUG
        Returned++;
#endif
        _pool.Add(ltm);
    }
}
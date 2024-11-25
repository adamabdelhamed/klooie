namespace klooie;

/// <summary>
/// An object that represents a time period that has
/// a beginning and an end
/// </summary>
public interface ILifetime : ILifetimeManager, IDisposable
{
    /// <summary>
    /// Disposes the object if it hasn't already been disposed
    /// </summary>
    /// <returns>true if the object was disposed by this call</returns>
    bool TryDispose();
}

/// <summary>
/// Extension methods for lifetime
/// </summary>
public static class ILifetimeEx
{
    /// <summary>
    /// Creates a new lifetime that will dispose when the parent disposes
    /// </summary>
    /// <param name="lt">the parent</param>
    /// <returns>the new lifetime</returns>
    public static Lifetime CreateChildLifetime(this ILifetime lt)
    {
        var ret = new Lifetime();
        lt.OnDisposed(() =>
        {
            if (ret.IsExpired == false)
            {
                ret.Dispose();
            }
        });
        return ret;
    }

    /// <summary>
    /// Converts a task to a lifetime
    /// </summary>
    /// <param name="t">the tast</param>
    /// <returns>a lifetime</returns>
    public static ILifetimeManager ToLifetime(this Task t, EventLoop loop = null)
    {
        loop = loop ?? ConsoleApp.Current;
        if(loop == null) throw new ArgumentException("ToLifetime() requires an event loop");

        var lt = new Lifetime();
        loop.Invoke(async () =>
        {
            await t;
            lt.Dispose();
        });
        return lt;
    }
}

/// <summary>
/// An object that has a beginning and and end  that can be used to define the lifespan of event and observable subscriptions.
/// </summary>
public class Lifetime : Disposable, ILifetime
{
    private LifetimeManager _manager;
    public ILifetimeManager Manager => _manager;
    private static readonly Lifetime forever = CreateForeverLifetime();

    private static Lifetime CreateForeverLifetime()
    {
        var ret = new Lifetime();
        ret.OnDisposed(() => throw new Exception("Forever lifetime expired"));
        return ret;
    }

    /// <summary>
    /// The forever lifetime manager that will never end. Any subscriptions you intend to keep forever should use this lifetime so it's easy to spot leaks.
    /// </summary>
    public static ILifetimeManager Forever => forever._manager;

    /// <summary>
    /// If true then this lifetime has already ended
    /// </summary>
    public bool IsExpired => _manager == null;

    /// <summary>
    /// returns true if the lifetime's Dispose() method is currently running, false otherwise
    /// </summary>
    public bool IsExpiring { get; private set; }

    /// <summary>
    /// Returns true if the lifetime is not expired or expiring
    /// </summary>
    public bool ShouldContinue => IsExpired == false && IsExpiring == false;


    /// <summary>
    /// Returns false if the lifetime is not expired or expiring
    /// </summary>
    public bool ShouldStop => !ShouldContinue;

    /// <summary>
    /// Creates a new lifetime
    /// </summary>
    public Lifetime() => _manager = new LifetimeManager();

    protected override void AfterDispose() => _manager.IsExpired = true;

    /// <summary>
    /// Delays until this lifetime is complete
    /// </summary>
    /// <returns>an async task</returns>
    public Task AsTask()
    {
        var tcs = new TaskCompletionSource<bool>();
        OnDisposed(SetResultTrue, tcs);
        return tcs.Task;
    }

    /// <summary>
    /// Registers an action to run when this lifetime ends
    /// </summary>
    /// <param name="cleanupCode">code to run when this lifetime ends</param>
    /// <returns>a promis that will resolve after the cleanup code has run</returns>
    public void OnDisposed(Action cleanupCode)
    {
        if (IsExpired == false)
        {
            _manager.OnDisposed(cleanupCode);
        }
    }

    /// <summary>
    /// Registers an action to run when this lifetime ends
    /// </summary>
    /// <param name="cleanupCode">code to run when this lifetime ends</param>
    /// <param name="param">a parameter to send to the callback</param>
    public void OnDisposed(Action<object> cleanupCode, object param)
    {
        if (IsExpired == false)
        {
            _manager.OnDisposed(cleanupCode, param);
        }
    }

    /// <summary>
    /// Registers a disposable to be disposed when this lifetime ends
    /// </summary>
    /// <param name="cleanupCode">an object to dispose when this lifetime ends</param>
    public void OnDisposed(IDisposable cleanupCode)
    {
        if (IsExpired == false)
        {
            _manager.OnDisposed(cleanupCode);
        }
    }

    /// <summary>
    /// Disposes if not already disposed
    /// </summary>
    /// <returns>true if this call disposed the object, false otherwise</returns>
    public bool TryDispose()
    {
        if (IsExpired || IsExpiring)
        {
            return false;
        }
        else
        {
            Dispose();
            return true;
        }
    }

    /// <summary>
    /// Creates a new lifetime that will end when any of the given
    /// lifetimes ends
    /// </summary>
    /// <param name="others">the lifetimes to use to generate this new lifetime</param>
    /// <returns>a new lifetime that will end when any of the given
    /// lifetimes ends</returns>
    public static Lifetime EarliestOf(params ILifetimeManager[] others)
    {
        return EarliestOf((IEnumerable<ILifetimeManager>)others);
    }

    /// <summary>
    /// Creates a new lifetime that will end when all of the given lifetimes end
    /// </summary>
    /// <param name="others">the lifetimes to use to generate this new lifetime</param>
    /// <returns>a new lifetime that will end when all of the given lifetimes end</returns>
    public static Lifetime WhenAll(params ILifetimeManager[] others) => new WhenAllTracker(others);

    /// <summary>
    /// Creates a new lifetime that will end when any of the given
    /// lifetimes ends
    /// </summary>
    /// <param name="others">the lifetimes to use to generate this new lifetime</param>
    /// <returns>a new lifetime that will end when any of the given
    /// lifetimes ends</returns>
    public static Lifetime EarliestOf(IEnumerable<ILifetimeManager> others) => new EarliestOfTracker(others.ToArray());





    /// <summary>
    /// Runs all the cleanup actions that have been registerd
    /// </summary>
    protected override void DisposeManagedResources()
    {
        if (!IsExpired)
        {
            var man = _manager;
            IsExpiring = true;
            _manager.IsExpiring = true;
            try
            {
                _manager.Finish();
                _manager.IsExpired = true;
                _manager = null;
            }
            finally
            {
                IsExpiring = false;
                man.IsExpiring = false;
            }
        }
    }

    private void SetResultTrue(object tcs) => ((TaskCompletionSource<bool>)tcs).SetResult(true);

    private class EarliestOfTracker : Lifetime
    {
        public EarliestOfTracker(ILifetimeManager[] lts)
        {
            if (lts.Length == 0)
            {
                Dispose();
                return;
            }

            foreach (var lt in lts)
            {
                lt?.OnDisposed(() => TryDispose());
            }
        }
    }

    private class WhenAllTracker : Lifetime
    {
        int remaining;
        public WhenAllTracker(ILifetimeManager[] lts)
        {
            if (lts.Length == 0)
            {
                Dispose();
                return;
            }
            remaining = lts.Length;
            foreach (var lt in lts)
            {
                lt.OnDisposed(Count);
            }
        }

        private void Count()
        {
            if (Interlocked.Decrement(ref remaining) == 0)
            {
                Dispose();
            }
        }
    }
}
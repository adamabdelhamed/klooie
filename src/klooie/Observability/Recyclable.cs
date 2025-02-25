using System.Runtime.CompilerServices;

namespace klooie;

public class Recyclable : ILifetime
{
    private static readonly Recyclable forever = new Recyclable();
    public static Recyclable Forever => forever;

    private List<Subscription>? disposalSubscribers;

    private List<Subscription> DisposalSubscribers => disposalSubscribers ?? (disposalSubscribers = SubscriptionListPool.Rent());

    internal IObjectPool? Pool { get; set; }

    public bool IsExpired { get; private set; }  

    public bool IsExpiring { get; private set; }

    public bool ShouldContinue => IsExpired == false && IsExpiring == false;

    public bool ShouldStop => !ShouldContinue;

    private TaskCompletionSource endedTaskCompletionSource;

    public Recyclable()
    {
        Rent();
    }

    public Task AsTask() => endedTaskCompletionSource?.Task ?? (endedTaskCompletionSource = new TaskCompletionSource()).Task;

    public bool TryDispose()
    {
        if (ShouldStop) return false;
        Dispose();
        return true;
    }

    public void Dispose()
    {
        if (IsExpiring || IsExpired) throw new InvalidOperationException("Cannot dispose an object that is already being disposed or has been disposed");
        IsExpiring = true;
        try
        {


            if(disposalSubscribers != null && disposalSubscribers.Count > 0)
            {
                NotificationBufferPool.Notify(disposalSubscribers);
                SubscriptionListPool.Return(disposalSubscribers);
                disposalSubscribers = null;
            }

            if (endedTaskCompletionSource != null)
            {
                endedTaskCompletionSource.SetResult();
                endedTaskCompletionSource = null;
            }

            if (Pool != null)
            {
                OnReturn();
                Pool.ReturnThatShouldOnlyBeCalledInternally(this);
            }

        }
        finally
        {
            IsExpired = true;
            IsExpiring = false;
        }
    }

    protected virtual void OnInit() { }
    protected virtual void OnReturn() { }
     
    internal void Rent()
    {
        IsExpiring = false;
        IsExpired = false;
        disposalSubscribers = null;
        endedTaskCompletionSource = null;
        Pool = null;
        OnInit();
    }

    public static Recyclable EarliestOf(params ILifetime[] others)
    {
        return EarliestOf((IEnumerable<ILifetime>)others);
    }

    /// <summary>
    /// Creates a new lifetime that will end when all of the given lifetimes end
    /// </summary>
    /// <param name="others">the lifetimes to use to generate this new lifetime</param>
    /// <returns>a new lifetime that will end when all of the given lifetimes end</returns>
    public static Recyclable WhenAll(params ILifetime[] others) => new WhenAllTracker(others);

    /// <summary>
    /// Creates a new lifetime that will end when any of the given
    /// lifetimes ends
    /// </summary>
    /// <param name="others">the lifetimes to use to generate this new lifetime</param>
    /// <returns>a new lifetime that will end when any of the given
    /// lifetimes ends</returns>
    public static Recyclable EarliestOf(IEnumerable<ILifetime> others) => new EarliestOfTracker(others.ToArray());

    /// <summary>
    /// Creates a new lifetime that will dispose when the parent disposes
    /// </summary>
    /// <param name="lt">the parent</param>
    /// <returns>the new lifetime</returns>
    public Recyclable CreateChildRecyclable()
    {
        var ret = DefaultRecyclablePool.Instance.Rent();
        OnDisposed(ret, TryDisposeChild);
        return ret;
    }

    private static void TryDisposeChild(object rec) => ((Recyclable)rec).TryDispose();

    public void OnDisposed(Action cleanupCode)
    {
        var subscription = SubscriptionPool.Instance.Rent();
        subscription.Callback = cleanupCode;
        DisposalSubscribers.Add(subscription);
    }
    

    public void OnDisposed(object scope, Action<object> cleanupCode)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        var subscription = SubscriptionPool.Instance.Rent();
        subscription.Scope = scope;
        subscription.ScopedCallback = cleanupCode;
        DisposalSubscribers.Add(subscription);
    }

    public void OnDisposed(Recyclable obj)
    {
        var subscription = SubscriptionPool.Instance.Rent();
        subscription.ToAlsoDispose = obj;
        subscription.Scope = obj;
        subscription.ScopedCallback = Event.DisposeStatic;
        DisposalSubscribers.Add(subscription);
    }

    private class EarliestOfTracker : Recyclable
    {
        public EarliestOfTracker(ILifetime[] lts)
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

    private class WhenAllTracker : Recyclable
    {
        int remaining;
        public WhenAllTracker(ILifetime[] lts)
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

public interface IObjectPool
{
    void Clear();
    IObjectPool Fill(int? count = null);
    public void ReturnThatShouldOnlyBeCalledInternally(Recyclable rented);
}

public abstract class RecycleablePool<T> : IObjectPool where T : Recyclable
{
#if DEBUG
    public int Created { get; private set; }
    public int Rented { get; private set; }
    public int Returned { get; private set; }
    public int AllocationsSaved => Rented - Created;

#endif
    private readonly Stack<T> _pool = new Stack<T>();

    public abstract T Factory();

    public int DefaultFillSize { get; set; } = 10;

    protected RecycleablePool() { }
    public T Rent()
    {
#if DEBUG
        Rented++;
#endif
        if (_pool.Count > 0)
        {
            var ret = _pool.Pop();
            ret.Rent();
            ret.Pool = this;
            return ret;
        }

#if DEBUG
        Created++;
#endif

        var newInstance = Factory();
        newInstance.Pool = this;
        return newInstance;
    }

    public void ReturnThatShouldOnlyBeCalledInternally(Recyclable rented)
    {
#if DEBUG
        Returned++;
#endif
        if(rented.Pool != this) throw new InvalidOperationException("Object returned to wrong pool");
        rented.Pool = null;
        _pool.Push((T)rented);
    }
 
    public void Clear()
    {
        _pool.Clear();
    }

    public IObjectPool Fill(int? count = null)
    {
        count = count ?? DefaultFillSize;
        for (var i = 0; i < count.Value; i++)
        {
            _pool.Push(Factory());
        }
        return this;
    }
}
public class GenericReferenceEqualityComparer<T> : IEqualityComparer<T>
{
    public static GenericReferenceEqualityComparer<T> Instance { get; } = new GenericReferenceEqualityComparer<T>();

    private GenericReferenceEqualityComparer() { }

    public bool Equals(T x, T y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
 
public static class TaskExtensions
{
    /// <summary>
    /// Converts a task to a lifetime
    /// </summary>
    /// <param name="t">the tast</param>
    /// <returns>a lifetime</returns>
    public static ILifetime ToLifetime(this Task t, EventLoop loop = null)
    {
        loop = loop ?? ConsoleApp.Current;
        if (loop == null) throw new ArgumentException("ToLifetime() requires an event loop");

        var lt = DefaultRecyclablePool.Instance.Rent();
        loop.Invoke(async () =>
        {
            await t;
            lt.Dispose();
        });
        return lt;
    }
}

 
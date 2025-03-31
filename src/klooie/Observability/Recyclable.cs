using System.Runtime.CompilerServices;
namespace klooie;

public class Recyclable : ILifetime
{
    private static readonly Recyclable forever = new Recyclable();
    public static Recyclable Forever => forever;

    private List<Subscription>? disposalSubscribers;
    private List<Subscription> DisposalSubscribers => disposalSubscribers ?? (disposalSubscribers = SubscriptionListPool.Rent());

    internal IObjectPool? Pool { get; set; }

    private bool IsExpiring { get;  set; }
    private bool IsExpired { get; set; }


    public int CurrentVersion { get; private set; }

    public int Lease => CurrentVersion;

    public bool IsStillValid(int leaseVersion)
    {
        return !IsExpired && !IsExpiring && leaseVersion == CurrentVersion;
    }

    private TaskCompletionSource? endedTaskCompletionSource;

    public Recyclable()
    {
        Rent();
    }

    public Task AsTask() => endedTaskCompletionSource?.Task ?? (endedTaskCompletionSource = new TaskCompletionSource()).Task;

    public bool TryDispose()
    {
        if (IsExpired || IsExpiring) return false;
        Dispose();
        return true;
    }

    public void Dispose()
    {
        if (IsExpiring || IsExpired) throw new InvalidOperationException("Cannot dispose an object that is already being disposed or has been disposed");
        IsExpiring = true;
        try
        {
            if (disposalSubscribers != null && disposalSubscribers.Count > 0)
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
        CurrentVersion++;
        IsExpiring = false;
        IsExpired = false;
        disposalSubscribers = null;
        endedTaskCompletionSource = null;
        OnInit();
    }

    public static Recyclable EarliestOf(params ILifetime[] others) => EarliestOf((IEnumerable<ILifetime>)others);
    public static Recyclable WhenAll(params ILifetime[] others) => new WhenAllTracker(others);
    public static Recyclable EarliestOf(IEnumerable<ILifetime> others) => new EarliestOfTracker(others.ToArray());

    public Recyclable CreateChildRecyclable(out int lease)
    {
        var ret = DefaultRecyclablePool.Instance.Rent(out lease);
        OnDisposed(ret, TryDisposeChild);
        return ret;
    }

    public Recyclable CreateChildRecyclable()
    {
        return CreateChildRecyclable(out _);
    }

    private static void TryDisposeChild(object rec) => ((Recyclable)rec).TryDispose();

    public void OnDisposed(Action cleanupCode)
    {
        var subscription = SubscriptionPool.Instance.Rent(out int _);
        subscription.Callback = cleanupCode;
        DisposalSubscribers.Add(subscription);
    }

    public void OnDisposed(object scope, Action<object> cleanupCode)
    {
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        var subscription = SubscriptionPool.Instance.Rent(out int _);
        subscription.Scope = scope;
        subscription.ScopedCallback = cleanupCode;
        DisposalSubscribers.Add(subscription);
    }

    public void OnDisposed(Recyclable obj)
    {
        var subscription = SubscriptionPool.Instance.Rent(out int _);
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
#if DEBUG
    int Created { get; }
    int Rented { get; }
    int Returned { get; }
    int AllocationsSaved => Rented - Created;
#endif
    void Clear();
    IObjectPool Fill(int? count = null);
    void ReturnThatShouldOnlyBeCalledInternally(Recyclable rented);
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

    protected RecycleablePool()
    {
        PoolManager.Instance.Add(this);
    }

    public override string ToString()
    {
        var typeName = GetFriendlyName(typeof(T));
#if DEBUG
        return $"{typeName}: Pending Return: {Rented - Returned} Created: {Created} Rented: {Rented} Returned: {Returned} AllocationsSaved: {AllocationsSaved}";
#endif
        return $"{typeName}";
    }

    public static string GetFriendlyName(Type type)
    {
        if (type.IsGenericType)
        {
            var baseName = type.Name.Substring(0, type.Name.IndexOf('`'));
            var genericArgs = type.GetGenericArguments();
            var formattedArgs = string.Join(", ", Array.ConvertAll(genericArgs, GetFriendlyName));
            return $"{baseName}<{formattedArgs}>";
        }
        return type.Name;
    }

    public T Rent(out int lease)
    {
#if DEBUG
        Rented++;
#endif
        T ret;
        if (_pool.Count > 0)
        {
            ret = _pool.Pop();
        }
        else
        {
#if DEBUG
            Created++;
#endif
            ret = Factory();
        }
        ret.Pool = this;
        ret.Rent();
        lease = ret.CurrentVersion;
        return ret;
    }

    public T Rent()
    {
        return Rent(out _);
    }

    public void ReturnThatShouldOnlyBeCalledInternally(Recyclable rented)
    {
#if DEBUG
        Returned++;
#endif
        if (rented.Pool != this) throw new InvalidOperationException("Object returned to wrong pool");
        rented.Pool = null;
        _pool.Push((T)rented);
    }

    public void Clear()
    {
        _pool.Clear();
    }

    public IObjectPool Fill(int? count = null)
    {
        count ??= DefaultFillSize;
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
    public static ILifetime ToLifetime(this Task t, EventLoop loop = null)
    {
        loop ??= ConsoleApp.Current;
        if (loop == null) throw new ArgumentException("ToLifetime() requires an event loop");
        var lt = DefaultRecyclablePool.Instance.Rent(out _);
        loop.Invoke(async () =>
        {
            await t;
            lt.Dispose();
        });
        return lt;
    }
}
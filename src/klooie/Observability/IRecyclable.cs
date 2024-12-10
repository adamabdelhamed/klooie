using System.Runtime.CompilerServices;

namespace klooie;
public interface IRecyclable : ILifetimeManager
{
    bool IsInUse { get; }
    void Reset();
    void Initialize();
}

public class Recyclable : IRecyclable, ILifetime
{
    private bool isResetting;
    private bool isInUse = true;
    private LifetimeManager? lifetimeManager;
    public bool IsInUse => isInUse;
    public bool IsExpired => isInUse == false;
    public bool IsExpiring => isResetting;
    public bool ShouldContinue => IsExpired == false && IsExpiring == false;
    public bool ShouldStop => !ShouldContinue;


    public bool TryDispose()
    {
        if (ShouldContinue)
        {
            Dispose();
            return true;
        }
        return false;
    }

    public void Dispose() => Return();


    protected virtual void Return()
    {
        RecycleablePool<Recyclable>.Instance.Return(this);
    }
    protected virtual void ProtectedInit() { }

    public void OnDisposed(Action action)
    {
        lifetimeManager = lifetimeManager ?? new LifetimeManager();
        lifetimeManager.OnDisposed(action);
    }

    public void OnDisposed(IDisposable obj)
    {
        lifetimeManager = lifetimeManager ?? new LifetimeManager();
        lifetimeManager.OnDisposed(obj);
    }

    public void OnDisposed(Subscription obj)
    {
        lifetimeManager = lifetimeManager ?? new LifetimeManager();
        lifetimeManager.OnDisposed(obj);
    }

    public void Initialize()
    {
        isInUse = true;
        ProtectedInit();
    }

    public void Reset()
    {
        isResetting = true;
        try
        {
            lifetimeManager?.Finish();
        }
        finally
        {
            isResetting = false;
            isInUse = false;
        }
    }
}
public class RecycleablePool<T> where T : IRecyclable
{
    private static RecycleablePool<T> _instance;
    public static RecycleablePool<T> Instance => _instance ??= new RecycleablePool<T>();

#if DEBUG
    public int Created { get; private set; }
    public int Rented { get; private set; }
    public int Returned { get; private set; }
    public int AllocationsSaved => Rented - Created;

#endif
    private readonly HashSet<T> _pool = new HashSet<T>(GenericReferenceEqualityComparer<T>.Instance);

    public Func<T> Factory { get; set; } = () => Activator.CreateInstance<T>();

    private RecycleablePool() { }
    public T Rent()
    {
#if DEBUG
        Rented++;
#endif
        if (_pool.Count > 0)
        {
            var first = _pool.First();
            _pool.Remove(first);
            first.Initialize();
            return first;
        }

#if DEBUG
        Created++;
#endif

        var ret = Factory();
        ret.Initialize();
        return ret;
    }

    public void Return(T rented)
    {
#if DEBUG
        Returned++;
#endif
        rented.Reset();
        _pool.Add(rented);
    }

    public void Use(Action<T> action)
    {
        var recyclable = Rent();
        try
        {
            action(recyclable);
        }
        finally
        {
            Return(recyclable);
        }
    }

    public void Use(T recyclable, Action<T> action)
    {
        try
        {
            action(recyclable);
        }
        finally
        {
            Return(recyclable);
        }
    }

    public async Task Use(Func<T,Task> action)
    {
        var recyclable = Rent();
        try
        {
            await action(recyclable);
        }
        finally
        {
            Return(recyclable);
        }
    }

    public async Task Use(T recyclable, Func<T,Task> action)
    {
        try
        {
            await action(recyclable);
        }
        finally
        {
            Return(recyclable);
        }
    }

    public async Task<T2> Use<T2>(Func<T, Task<T2>> action)
    {
        var recyclable = Rent();
        try
        {
            return await action(recyclable);
        }
        finally
        {
            Return(recyclable);
        }
    }

    public async Task<T2> Use<T2>(T recyclable, Func<T, Task<T2>> action)
    {
        try
        {
            return await action(recyclable);
        }
        finally
        {
            Return(recyclable);
        }
    }

    public void Clear()
    {
        _pool.Clear();
    }
}
public class GenericReferenceEqualityComparer<T> : IEqualityComparer<T>
{
    public static GenericReferenceEqualityComparer<T> Instance { get; } = new GenericReferenceEqualityComparer<T>();

    private GenericReferenceEqualityComparer() { }

    public bool Equals(T x, T y) => ReferenceEquals(x, y);

    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}
public static class IRecyclableEx
{
    /// <summary>
    /// Creates a new lifetime that will dispose when the parent disposes
    /// </summary>
    /// <param name="lt">the parent</param>
    /// <returns>the new lifetime</returns>
    public static Recyclable CreateChildRecyclable(this IRecyclable lt)
    {
        var ret = RecycleablePool<Recyclable>.Instance.Rent();
        lt.OnDisposed(() =>
        {
            if (ret.IsExpired == false)
            {
                RecycleablePool<Recyclable>.Instance.Return(ret);
            }
        });
        return ret;
    }

}

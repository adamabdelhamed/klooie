﻿using System.Runtime.CompilerServices;

namespace klooie;
public interface IRecyclable : ILifetimeManager, ILifetime
{
    /// <summary>
    /// Initializes the object for use. Implementing classes should call this from their constructor. It will also
    /// be called by RecycleablePool when the object is recycled.
    /// </summary>
    void Initialize();
}

public class Recyclable : IRecyclable, ILifetime
{
    private bool isInUse;
    private LifetimeManager? lifetimeManager;
    public bool IsExpired => isInUse == false;
    public bool IsExpiring => isInUse && lifetimeManager?.IsExpiring == true;
    public bool ShouldContinue => IsExpired == false && IsExpiring == false;
    public bool ShouldStop => !ShouldContinue;

    public Recyclable()
    {
        Initialize();
    }

    public bool TryDispose()
    {
        if (ShouldContinue)
        {
            Dispose();
            return true;
        }
        return false;
    }

    public void Dispose()
    {
        lifetimeManager?.Finish();
        if (lifetimeManager != null)
        {
            LifetimeManagerPool.Return(lifetimeManager);
        }
        lifetimeManager = null;
        isInUse = false;
    }

    protected virtual void ProtectedInit() { }

    public void OnDisposed(Action action)
    {
        lifetimeManager = lifetimeManager ?? LifetimeManagerPool.Rent();
        lifetimeManager.OnDisposed(action);
    }

    public void OnDisposed(object scope, Action<object> action)
    {
        lifetimeManager = lifetimeManager ?? LifetimeManagerPool.Rent();
        lifetimeManager.OnDisposed(scope, action);
    }

    public void OnDisposed(IDisposable obj)
    {
        lifetimeManager = lifetimeManager ?? LifetimeManagerPool.Rent();
        lifetimeManager.OnDisposed(obj);
    }

    public void OnDisposed(Subscription obj)
    {
        lifetimeManager = lifetimeManager ?? LifetimeManagerPool.Rent();
        lifetimeManager.OnDisposed(obj);
    }

    public void Initialize()
    {
        isInUse = true;
        ProtectedInit();
    }

 
}
public abstract class RecycleablePool<T> where T : IRecyclable
{
#if DEBUG
    public int Created { get; private set; }
    public int Rented { get; private set; }
    public int Returned { get; private set; }
    public int AllocationsSaved => Rented - Created;

#endif
    private readonly Stack<T> _pool = new Stack<T>();

    public abstract T Factory();

    protected RecycleablePool() { }
    public T Rent()
    {
#if DEBUG
        Rented++;
#endif
        if (_pool.Count > 0)
        {
            var ret = _pool.Pop();
            ret.Initialize();
            return ret;
        }

#if DEBUG
        Created++;
#endif

        return Factory();
    }

    public void Return(T rented)
    {
#if DEBUG
        Returned++;
#endif
        rented.TryDispose();
        _pool.Push(rented);
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
        var ret = DefaultRecyclablePool.Instance.Rent();
        lt.OnDisposed(() =>
        {
            if (ret.ShouldContinue)
            {
                DefaultRecyclablePool.Instance.Return(ret);
            }
        });
        return ret;
    }

}

 
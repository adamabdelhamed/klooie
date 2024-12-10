using System.Collections.Concurrent;

namespace klooie.Observability;
public abstract class Pool<T> where T : IRecyclable 
{
    protected Pool() { }

    protected abstract T Factory();


#if DEBUG
    public int Created { get; private set; }
    public int Rented { get; private set; }
    public int Returned { get; private set; }
    public int AllocationsSaved => Rented - Created;

#endif
    private readonly ConcurrentBag<T> _pool = new ConcurrentBag<T>();

    public T Rent()
    {
#if DEBUG
        Rented++;
#endif
        if (_pool.TryTake(out var t))
        {
            t.Initialize();
            return t;
        }

#if DEBUG
        Created++;
#endif

        return Factory();
    }

    public void Return(T t)
    {
#if DEBUG
        Returned++;
#endif
        _pool.Add(t);
    }
}
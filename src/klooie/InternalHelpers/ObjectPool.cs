using System.Collections.Concurrent;

namespace klooie;
public class SingleThreadObjectPool<T> where T : new()
{
#if DEBUG
    public static int EventsCreated { get; private set; }
    public static int EventsRented { get; private set; }
    public static int EventsReturned { get; private set; }
    public static int AllocationsSaved => EventsRented - EventsCreated;

#endif
    private readonly List<T> _pool = new List<T>();

    public T Rent()
    {
#if DEBUG
        EventsRented++;
#endif
        if (_pool.Count  > 0)
        {
            var rented = _pool[_pool.Count - 1];
            _pool.RemoveAt(0);
            return rented;
        }

#if DEBUG
        EventsCreated++;
#endif

        return new T();
    }

    public void Return(T obj)
    {
#if DEBUG
        EventsReturned++;
#endif
        _pool.Add(obj);
    }
}

public class ConcurrentbjectPool<T> where T : new()
{
#if DEBUG
    public static int EventsCreated { get; private set; }
    public static int EventsRented { get; private set; }
    public static int EventsReturned { get; private set; }
    public static int AllocationsSaved => EventsRented - EventsCreated;

#endif
    private readonly ConcurrentBag<T> _pool = new ConcurrentBag<T>();

    public T Rent()
    {
#if DEBUG
        EventsRented++;
#endif
        if (_pool.TryTake(out var t))
        {
            return t;
        }

#if DEBUG
        EventsCreated++;
#endif

        return new T();
    }

    public void Return(T obj)
    {
#if DEBUG
        EventsReturned++;
#endif
        _pool.Add(obj);
    }
}
using System;
using System.Collections.Generic;
using System.Threading;

namespace klooie;

public interface IObjectPool
{
    int Created { get; }
    int Rented { get; }
    int Returned { get; }
    int Pending => Rented - Returned;
    int AllocationsSaved => Rented - Created;
#if DEBUG
    StackHunter StackHunter { get; }
    HashSet<PendingRecyclableTracker> PendingReturns { get; }
#endif
    void Clear();
    IObjectPool Fill(int? count = null);
    void ReturnThatShouldOnlyBeCalledInternally(Recyclable rented);
}

public abstract class RecycleablePool<T> : IObjectPool where T : Recyclable
{
    // Atomic, overflow-safe counters
    private long _created;
    private long _rented;
    private long _returned;

    // Expose int metrics via clamped reads to avoid negative wrap
    public int Created => ClampToInt(Interlocked.Read(ref _created));
    public int Rented => ClampToInt(Interlocked.Read(ref _rented));
    public int Returned => ClampToInt(Interlocked.Read(ref _returned));
    int Pending => Rented - Returned;
    public int AllocationsSaved => Rented - Created;

#if DEBUG
    public HashSet<PendingRecyclableTracker> PendingReturns { get; } = new HashSet<PendingRecyclableTracker>();
    public StackHunter StackHunter { get; private set; } = new StackHunter() { Mode = Recyclable.StackHunterMode };
#endif

    // Per-instance, per-thread stacks (do NOT share across pool instances)
    private readonly ThreadLocal<Stack<T>> _tlsStacks =
        new(() => new Stack<T>(), trackAllValues: true);

    private Stack<T> StackForCurrentThread => _tlsStacks.Value!;

    public abstract T Factory();
    public int DefaultFillSize { get; set; } = 10;

    protected RecycleablePool()
    {
        PoolManager.Instance.Add(this);
    }

    public override string ToString()
    {
        var typeName = GetFriendlyName(typeof(T));
        var rented = Interlocked.Read(ref _rented);
        var returned = Interlocked.Read(ref _returned);
        var created = Interlocked.Read(ref _created);
        var pending = rented - returned;
        var saved = rented - created;
        return $"{typeName}: Pending Return: {pending} Created: {created} Rented: {rented} Returned: {returned} AllocationsSaved: {saved}";
    }

    public static string GetFriendlyName(Type type)
    {
        // Special-case: If this is FuncPool<T>, just return T's friendly name
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(FuncPool<>))
        {
            return GetFriendlyName(type.GetGenericArguments()[0]);
        }

        if (type.IsGenericType)
        {
            var index = type.Name.IndexOf('`');
            if (index < 0) return type.FullName;
            var baseName = type.Name.Substring(0, index);
            var genericArgs = type.GetGenericArguments();
            var formattedArgs = string.Join(", ", Array.ConvertAll(genericArgs, GetFriendlyName));
            return $"{baseName}<{formattedArgs}>";
        }

        return type.Name;
    }

    public T Rent(out int lease)
    {
        if (Recyclable.PoolingEnabled == false)
        {
            var fresh = Factory() ?? throw new InvalidOperationException("Factory returned null, cannot rent from pool.");
            // Do NOT attach to the pool when pooling is disabled
            fresh.ThreadId = Thread.CurrentThread.ManagedThreadId;
            lease = fresh.CurrentVersion;
            return fresh;
        }

        Interlocked.Increment(ref _rented);

#if DEBUG
        ComparableStackTrace? trace = null;
        if (Recyclable.StackHunterMode == StackHunterMode.Full)
        {
            trace = StackHunter.RegisterCurrentStackTrace(2, 10);
        }
#endif

        T ret;
        var stack = StackForCurrentThread;
        if (stack.Count > 0)
        {
            ret = stack.Pop();
            ret.Rent();
        }
        else
        {
            Interlocked.Increment(ref _created);
            ret = Factory() ?? throw new InvalidOperationException("Factory returned null, cannot rent from pool.");
            ret.ThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        ret.Pool = this;
        lease = ret.CurrentVersion;

#if DEBUG
        if (trace != null && Recyclable.StackHunterMode == StackHunterMode.Full)
        {
            PendingReturns.Add(new PendingRecyclableTracker(ret, trace));
        }
#endif

        return ret;
    }

    public T Rent()
    {
        return Rent(out _);
    }

    public void ReturnThatShouldOnlyBeCalledInternally(Recyclable rented)
    {
        T rentedT = (T)rented ?? throw new ArgumentNullException(nameof(rented), "Cannot return a null object to the pool.");

#if DEBUG
        PendingReturns.Remove(new PendingRecyclableTracker(rentedT, null));
#endif

        if (rented.ThreadId != Thread.CurrentThread.ManagedThreadId)
            throw new InvalidOperationException("This pool was rented from a different thread");
        if (!ReferenceEquals(rentedT.Pool, this))
            throw new InvalidOperationException("Object returned to wrong pool");

        rentedT.Pool = null;

        var stack = StackForCurrentThread;
        stack.Push(rentedT);

        // Count the return only after validation & push
        Interlocked.Increment(ref _returned);
    }

    public void Clear()
    {
        // Optional: throw if pending > 0 to catch leaks; comment out if you prefer silent clearing
        // if ((Interlocked.Read(ref _rented) - Interlocked.Read(ref _returned)) > 0)
        //     throw new InvalidOperationException("Cannot Clear() with pending rentals.");

        foreach (var s in _tlsStacks.Values)
            s.Clear();

        Interlocked.Exchange(ref _created, 0);
        Interlocked.Exchange(ref _rented, 0);
        Interlocked.Exchange(ref _returned, 0);

#if DEBUG
        StackHunter = new StackHunter() { Mode = Recyclable.StackHunterMode };
        PendingReturns.Clear();
#endif
    }

    public IObjectPool Fill(int? count = null)
    {
        count ??= DefaultFillSize;
        var stack = StackForCurrentThread;
        for (var i = 0; i < count.Value; i++)
        {
            var fresh = Factory() ?? throw new InvalidOperationException("Factory returned null, cannot fill pool.");
            // Note: we do not increment _created here to keep Created == number of on-demand creations
            stack.Push(fresh);
        }
        return this;
    }

    private static int ClampToInt(long value)
    {
        if (value <= int.MinValue) return int.MinValue;
        if (value >= int.MaxValue) return int.MaxValue;
        return (int)value;
    }
}

public class PendingRecyclableTracker
{
    public Recyclable Rented { get; init; }

    public ComparableStackTrace RenterStackTrace { get; init; }

    public PendingRecyclableTracker(Recyclable rented, ComparableStackTrace renterStackTrace)
    {
        Rented = rented;
        RenterStackTrace = renterStackTrace;
    }

    public override bool Equals(object? obj)
    {
        return obj is PendingRecyclableTracker other && ReferenceEquals(Rented, other.Rented);
    }

    public override int GetHashCode()
    {
        return Rented.GetHashCode();
    }
}

public sealed class FuncPool<T> : RecycleablePool<T> where T : Recyclable
{
    private readonly Func<T> factory;
    internal FuncPool(Func<T> factory) => this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
    public override T Factory() => factory();
}

public sealed class LazyPool<T> : Lazy<FuncPool<T>> where T : Recyclable
{
    public LazyPool(Func<T> factory) : base(() => new FuncPool<T>(factory)) { }
}

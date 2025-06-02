namespace klooie;
public interface IObjectPool
{
#if DEBUG
    int Created { get; }
    int Rented { get; }
    int Returned { get; }
    int AllocationsSaved => Rented - Created;
    StackHunter StackHunter { get; }
    HashSet<PendingRecyclableTracker> PendingReturns { get; }
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

    public HashSet<PendingRecyclableTracker> PendingReturns { get; } = new HashSet<PendingRecyclableTracker>();
    public StackHunter StackHunter { get; private set; } = new StackHunter() { Mode = Recyclable.StackHunterMode };
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
        var trace =  StackHunter.RegisterCurrentStackTrace(2, 10);
#endif
        T ret;
        if (_pool.Count > 0)
        {
            ret = _pool.Pop();
            ret.Rent();
        }
        else
        {
#if DEBUG
            Created++;
#endif
            ret = Factory();
        }
        ret.Pool = this;
        lease = ret.CurrentVersion;

#if DEBUG
        if (trace != null) PendingReturns.Add(new PendingRecyclableTracker(ret, trace));
#endif

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
        PendingReturns.Remove(new PendingRecyclableTracker((T)rented, null));
#endif
        if (rented.Pool != this) throw new InvalidOperationException("Object returned to wrong pool");
        rented.Pool = null;
        _pool.Push((T)rented);
    }

    public void Clear()
    {
        _pool.Clear();
#if DEBUG
        Created = 0;
        Rented = 0;
        Returned = 0;
        StackHunter = new StackHunter();
#endif
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

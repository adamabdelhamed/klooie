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

    public int Created { get; private set; }
    public int Rented { get; private set; }
    public int Returned { get; private set; }
    int Pending => Rented - Returned;
    public int AllocationsSaved => Rented - Created;
#if DEBUG
    public HashSet<PendingRecyclableTracker> PendingReturns { get; } = new HashSet<PendingRecyclableTracker>();
    public StackHunter StackHunter { get; private set; } = new StackHunter() { Mode = Recyclable.StackHunterMode };
#endif
    private readonly Stack<T> _pool = new Stack<T>();
    public abstract T Factory();
    public int DefaultFillSize { get; set; } = 10;


    private void EnsureThreadAffinity()
    {
        if(ConsoleApp.Current == null)
        {
            //throw new InvalidOperationException("RecycleablePool must be used within a ConsoleApp context. Ensure you are using it in the correct thread.");
        }
    }

    protected RecycleablePool()
    {
        EnsureThreadAffinity();
        PoolManager.Instance.Add(this);
    }

    public override string ToString()
    {
        var typeName = GetFriendlyName(typeof(T));
        return $"{typeName}: Pending Return: {Rented - Returned} Created: {Created} Rented: {Rented} Returned: {Returned} AllocationsSaved: {AllocationsSaved}";

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
        EnsureThreadAffinity();
        if (Recyclable.PoolingEnabled == false)
        {
            var fresh = Factory();
            if(fresh == null)
            {
                throw new InvalidOperationException("Factory returned null, cannot rent from pool.");
            }
            fresh.Pool = this;
            lease = fresh.CurrentVersion;
            return fresh;
        }


        Rented++;
#if DEBUG
        ComparableStackTrace? trace = null;
        if (Recyclable.StackHunterMode == StackHunterMode.Full)
        {
            trace = StackHunter.RegisterCurrentStackTrace(2, 10);
        }
#endif
        T ret;
        if (_pool.Count > 0)
        {
            ret = _pool.Pop();
            ret.Rent();
        }
        else
        {

            Created++;
            ret = Factory();
            if (ret == null)
            {
                throw new InvalidOperationException("Factory returned null, cannot rent from pool.");
            }
        }
        ret.Pool = this;
        lease = ret.CurrentVersion;

#if DEBUG
        if (trace != null && Recyclable.StackHunterMode == StackHunterMode.Full) PendingReturns.Add(new PendingRecyclableTracker(ret, trace));
#endif

        return ret;
    }

    public T Rent()
    {
        EnsureThreadAffinity();
        return Rent(out _);
    }

    public void ReturnThatShouldOnlyBeCalledInternally(Recyclable rented)
    {
        EnsureThreadAffinity();
        T rentedT = (T)rented;
        if (rentedT == null)
        {
            throw new ArgumentNullException(nameof(rented), "Cannot return a null object to the pool.");
        }
        Returned++;
#if DEBUG
        PendingReturns.Remove(new PendingRecyclableTracker(rentedT, null));
#endif

        if (rentedT.Pool != this) throw new InvalidOperationException("Object returned to wrong pool");
        rentedT.Pool = null;
        _pool.Push(rentedT);
    }

    public void Clear()
    {
        EnsureThreadAffinity();
        _pool.Clear();
        Created = 0;
        Rented = 0;
        Returned = 0;
#if DEBUG
        StackHunter = new StackHunter();
#endif
    }

    public IObjectPool Fill(int? count = null)
    {
        EnsureThreadAffinity();
        count ??= DefaultFillSize;
        for (var i = 0; i < count.Value; i++)
        {
            var fresh = Factory();
            if (fresh == null)
            {
                throw new InvalidOperationException("Factory returned null, cannot fill pool.");
            }
            _pool.Push(fresh);
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

public sealed class FuncPool<T> : RecycleablePool<T> where T : Recyclable
{
    private Func<T> factory;
    internal FuncPool(Func<T> factory) => this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
    public override T Factory() => factory();
}

public sealed class LazyPool<T> : Lazy<FuncPool<T>> where T : Recyclable
{
    public LazyPool(Func<T> factory) : base(() => new FuncPool<T>(factory)) { }
}

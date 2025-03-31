namespace klooie;
public class PoolManager
{
    public static Lazy<PoolManager> _instanceFactory = new Lazy<PoolManager>(()=> new PoolManager(), true);
    public static PoolManager Instance => _instanceFactory.Value;
    private PoolManager() { }

    private List<IObjectPool> pools { get; set; } = new List<IObjectPool>();

    public IReadOnlyList<IObjectPool> Pools => pools;

    public T Get<T>() where T : IObjectPool
    {
        var pool = pools.FirstOrDefault(p => p.GetType() == typeof(T));
        if (pool == null)
        {
            pool = (T)Activator.CreateInstance(typeof(T));
            pools.Add(pool);
        }
        return (T)pool;
    }

    public void ClearAll()
    {
        for(var i = 0; i < pools.Count; i++)
        {
            pools[i].Clear();
        }
    }

    public void FillAll(int? count = null)
    {
        for (var i = 0; i < pools.Count; i++)
        {
            pools[i].Fill(count);
        }
    }

    internal void Add<T>(RecycleablePool<T> recycleablePool) where T : Recyclable
    {
        if(pools.Where(p => p.GetType() == recycleablePool.GetType()).Count() == 0)
        {
            pools.Add(recycleablePool);
        }
        else
        {
            throw new InvalidOperationException("Pool already exists");
        }
    }
}

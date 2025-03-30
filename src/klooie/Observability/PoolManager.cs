namespace klooie;
public class PoolManager
{
    public static Lazy<PoolManager> _instanceFactory = new Lazy<PoolManager>(()=> new PoolManager(), true);
    public static PoolManager Instance => _instanceFactory.Value;
    private PoolManager() { }

    private List<IObjectPool> Pools { get; set; } = new List<IObjectPool>();

    public T Get<T>() where T : IObjectPool
    {
        var pool = Pools.FirstOrDefault(p => p.GetType() == typeof(T));
        if (pool == null)
        {
            pool = (T)Activator.CreateInstance(typeof(T));
            Pools.Add(pool);
        }
        return (T)pool;
    }

    public void ClearAll()
    {
        for(var i = 0; i < Pools.Count; i++)
        {
            Pools[i].Clear();
        }
    }

    public void FillAll(int? count = null)
    {
        for (var i = 0; i < Pools.Count; i++)
        {
            Pools[i].Fill(count);
        }
    }

    internal void Add<T>(RecycleablePool<T> recycleablePool) where T : Recyclable
    {
        if(Pools.Where(p => p.GetType() == recycleablePool.GetType()).Count() == 0)
        {
            Pools.Add(recycleablePool);
        }
        else
        {
            throw new InvalidOperationException("Pool already exists");
        }
    }
}

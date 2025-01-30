namespace klooie;
public class PoolManager
{
    public List<IObjectPool> Pools { get; private set; } = new List<IObjectPool>();

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
}

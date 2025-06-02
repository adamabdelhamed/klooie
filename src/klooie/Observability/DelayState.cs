namespace klooie;

public class DelayState : Recyclable
{
    internal Action<object> InnerAction;
    private RecyclableList<ILifetime> Dependencies;
    private RecyclableList<int> Leases;

    public ILifetime MainDependency => Dependencies.Items[0];

    public bool AreAllDependenciesValid
    {
        get
        {
            for (int i = 0; i < Leases.Count; i++)
            {
                if (Dependencies[i].IsStillValid(Leases[i]) == false)
                {
                    return false;
                }
            }
            return true;
        }
    }

    protected override void OnInit()
    {
        base.OnInit();
        Dependencies = RecyclableListPool<ILifetime>.Instance.Rent();
        Leases = RecyclableListPool<int>.Instance.Rent(1);
    }

    public static DelayState Create(ILifetime dependency)
    {
        var ret = DelayStatePool.Instance.Rent();
        ret.AddDependency(dependency);
        return ret;
    }

    public void AddDependency(ILifetime dependency)
    {
        if (Dependencies == null)
        {
            Dependencies = RecyclableListPool<ILifetime>.Instance.Rent();
            Leases = RecyclableListPool<int>.Instance.Rent();
        }
        Dependencies.Items.Add(dependency);
        Leases.Items.Add(dependency.Lease);
    }

    public static DelayState Create(RecyclableList<ILifetime> dependencies)
    {
        var ret = DelayStatePool.Instance.Rent();
        ret.Dependencies = dependencies;
        ret.Leases = RecyclableListPool<int>.Instance.Rent(dependencies.Count);
        for (int i = 0; i < dependencies.Count; i++)
        {
            ret.Leases.Items.Add(dependencies[i].Lease);
        }
        return ret;
    }

    internal void DisposeAllValidDependencies()
    {
        if (Dependencies == null) return;
        for (int i = 0; i < Dependencies.Count; i++)
        {
            if (Dependencies[i] == null || Dependencies[i].IsStillValid(Leases[i]) == false)
            {
                continue;
            }
            (Dependencies[i] as Recyclable)?.TryDispose();
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        Dependencies?.Dispose();
        Dependencies = null;
        Leases?.Dispose();
        Leases = null;
        InnerAction = null;
    }
}
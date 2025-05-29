namespace klooie;

public class DelayState : Recyclable
{
    internal Action<object> InnerAction;
    private RecyclableList<ILifetime> Dependencies;
    private RecyclableList<int> Leases;

    public ILifetime MainDependency => Dependencies.Items[0];

    public bool IsStillValid
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
        ret.Dependencies.Items.Add(dependency);
        ret.Leases.Items.Add(dependency.Lease);
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
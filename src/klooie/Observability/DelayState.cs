namespace klooie;

public class DelayState : Recyclable
{
    internal Action<object> InnerAction;
    protected RecyclableList<ILifetime> Dependencies;
    protected RecyclableList<int> Leases;

    private Event<Recyclable>? _beforeDisposeDependency;
    public Event<Recyclable> BeforeDisposeDependency => _beforeDisposeDependency ??= Event<Recyclable>.Create();
    public ILifetime MainDependency => Dependencies.Items[0];

    public bool AreAllDependenciesValid
    {
        get
        {
            if (Leases == null ||Dependencies == null || Leases.Count == 0 || Dependencies.Count == 0) return false;
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

    public ILifetime[] ValidDependencies =>
        Dependencies?.Items.Where((d, i) => d != null && d.IsStillValid(Leases[i])).ToArray() ?? Array.Empty<Recyclable>();

    protected override void OnInit()
    {
        base.OnInit();
        Dependencies = RecyclableListPool<ILifetime>.Instance.Rent();
        Leases = RecyclableListPool<int>.Instance.Rent();
    }

    public static DelayState Create(ILifetime dependency)
    {
        var ret = DelayStatePool.Instance.Rent();
        ret.AddDependency(dependency);
        return ret;
    }

    public void AddDependency(ILifetime dependency)
    {
        if(dependency == null) throw new ArgumentNullException(nameof(dependency), "Dependency cannot be null");
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
            if (Dependencies[i] is Recyclable r)
            { 
                _beforeDisposeDependency?.Fire(r);
                r.TryDispose(Leases[i], $"InnerLoopAPIs - {nameof(DisposeAllValidDependencies)}");
            }
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
        _beforeDisposeDependency?.TryDispose();
        _beforeDisposeDependency = null;
    }
}
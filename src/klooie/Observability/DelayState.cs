namespace klooie;

public class DelayState : Recyclable
{
    // Tracks the lease of each dependency so that validity checks and
    // disposals use the captured lease instead of the current one
    protected RecyclableList<LeaseState<Recyclable>> Dependencies;

    private Event<Recyclable>? _beforeDisposeDependency;
    public Event<Recyclable> BeforeDisposeDependency => _beforeDisposeDependency ??= Event<Recyclable>.Create();
    public Recyclable MainDependency => Dependencies.Count > 0 && Dependencies.Items[0].IsRecyclableValid ? Dependencies.Items[0].Recyclable : null;

    public int DependencyCount => Dependencies?.Count ?? 0;

    public bool AreAllDependenciesValid
    {
        get
        {
            if (Dependencies == null || Dependencies.Count == 0) return false;
            for (int i = 0; i < Dependencies.Count; i++)
            {
                if (Dependencies[i].IsRecyclableValid == false)
                {
                    return false;
                }
            }
            return true;
        }
    }

    public LeaseState<Recyclable> DependencyAt(int index) => Dependencies[index];

    public ILifetime[] ValidDependencies =>
        Dependencies?.Items
            .Where(d => d.IsRecyclableValid && d.Recyclable != null)
            .Select(d => (ILifetime)d.Recyclable!)
            .ToArray() ?? Array.Empty<Recyclable>();

    protected override void OnInit()
    {
        base.OnInit();
        Dependencies = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent(10);
    }

    internal static LazyPool<DelayState> pool = new LazyPool<DelayState>(() => new DelayState());
    protected DelayState() { }

    public static DelayState Create(ILifetime dependency)
    {
        var ret = pool.Value.Rent();
        ret.AddDependency(dependency);
        return ret;
    }

    public void AddDependency(ILifetime dependency)
    {
        if(dependency == null) throw new ArgumentNullException(nameof(dependency), "Dependency cannot be null");
        if (Dependencies == null)
        {
            Dependencies = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent();
        }
        Dependencies.Items.Add(LeaseHelper.Track((Recyclable)dependency));
    }

    public static DelayState Create(RecyclableList<ILifetime> dependencies)
    {
        var ret = pool.Value.Rent();
        ret.Dependencies = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent(dependencies.Count);
        for (int i = 0; i < dependencies.Count; i++)
        {
            ret.Dependencies.Items.Add(LeaseHelper.Track((Recyclable)dependencies[i]));
        }
        dependencies.Dispose();
        return ret;
    }

    internal void DisposeAllValidDependencies()
    {
        if (Dependencies == null) return;
        for (int i = 0; i < Dependencies.Count; i++)
        {
            var tracker = Dependencies[i];
            var dep = tracker.Recyclable;
            if (dep != null)
            {
                _beforeDisposeDependency?.Fire(dep);
                tracker.TryDisposeRecyclable();
            }
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        if (Dependencies != null)
        {
            for (int i = 0; i < Dependencies.Count; i++)
            {
                Dependencies[i]?.TryDispose();
            }
            Dependencies.Dispose();
            Dependencies = null;
        }
        _beforeDisposeDependency?.TryDispose();
        _beforeDisposeDependency = null;
    }
}
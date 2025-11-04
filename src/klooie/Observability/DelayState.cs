namespace klooie;

public class DelayState : Recyclable
{
    private static readonly LazyPool<DelayState> pool = new(() => new DelayState());
    internal static LazyPool<DelayState> Pool => pool;

    // Fast path: single dependency
    private LeaseState<Recyclable>? singleDependency;

    // Slow path: multiple dependencies
    private RecyclableList<LeaseState<Recyclable>>? multipleDependencies;

    private Event<Recyclable>? _beforeDisposeDependency;
    public Event<Recyclable> BeforeDisposeDependency => _beforeDisposeDependency ??= Event<Recyclable>.Create();

    public Recyclable? MainDependency
    {
        get
        {
            if (singleDependency != null && singleDependency.IsRecyclableValid) return singleDependency.Recyclable;
            if (multipleDependencies != null && multipleDependencies.Count > 0 && multipleDependencies.Items[0].IsRecyclableValid)
                return multipleDependencies.Items[0].Recyclable;
            return null;
        }
    }

    public int DependencyCount
    {
        get
        {
            if (multipleDependencies != null) return multipleDependencies.Count;
            return singleDependency != null ? 1 : 0;
        }
    }

    public bool AreAllDependenciesValid
    {
        get
        {
            if (multipleDependencies != null)
            {
                for (int i = 0; i < multipleDependencies.Count; i++)
                {
                    if (!multipleDependencies[i].IsRecyclableValid) return false;
                }
                return multipleDependencies.Count > 0;
            }
            return singleDependency != null && singleDependency.IsRecyclableValid;
        }
    }

    public LeaseState<Recyclable> DependencyAt(int index)
    {
        if (multipleDependencies != null) return multipleDependencies[index];
        if (index == 0 && singleDependency != null) return singleDependency;
        throw new IndexOutOfRangeException();
    }

    public ILifetime[] ValidDependencies
    {
        get
        {
            if (multipleDependencies != null)
            {
                return multipleDependencies.Items
                    .Where(d => d.IsRecyclableValid && d.Recyclable != null)
                    .Select(d => (ILifetime)d.Recyclable!)
                    .ToArray();
            }
            if (singleDependency != null && singleDependency.IsRecyclableValid && singleDependency.Recyclable != null)
            {
                return new ILifetime[] { singleDependency.Recyclable };
            }
            return Array.Empty<ILifetime>();
        }
    }

    protected override void OnInit()
    {
        base.OnInit();
        singleDependency = null;
        multipleDependencies = null;
    }

    protected DelayState() { }

    public static DelayState Create(ILifetime dependency)
    {
        var ret = pool.Value.Rent();
        ret.AddDependency(dependency);
        return ret;
    }

    public static DelayState Create(RecyclableList<ILifetime> dependencies)
    {
        var ret = pool.Value.Rent();
        if (dependencies.Count == 1)
        {
            ret.singleDependency = LeaseHelper.Track((Recyclable)dependencies[0]);
        }
        else
        {
            ret.multipleDependencies = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent(dependencies.Count);
            for (int i = 0; i < dependencies.Count; i++)
            {
                ret.multipleDependencies.Items.Add(LeaseHelper.Track((Recyclable)dependencies[i]));
            }
        }
        dependencies.Dispose();
        return ret;
    }

    public void AddDependency(ILifetime dependency)
    {
        if (dependency == null)
            throw new ArgumentNullException(nameof(dependency));

        var newLease = LeaseHelper.Track((Recyclable)dependency);

        if (multipleDependencies != null)
        {
            multipleDependencies.Items.Add(newLease);
            return;
        }

        if (singleDependency == null)
        {
            singleDependency = newLease;
            return;
        }

        // Escalate to list
        multipleDependencies = RecyclableListPool<LeaseState<Recyclable>>.Instance.Rent(4);
        multipleDependencies.Items.Add(singleDependency);
        multipleDependencies.Items.Add(newLease);
        singleDependency = null;
    }

    internal void DisposeAllValidDependencies()
    {
        if (multipleDependencies != null)
        {
            for (int i = 0; i < multipleDependencies.Count; i++)
            {
                var tracker = multipleDependencies[i];
                if (tracker.Recyclable != null)
                {
                    _beforeDisposeDependency?.Fire(tracker.Recyclable);
                    tracker.TryDisposeRecyclable();
                }
            }
            return;
        }

        if (singleDependency != null && singleDependency.Recyclable != null)
        {
            _beforeDisposeDependency?.Fire(singleDependency.Recyclable);
            singleDependency.TryDisposeRecyclable();
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();

        if (multipleDependencies != null)
        {
            for (int i = 0; i < multipleDependencies.Count; i++)
            {
                multipleDependencies[i]?.TryDispose();
            }
            multipleDependencies.Dispose();
            multipleDependencies = null;
        }

        if (singleDependency != null)
        {
            singleDependency.TryDispose();
            singleDependency = null;
        }

        _beforeDisposeDependency?.TryDispose();
        _beforeDisposeDependency = null;
    }
}

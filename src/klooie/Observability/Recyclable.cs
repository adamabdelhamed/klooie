using System.Runtime.CompilerServices;
namespace klooie;

public class Recyclable : ILifetime
{
    public string DisposalReason { get; set; } 
    public static bool PoolingEnabled { get; set; } = true;
    public static StackHunterMode StackHunterMode { get; set; } = StackHunterMode.Off;

    private SubscriberCollection? disposalSubscribers;
    private SubscriberCollection DisposalSubscribers
    {
        get
        {
            var ret = disposalSubscribers ??= SubscriberCollection.Create();
            if(ret.ThreadId != this.ThreadId)
            {
                throw new InvalidOperationException("Cannot access disposal subscribers from a different thread" + GetType().Name);
            }
            return ret;
        }
    }

    internal IObjectPool? Pool { get; set; }
    public int ThreadId { get; internal set; }
    private bool IsExpiring { get; set; }
    private bool IsExpired { get; set; }

    internal string RecyclableState => $"Version: {Lease}, IsExpiring: {IsExpiring}, IsExpired: {IsExpired}";
    public int CurrentVersion { get; private set; }

    public int Lease => CurrentVersion;

    public bool IsStillValid(int leaseVersion)
    {
        return !IsExpired && !IsExpiring && leaseVersion == CurrentVersion;
    }


    public Recyclable()
    {
        ThreadId = Thread.CurrentThread.ManagedThreadId;
        Rent();
    }


    [Obsolete("Adds no value, bypasses lease and reason checks")]
    public static void TryDisposeMe(Recyclable me) => me.TryDispose("Recyclable.TryDisposeMe");


    [Obsolete("This method is obsolete because it does not require the caller to provide a lease, which can result in one component silently disposing another component's Recyclable.")]
    public bool TryDispose(string reason) => TryDispose(Lease, reason);

    [Obsolete]
    public void Dispose(string reason) => Dispose(Lease, reason);
    public bool TryDispose(int lease, string reason)
    {
        if (Lease != lease || IsExpired || IsExpiring) return false;
        Dispose(lease, reason);
        return true;
    }

    public void Dispose(int lease, string reason)
    {
        if(string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A reason must be provided when disposing with the lease parameter", nameof(reason));
        if (lease != Lease) throw new ArgumentException($"Cannot dispose with an invalid lease. Current lease: {Lease}, provided lease: {lease}");
        if (IsExpiring || IsExpired) throw new InvalidOperationException("Cannot dispose an object that is already being disposed or has been disposed: " + GetType().Name);
        DisposalReason = reason;
        IsExpiring = true;
        try
        {
            OnReturn();

            if (disposalSubscribers?.Count > 0)
            {
                disposalSubscribers.Notify();
                disposalSubscribers?.Dispose();
                disposalSubscribers = null;
            }

            if (Pool != null)
            {
                Pool.ReturnThatShouldOnlyBeCalledInternally(this);
            }
        }
        finally
        {
            IsExpired = true;
            IsExpiring = false;
        }
    }

    protected virtual void OnInit() 
    {

    }
    protected virtual void OnReturn() { }
    internal void Rent()
    {
        CurrentVersion++;
        IsExpiring = false;
        IsExpired = false;
        OnInit();
    }

    public static Recyclable EarliestOf(params ILifetime[] others) => EarliestOf((IEnumerable<ILifetime>)others);
    public static Recyclable WhenAll(params ILifetime[] others) => new WhenAllTracker(others);
    public static Recyclable EarliestOf(IEnumerable<ILifetime> others) => new EarliestOfTracker(others.ToArray());

    public Recyclable CreateChildRecyclable(out int lease)
    {
        var ret = DefaultRecyclablePool.Instance.Rent(out lease);
        var tracker = LeaseHelper.TrackOwnerRelationship(this, ret);
        OnDisposed(tracker, static (tracker) =>
        {
            tracker.TryDisposeRecyclable();
            tracker.Dispose("external/klooie/src/klooie/Observability/Recyclable.cs:120");
        });
        return ret;
    }

    public Recyclable CreateChildRecyclable()
    {
        return CreateChildRecyclable(out _);
    }


    public void OnDisposed(Action cleanupCode)
    {
        if(IsExpired || IsExpiring) throw new InvalidOperationException("Cannot add a disposal callback to an object that is already being disposed or has been disposed" + GetType().Name);
        var subscription = ActionSubscription.Create(cleanupCode);
        DisposalSubscribers.Subscribe(subscription);
    }

    public void OnDisposed<T>(T scope, Action<T> cleanupCode)
    {
        if (IsExpired || IsExpiring) throw new InvalidOperationException("Cannot add a disposal callback to an object that is already being disposed or has been disposed" + GetType().Name);
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        var subscription = ScopedSubscription<T>.Create(scope, cleanupCode);
        DisposalSubscribers.Subscribe(subscription);
    }

    private class EarliestOfTracker : Recyclable
    {
        public EarliestOfTracker(ILifetime[] lts)
        {
            if (lts.Length == 0)
            {
                Dispose("external/klooie/src/klooie/Observability/Recyclable.cs:152");
                return;
            }
            foreach (var lt in lts)
            {
                lt?.OnDisposed(this, DisposeMe);
            }
        }

        private static void DisposeMe(object me) => ((EarliestOfTracker)me).TryDispose("external/klooie/src/klooie/Observability/Recyclable.cs:164");
    }

    private class WhenAllTracker : Recyclable
    {
        int remaining;
        public WhenAllTracker(ILifetime[] lts)
        {
            if (lts.Length == 0)
            {
                Dispose("external/klooie/src/klooie/Observability/Recyclable.cs:171");
                return;
            }
            remaining = lts.Length;
            foreach (var lt in lts)
            {
                lt.OnDisposed(Count);
            }
        }
        private void Count()
        {
            if (Interlocked.Decrement(ref remaining) == 0)
            {
                Dispose("external/klooie/src/klooie/Observability/Recyclable.cs:184");
            }
        }
    }
}



public class GenericReferenceEqualityComparer<T> : IEqualityComparer<T>
{
    public static GenericReferenceEqualityComparer<T> Instance { get; } = new GenericReferenceEqualityComparer<T>();
    private GenericReferenceEqualityComparer() { }
    public bool Equals(T x, T y) => ReferenceEquals(x, y);
    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}

public static class TaskExtensions
{
    public static Recyclable ToLifetime(this Task t, EventLoop loop = null)
    {
        loop ??= ConsoleApp.Current;
        if (loop == null) throw new ArgumentException("ToLifetime() requires an event loop");
        var lt = DefaultRecyclablePool.Instance.Rent(out int lease);
        loop.Invoke(async () =>
        {
            await t;
            lt.TryDispose(lease, "external/klooie/src/klooie/Observability/Recyclable.cs:213");
        });
        return lt;
    }
}

using System.Runtime.CompilerServices;
namespace klooie;

public class Recyclable : ILifetime
{
    public string DisposalReason { get; set; } 
    public static bool PoolingEnabled { get; set; } = true;
    public static StackHunterMode StackHunterMode { get; set; } = StackHunterMode.Off;
    private static readonly Recyclable forever = new Recyclable();
    public static Recyclable Forever => forever;

    private SubscriberCollection? disposalSubscribers;
    private SubscriberCollection DisposalSubscribers => disposalSubscribers ??= SubscriberCollection.Create();

    internal IObjectPool? Pool { get; set; }

    private bool IsExpiring { get; set; }
    private bool IsExpired { get; set; }


    public int CurrentVersion { get; private set; }

    public int Lease => CurrentVersion;

    public bool IsStillValid(int leaseVersion)
    {
        return !IsExpired && !IsExpiring && leaseVersion == CurrentVersion;
    }

    private TaskCompletionSource? endedTaskCompletionSource;

    public Recyclable()
    {
        Rent();
    }

    public Task AsTask() => endedTaskCompletionSource?.Task ?? (endedTaskCompletionSource = new TaskCompletionSource()).Task;

    public static void TryDisposeMe(Recyclable me) => me.TryDispose("Recyclable.TryDisposeMe");

    public bool TryDispose(int lease, string? reason = null)
    {
        if (Lease != lease) return false;
        return TryDispose(reason);
    }

    public bool TryDispose(string? reason = null)
    {
        if (IsExpired || IsExpiring) return false;
        Dispose(reason);
        return true;
    }

    public void Dispose(string? reason = null)
    {
        if (IsExpiring || IsExpired) throw new InvalidOperationException("Cannot dispose an object that is already being disposed or has been disposed");
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

            if (endedTaskCompletionSource != null)
            {
                endedTaskCompletionSource.SetResult();
                endedTaskCompletionSource = null;
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
        endedTaskCompletionSource = null;
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
            tracker.Dispose();
        });
        return ret;
    }

    public Recyclable CreateChildRecyclable()
    {
        return CreateChildRecyclable(out _);
    }


    public void OnDisposed(Action cleanupCode)
    {
        if(IsExpired || IsExpiring) throw new InvalidOperationException("Cannot add a disposal callback to an object that is already being disposed or has been disposed");
        var subscription = ActionSubscription.Create(cleanupCode);
        DisposalSubscribers.Track(subscription);
    }

    public void OnDisposed<T>(T scope, Action<T> cleanupCode)
    {
        if (IsExpired || IsExpiring) throw new InvalidOperationException("Cannot add a disposal callback to an object that is already being disposed or has been disposed");
        if (scope == null) throw new ArgumentNullException(nameof(scope));
        var subscription = ScopedSubscription<T>.Create(scope, cleanupCode);
        DisposalSubscribers.Track(subscription);
    }

    private class EarliestOfTracker : Recyclable
    {
        public EarliestOfTracker(ILifetime[] lts)
        {
            if (lts.Length == 0)
            {
                Dispose();
                return;
            }
            foreach (var lt in lts)
            {
                lt?.OnDisposed(this, DisposeMe);
            }
        }

        private static void DisposeMe(object me) => ((EarliestOfTracker)me).TryDispose();
    }

    private class WhenAllTracker : Recyclable
    {
        int remaining;
        public WhenAllTracker(ILifetime[] lts)
        {
            if (lts.Length == 0)
            {
                Dispose();
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
                Dispose();
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
            lt.TryDispose(lease);
        });
        return lt;
    }
}
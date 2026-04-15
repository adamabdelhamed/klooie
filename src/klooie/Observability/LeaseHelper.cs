using System;

namespace klooie;

/// <summary>
/// Helper methods for tracking lifetimes of <see cref="Recyclable"/> objects.
/// </summary>
public static class LeaseHelper
{
    /// <summary>
    /// Creates a <see cref="LeaseState{TOwner, TRecyclable}"/> that tracks the
    /// lease of both an owner and a recyclable object.
    /// </summary>
    public static LeaseState<TOwner, TRecyclable> TrackOwnerRelationship<TOwner, TRecyclable>(TOwner owner, TRecyclable recyclable) where TOwner : Recyclable where TRecyclable : Recyclable
        => LeaseState<TOwner, TRecyclable>.Create(owner, recyclable);

    /// <summary>
    /// Creates a <see cref="LeaseStateWithStash{TOwner, TRecyclable, TStash}"/> that tracks the
    /// lease of both an owner and a recyclable object plus a strongly typed stash value.
    /// </summary>
    public static LeaseStateWithStash<TOwner, TRecyclable, TStash> TrackOwnerRelationship<TOwner, TRecyclable, TStash>(TOwner owner, TRecyclable recyclable, TStash stash) where TOwner : Recyclable where TRecyclable : Recyclable
        => LeaseStateWithStash<TOwner, TRecyclable, TStash>.Create(owner, recyclable, stash);

    /// <summary>
    /// Creates a <see cref="LeaseState{TRecyclable}"/> that tracks the lease of a single object.
    /// </summary>
    public static LeaseState<TRecyclable> Track<TRecyclable>(TRecyclable recyclable) where TRecyclable : Recyclable
        => LeaseState<TRecyclable>.Create(recyclable);

    /// <summary>
    /// Creates a <see cref="LeaseStateWithStash{TRecyclable, TStash}"/> that tracks the lease of a single
    /// object plus a strongly typed stash value.
    /// </summary>
    public static LeaseStateWithStash<TRecyclable, TStash> Track<TRecyclable, TStash>(TRecyclable recyclable, TStash stash) where TRecyclable : Recyclable
        => LeaseStateWithStash<TRecyclable, TStash>.Create(recyclable, stash);

    /// <summary>
    /// Updates the lease state of a recyclable object, recycling the old instance if necessary.
    /// </summary>
    public static void Recycle<T>(T toTrack, ref LeaseState<T> trackerReference) where T : Recyclable
    {
        if (trackerReference != null)
        {
            trackerReference.Recycle(toTrack);
            return;
        }

        trackerReference = Track(toTrack);
    }

    /// <summary>
    /// Updates the lease state of a recyclable object with stash data, recycling the old instance if necessary.
    /// </summary>
    public static void Recycle<T, TStash>(T toTrack, TStash stash, ref LeaseStateWithStash<T, TStash> trackerReference) where T : Recyclable
    {
        if (trackerReference != null)
        {
            trackerReference.Recycle(toTrack, stash);
            return;
        }

        trackerReference = Track(toTrack, stash);
    }
}

/// <summary>
/// Represents a snapshot of the leases for an owner object and a recyclable it owns.
/// </summary>
public class LeaseState<TOwner, TRecyclable> : Recyclable where TOwner : Recyclable where TRecyclable : Recyclable
{
    private static readonly LazyPool<LeaseState<TOwner, TRecyclable>> pool = new(() => new LeaseState<TOwner, TRecyclable>());

    /// <summary>
    /// The recyclable instance being tracked.
    /// </summary>
    public TRecyclable? Recyclable { get; private set; }

    /// <summary>
    /// The owner of the recyclable instance.
    /// </summary>
    public TOwner? Owner { get; private set; }

    /// <summary>
    /// The lease value captured for <see cref="Recyclable"/> when tracking started.
    /// </summary>
    public int RecyclableLease { get; private set; }

    /// <summary>
    /// The lease value captured for <see cref="Owner"/> when tracking started.
    /// </summary>
    public int OwnerLease { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the owner is still valid for the captured lease.
    /// </summary>
    public bool IsOwnerValid => Owner != null && Owner.IsStillValid(OwnerLease);

    /// <summary>
    /// Gets a value indicating whether the recyclable is still valid for the captured lease.
    /// </summary>
    public bool IsRecyclableValid => Recyclable != null && Recyclable.IsStillValid(RecyclableLease);

    protected LeaseState() { }

    /// <summary>
    /// Creates and initializes a lease state for the specified owner/recyclable pair.
    /// </summary>
    public static LeaseState<TOwner, TRecyclable> Create(TOwner owner, TRecyclable recyclable)
    {
        var ret = pool.Value.Rent();
        ret.Initialize(owner, recyclable);
        return ret;
    }

    /// <summary>
    /// Attempts to dispose the tracked recyclable using the captured lease.
    /// </summary>
    public void TryDisposeRecyclable() => Recyclable?.TryDispose(RecyclableLease, "external/klooie/src/klooie/Observability/LeaseHelper.cs:118");

    /// <summary>
    /// Attempts to dispose the tracked owner using the captured lease.
    /// </summary>
    public bool TryDisposeOwner() => Owner != null && Owner.TryDispose(OwnerLease, "external/klooie/src/klooie/Observability/LeaseHelper.cs:123");

    /// <summary>
    /// Initializes this instance with the given owner/recyclable pair.
    /// </summary>
    protected void Initialize(TOwner owner, TRecyclable recyclable)
    {
        Owner = owner;
        Recyclable = recyclable;
        OwnerLease = owner.Lease;
        RecyclableLease = recyclable.Lease;
    }

    /// <summary>
    /// Clears tracked values when the state object is returned to the pool.
    /// </summary>
    protected override void OnReturn()
    {
        base.OnReturn();
        Owner = null;
        Recyclable = null;
        OwnerLease = default;
        RecyclableLease = default;
    }
}

/// <summary>
/// Represents a snapshot of the leases for an owner object and a recyclable it owns,
/// plus a strongly typed stash value.
/// </summary>
public class LeaseStateWithStash<TOwner, TRecyclable, TStash> : LeaseState<TOwner, TRecyclable> where TOwner : Recyclable where TRecyclable : Recyclable
{
    private static readonly LazyPool<LeaseStateWithStash<TOwner, TRecyclable, TStash>> pool = new(() => new LeaseStateWithStash<TOwner, TRecyclable, TStash>());

    /// <summary>
    /// Additional strongly typed state that can travel with this lease state.
    /// </summary>
    public TStash? Stash { get; private set; }

    protected LeaseStateWithStash() { }

    /// <summary>
    /// Creates and initializes a lease state for the specified owner/recyclable pair and stash value.
    /// </summary>
    public static LeaseStateWithStash<TOwner, TRecyclable, TStash> Create(TOwner owner, TRecyclable recyclable, TStash stash)
    {
        var ret = pool.Value.Rent();
        ret.Initialize(owner, recyclable);
        ret.Stash = stash;
        return ret;
    }

    /// <summary>
    /// Clears tracked values when the state object is returned to the pool.
    /// </summary>
    protected override void OnReturn()
    {
        base.OnReturn();
        Stash = default;
    }
}

/// <summary>
/// Represents a snapshot of the lease for a single recyclable object.
/// </summary>
public class LeaseState<TRecyclable> : Recyclable where TRecyclable : Recyclable
{
    private static readonly LazyPool<LeaseState<TRecyclable>> pool = new(() => new LeaseState<TRecyclable>());

    /// <summary>
    /// The recyclable instance being tracked.
    /// </summary>
    public TRecyclable? Recyclable { get => field != null && field.IsStillValid(RecyclableLease) ? field : null; private set; }

    /// <summary>
    /// The lease value captured when tracking started.
    /// </summary>
    public int RecyclableLease { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the recyclable is still valid for the captured lease.
    /// </summary>
    public bool IsRecyclableValid => Recyclable != null && Recyclable.IsStillValid(RecyclableLease);

    protected LeaseState() { }

    /// <summary>
    /// Creates and initializes a lease state for the given recyclable.
    /// </summary>
    public static LeaseState<TRecyclable> Create(TRecyclable recyclable)
    {
        var ret = pool.Value.Rent();
        ret.Initialize(recyclable);
        return ret;
    }

    [Obsolete("Use the version that lets you pass a reason")]
    public void UnTrackAndDispose() => UnTrackAndDispose("LeaseState<T>.UnTrackAndDispose");

    public void UnTrackAndDispose(string reason)
    {
        if(reason == null) throw new ArgumentException("Reason cannot be null", nameof(reason));
        TryDisposeRecyclable();
        TryDispose(Lease, reason);
    }

    public void Recycle(TRecyclable replacement)
    {
        TryDisposeRecyclable();
        Initialize(replacement);
    }

    /// <summary>
    /// Attempts to dispose the tracked recyclable using the captured lease.
    /// </summary>
    public void TryDisposeRecyclable() => Recyclable?.TryDispose(RecyclableLease, "external/klooie/src/klooie/Observability/LeaseHelper.cs:234");

    /// <summary>
    /// Initializes this instance with the given recyclable.
    /// </summary>
    protected void Initialize(TRecyclable recyclable)
    {
        Recyclable = recyclable;
        RecyclableLease = recyclable.Lease;
    }

    /// <summary>
    /// Clears tracked values when this state object is returned to the pool.
    /// </summary>
    protected override void OnReturn()
    {
        base.OnReturn();
        Recyclable = null;
        RecyclableLease = default;
    }

    // Should only be used when the lease never had its reference stored, but instead had it created and passed as a callback scope where the caller
    // is the only code that ever gets a reference to it.
    public void FinishedTracking(string reason) => Dispose(Lease, reason);
}

/// <summary>
/// Represents a snapshot of the lease for a single recyclable object,
/// plus a strongly typed stash value.
/// </summary>
public class LeaseStateWithStash<TRecyclable, TStash> : LeaseState<TRecyclable> where TRecyclable : Recyclable
{
    private static readonly LazyPool<LeaseStateWithStash<TRecyclable, TStash>> pool = new(() => new LeaseStateWithStash<TRecyclable, TStash>());

    /// <summary>
    /// Additional strongly typed state that can travel with this lease state.
    /// </summary>
    public TStash? Stash { get; private set; }

    protected LeaseStateWithStash() { }

    /// <summary>
    /// Creates and initializes a lease state for the given recyclable and stash value.
    /// </summary>
    public static LeaseStateWithStash<TRecyclable, TStash> Create(TRecyclable recyclable, TStash stash)
    {
        var ret = pool.Value.Rent();
        ret.Initialize(recyclable);
        ret.Stash = stash;
        return ret;
    }

    /// <summary>
    /// Recycles the tracked recyclable and updates the stash value.
    /// </summary>
    public void Recycle(TRecyclable replacement, TStash stash)
    {
        base.Recycle(replacement);
        Stash = stash;
    }

    /// <summary>
    /// Clears tracked values when this state object is returned to the pool.
    /// </summary>
    protected override void OnReturn()
    {
        base.OnReturn();
        Stash = default;
    }
}

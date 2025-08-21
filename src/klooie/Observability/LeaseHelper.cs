using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
    /// <typeparam name="TOwner">The type of the owner.</typeparam>
    /// <typeparam name="TRecyclable">The type being tracked.</typeparam>
    /// <param name="owner">The owning object.</param>
    /// <param name="recyclable">The recyclable object whose lifetime is being tracked.</param>
    /// <returns>A lease state representing the owner/recyclable relationship.</returns>
    public static LeaseState<TOwner, TRecyclable> TrackOwnerRelationship<TOwner, TRecyclable>(TOwner owner, TRecyclable recyclable) where TOwner : Recyclable where TRecyclable : Recyclable
        => LeaseState<TOwner, TRecyclable>.Create(owner, recyclable);

    /// <summary>
    /// Creates a <see cref="LeaseState{TRecyclable}"/> that tracks the lease of a single object.
    /// </summary>
    /// <typeparam name="TRecyclable">The type being tracked.</typeparam>
    /// <param name="recyclable">The recyclable object to track.</param>
    /// <returns>A lease state representing the object and its current lease.</returns>
    public static LeaseState<TRecyclable> Track<TRecyclable>(TRecyclable recyclable) where TRecyclable : Recyclable
        => LeaseState<TRecyclable>.Create(recyclable);

    /// <summary>
    /// Updates the lease state of a recyclable object, recycling the old instance if necessary.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="toTrack"></param>
    /// <param name="trackerReference"></param>
    public static void Recycle<T>(T toTrack, ref LeaseState<T> trackerReference) where T : Recyclable
    {
        if (trackerReference != null)
        {
            trackerReference.Recycle(toTrack);
            return;
        }
        trackerReference = LeaseHelper.Track(toTrack);
    }
}

/// <summary>
/// Represents a snapshot of the leases for an owner object and a recyclable it owns.
/// </summary>
/// <typeparam name="TOwner">Type of the owner.</typeparam>
/// <typeparam name="TRecyclable">Type of the recyclable being tracked.</typeparam>
public class LeaseState<TOwner, TRecyclable> : Recyclable where TRecyclable : Recyclable where TOwner : Recyclable
{
    private static LazyPool<LeaseState<TOwner, TRecyclable>> pool = new LazyPool<LeaseState<TOwner, TRecyclable>>(() => new LeaseState<TOwner, TRecyclable>());

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

    private LeaseState() { }

    /// <summary>
    /// Creates and initializes a lease state for the specified owner/recyclable pair.
    /// </summary>
    /// <param name="owner">The owning object.</param>
    /// <param name="recyclable">The recyclable instance.</param>
    /// <returns>The created lease state.</returns>
    public static LeaseState<TOwner, TRecyclable> Create(TOwner owner, TRecyclable recyclable)
    {
        var ret = pool.Value.Rent();
        ret.Recyclable = recyclable;
        ret.Owner = owner;
        ret.RecyclableLease = recyclable.Lease;
        ret.OwnerLease = owner.Lease;
        return ret;
    }

    /// <summary>
    /// Attempts to dispose the tracked recyclable using the captured lease.
    /// </summary>
    public void TryDisposeRecyclable() => Recyclable?.TryDispose(RecyclableLease);

    /// <summary>
    /// Attempts to dispose the tracked owner using the captured lease.
    /// </summary>
    public bool TryDisposeOwner() => Owner == null ? false : Owner.TryDispose(OwnerLease);

    /// <summary>
    /// Clears tracked values when the state object is returned to the pool.
    /// </summary>
    protected override void OnReturn()
    {
        base.OnReturn();
        Recyclable = default;
        Owner = default;
        RecyclableLease = default;
        OwnerLease = default;
    }
}

/// <summary>
/// Represents a snapshot of the lease for a single recyclable object.
/// </summary>
/// <typeparam name="TRecyclable">The type being tracked.</typeparam>
public class LeaseState<TRecyclable> : Recyclable where TRecyclable : Recyclable
{
    private static LazyPool<LeaseState<TRecyclable>> pool = new LazyPool<LeaseState<TRecyclable>>(() => new LeaseState<TRecyclable>());

    /// <summary>
    /// The recyclable instance being tracked.
    /// </summary>
    public TRecyclable? Recyclable { get; private set; }

    /// <summary>
    /// The lease value captured when tracking started.
    /// </summary>
    public int RecyclableLease { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the recyclable is still valid for the captured lease.
    /// </summary>
    public bool IsRecyclableValid => Recyclable != null && Recyclable.IsStillValid(RecyclableLease);

    private LeaseState() { }

    /// <summary>
    /// Creates and initializes a lease state for the given recyclable.
    /// </summary>
    /// <param name="recyclable">The recyclable to track.</param>
    /// <returns>The created lease state.</returns>
    public static LeaseState<TRecyclable> Create(TRecyclable recyclable)
    {
        var ret = pool.Value.Rent();
        ret.Recyclable = recyclable;
        ret.RecyclableLease = recyclable.Lease;
        return ret;
    }

    public void UnTrackAndDispose()
    {
        TryDisposeRecyclable();
        TryDispose();
    }



    public void Recycle(TRecyclable replacement)
    {
        TryDisposeRecyclable();
        RecyclableLease = replacement.Lease;
        Recyclable = replacement;
    }

    /// <summary>
    /// Attempts to dispose the tracked recyclable using the captured lease.
    /// </summary>
    public void TryDisposeRecyclable() => Recyclable?.TryDispose(RecyclableLease);

    /// <summary>
    /// Clears tracked values when this state object is returned to the pool.
    /// </summary>
    protected override void OnReturn()
    {
        base.OnReturn();
        Recyclable = default;
        RecyclableLease = default;
    }
}
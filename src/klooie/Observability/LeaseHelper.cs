using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace klooie;
public static class LeaseHelper
{
    public static LeaseState<TOwner, TRecyclable> TrackOwnerRelationship<TOwner, TRecyclable>(TOwner owner, TRecyclable recyclable) where TOwner : Recyclable where TRecyclable : Recyclable 
        => LeaseState<TOwner, TRecyclable>.Create(owner, recyclable);

    public static LeaseState<TRecyclable> Track<TRecyclable>(TRecyclable recyclable) where TRecyclable : Recyclable
        => LeaseState<TRecyclable>.Create(recyclable);
}

public class LeaseState<TOwner, TRecyclable> : Recyclable where TRecyclable : Recyclable where TOwner : Recyclable
{
    private static LazyPool<LeaseState<TOwner, TRecyclable>> pool = new LazyPool<LeaseState<TOwner, TRecyclable>>(() => new LeaseState<TOwner, TRecyclable>());

    public TRecyclable? Recyclable { get; private set; }
    public TOwner? Owner { get; private set; }
    public int RecyclableLease { get; private set; }
    public int OwnerLease { get; private set; }

    public bool IsOwnerValid => Owner != null && Owner.IsStillValid(OwnerLease);
    public bool IsRecyclableValid => Recyclable != null && Recyclable.IsStillValid(RecyclableLease);

    private LeaseState() { }

    public static LeaseState<TOwner, TRecyclable> Create(TOwner owner, TRecyclable recyclable)
    {
        var ret = pool.Value.Rent();
        ret.Recyclable = recyclable;
        ret.Owner = owner;
        ret.RecyclableLease = recyclable.Lease;
        ret.OwnerLease = owner.Lease;
        return ret;
    }

    public void TryDisposeRecyclable() => Recyclable?.TryDispose(RecyclableLease);
    public void TryDisposeOwner() => Owner?.TryDispose(OwnerLease);

    protected override void OnReturn()
    {
        base.OnReturn();
        Recyclable = default;
        Owner = default;
        RecyclableLease = default;
        OwnerLease = default;
    }
}

public class LeaseState<TRecyclable> : Recyclable where TRecyclable : Recyclable
{
    private static LazyPool<LeaseState<TRecyclable>> pool = new LazyPool<LeaseState<TRecyclable>>(() => new LeaseState<TRecyclable>());

    public TRecyclable? Recyclable { get; private set; }
    public int RecyclableLease { get; private set; }
    public bool IsRecyclableValid => Recyclable != null && Recyclable.IsStillValid(RecyclableLease);

    private LeaseState() { }

    public static LeaseState<TRecyclable> Create(TRecyclable recyclable)
    {
        var ret = pool.Value.Rent();
        ret.Recyclable = recyclable;
        ret.RecyclableLease = recyclable.Lease;
        return ret;
    }

    public void TryDisposeRecyclable() => Recyclable?.TryDispose(RecyclableLease);

    protected override void OnReturn()
    {
        base.OnReturn();
        Recyclable = default;
        RecyclableLease = default;
    }
}
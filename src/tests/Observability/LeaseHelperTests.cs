using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Observability)]
public class LeaseHelperTests
{
    [TestMethod]
    public void TrackOwnerRelationship_DetectsDisposal()
    {
        var owner = DefaultRecyclablePool.Instance.Rent();
        var child = DefaultRecyclablePool.Instance.Rent();

        var state = LeaseHelper.TrackOwnerRelationship(owner, child);
        try
        {
            Assert.IsTrue(state.IsOwnerValid);
            Assert.IsTrue(state.IsRecyclableValid);

            owner.Dispose();
            Assert.IsFalse(state.IsOwnerValid);
            Assert.IsTrue(state.IsRecyclableValid);

            child.Dispose();
            Assert.IsFalse(state.IsRecyclableValid);
        }
        finally
        {
            state.Dispose();
            owner.TryDispose();
            child.TryDispose();
        }
    }

    [TestMethod]
    public void TryDisposeMethods_DisposeTrackedObjects()
    {
        var owner = DefaultRecyclablePool.Instance.Rent();
        var child = DefaultRecyclablePool.Instance.Rent();
        var state = LeaseHelper.TrackOwnerRelationship(owner, child);

        var ownerLease = owner.Lease;
        var childLease = child.Lease;

        try
        {
            state.TryDisposeRecyclable();
            Assert.IsFalse(child.IsStillValid(childLease));

            state.TryDisposeOwner();
            Assert.IsFalse(owner.IsStillValid(ownerLease));
        }
        finally
        {
            state.Dispose();
        }
    }

    [TestMethod]
    public void Track_LeaseSnapshot_RemainsInvalidAfterReuse()
    {
        var item = DefaultRecyclablePool.Instance.Rent();
        var state = LeaseHelper.Track(item);
        var originalLease = item.Lease;

        item.Dispose();
        var rerented = DefaultRecyclablePool.Instance.Rent();
        var newLease = rerented.Lease;

        Assert.AreSame(item, rerented);
        Assert.AreNotEqual(originalLease, newLease);
        Assert.IsFalse(state.IsRecyclableValid);

        rerented.Dispose();
        state.Dispose();
    }
}

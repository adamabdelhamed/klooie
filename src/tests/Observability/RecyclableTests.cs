using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace klooie.tests;
[TestClass]
public class RecyclableTests
{
    [TestMethod]
    public void Recyclable_Basic()
    {
        var toObserve = DefaultRecyclablePool.Instance.Rent();
        int count = 0;
        toObserve.OnDisposed(() => count++);
        Assert.AreEqual(0, count);
        toObserve.Dispose();
        Assert.AreEqual(1, count);

        var newlyRented = DefaultRecyclablePool.Instance.Rent();
        Assert.AreSame(toObserve, newlyRented);

        newlyRented.Dispose();
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void ConsoleControl_DisposalSanity()
    {
        var c = ConsoleControlPool.Instance.Rent();
        c.Dispose();
    }
}

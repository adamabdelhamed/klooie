using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace klooie.tests;
[TestClass]
[TestCategory(Categories.Observability)]
public class RecyclableTests
{
    [TestMethod]
    public void Recyclable_Basic()
    {
        var toObserve = DefaultRecyclablePool.Instance.Rent();
        int count = 0;
        toObserve.OnDisposed(() => count++);
        Assert.AreEqual(0, count);
        toObserve.Dispose("external/klooie/src/tests/Observability/RecyclableTests.cs:1");
        Assert.AreEqual(1, count);

        var newlyRented = DefaultRecyclablePool.Instance.Rent();
        Assert.AreSame(toObserve, newlyRented);

        newlyRented.Dispose("external/klooie/src/tests/Observability/RecyclableTests.cs:1");
        Assert.AreEqual(1, count);
    }

    [TestMethod]
    public void ConsoleControl_DisposalSanity()
    {
        var c = ConsoleControlPool.Instance.Rent();
        c.Dispose("external/klooie/src/tests/Observability/RecyclableTests.cs:1");
    }
}

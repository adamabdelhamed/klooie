
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace klooie.tests;

[TestClass]    
[TestCategory(Categories.Geometry)]
public class EdgeTests
{
    [TestMethod]
    public void Edge_Equality()
    {
        var e = new Edge(1, 2, 3, 4);
        Assert.AreEqual(1, e.X1);
        Assert.AreEqual(2, e.Y1);
        Assert.AreEqual(3, e.X2);
        Assert.AreEqual(4, e.Y2);

        var e2 = new Edge(1, 2, 3, 4);
        Assert.AreEqual(e, e2);
        Assert.AreEqual(e.GetHashCode(), e2.GetHashCode());
        Assert.IsTrue(e == e2);
        Assert.IsFalse(e != e2);
    }
}



using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace klooie.tests;

[TestClass]    
[TestCategory(Categories.Geometry)]
public class LocFTests
{
    [TestMethod]
    public void LocF_Equality()
    {
        var l = new LocF(1, 2);
        Assert.AreEqual(1, l.Left);
        Assert.AreEqual(2, l.Top);

        var l2 = new LocF(1, 2);
        Assert.AreEqual(l, l2);
        Assert.AreEqual(l.GetHashCode(), l2.GetHashCode());
        Assert.IsTrue(l == l2);
        Assert.IsFalse(l != l2);

        var l3 = new LocF(1, 3);
        Assert.AreNotEqual(l, l3);
        Assert.AreNotEqual(l.GetHashCode(), l3.GetHashCode());
        Assert.IsFalse(l == l3);
        Assert.IsTrue(l != l3);
    }

    [TestMethod]
    public void LocF_Angles()
    {
        Assert.AreEqual(45, new LocF(0, 0).CalculateAngleTo(1, 1));
        Assert.AreEqual(0, new LocF(0, 0).CalculateAngleTo(1, 0));
        Assert.AreEqual(180, new LocF(0, 0).CalculateAngleTo(-1, 0));
        Assert.AreEqual(270, new LocF(0, 0).CalculateAngleTo(0, -1));
        Assert.AreEqual(90, new LocF(0, 0).CalculateAngleTo(0, 1));
    }

    [TestMethod]
    public void LocF_Distances()
    {
        Assert.AreEqual(1, new LocF(0, 0).CalculateDistanceTo(1, 0));
        Assert.AreEqual(1, new LocF(0, 0).CalculateDistanceTo(-1, 0));
        Assert.AreEqual(1, new LocF(0, 0).CalculateNormalizedDistanceTo(1, 0));
        Assert.AreEqual(2f, new LocF(0, 0).CalculateNormalizedDistanceTo(0, 1));
    }

    [TestMethod]
    public void LocF_Offset()
    {
        Assert.IsTrue(new LocF(0, 0).Offset(1, 1).Equals(new LocF(1, 1)));
        Assert.IsTrue(new LocF(1, 1).Offset(-2, -2).Equals(new LocF(-1, -1)));
    }

    [TestMethod]
    public void LocF_ToRect()
    {
        Assert.AreEqual(new RectF(-.5f,-.5f,1,1), new LocF(0, 0).ToRect(1, 1));
        Assert.AreNotEqual(new RectF(0, 0, 1, 1), new LocF(0, 0).ToRect(1, 1));
    }

    [TestMethod]
    public void LocF_ConvertToLoc()
    {
        var l = new LocF(.5f, .5f);
        var converted = l.ToLoc();
        var convertedF = converted.ToLocF();
        Assert.AreEqual(converted, new Loc(1, 1));
        Assert.IsTrue(converted.Equals(convertedF));
        Assert.AreEqual(new Loc(1, 1), converted);
        Assert.IsTrue(convertedF.Equals(converted));
    }
}



using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace klooie.tests;

[TestClass]    
[TestCategory(Categories.Geometry)]
public class RectFTests
{
    [TestMethod]
    public void RectF_Equality()
    {
        var r1 = new RectF(-1, -1, 2, 2);
        var r2 = new RectF(-1, -1, 2, 2);
        var r3 = new RectF(-1, -2, 2, 2);
        
        Assert.AreEqual(r1, r2);
        Assert.AreNotEqual(r1, r3);

        Assert.AreEqual(r1.GetHashCode(), r2.GetHashCode());
        Assert.AreNotEqual(r1.GetHashCode(), r3.GetHashCode());
    }

    [TestMethod]
    public void RectF_Is()
    {
        Assert.IsTrue(new RectF(0, 0, 1, 1).IsLeftOf(new RectF(100, 0, 1, 1)));
        Assert.IsFalse(new RectF(0, 0, 1, 1).IsRightOf(new RectF(100, 0, 1, 1)));

        Assert.IsTrue(new RectF(0, 0, 1, 1).IsAbove(new RectF(0, 100, 1, 1)));
        Assert.IsFalse(new RectF(0, 0, 1, 1).IsBelow(new RectF(0, 100, 1, 1)));
    }

    [TestMethod]
    public void RectF_Distance()
    {
        Assert.AreEqual(0, new RectF(0, 0, 1, 1).CalculateDistanceTo(new RectF(0, 0, 1, 1)));
        Assert.AreEqual(0, new RectF(0, 0, 1, 1).CalculateDistanceTo(new RectF(1, 0, 1, 1)));
        Assert.AreEqual(0, new RectF(0, 0, 1, 1).CalculateDistanceTo(new RectF(0, 1, 1, 1)));

        Assert.AreEqual(1, new RectF(0, 0, 1, 1).CalculateDistanceTo(new RectF(2, 0, 1, 1)));
    }

    [TestMethod]
    public void RectF_Shrink()
    {
        var r = new RectF(0, 0, 1, 1);
        var small = r.Shrink(.5f);
        Assert.AreEqual(.5f,small.Width);
        Assert.AreEqual(.5f, small.Height);
        Assert.AreEqual(r.Center, small.Center);
    }

    [TestMethod]
    public void RectF_Grow()
    {
        var r = new RectF(0, 0, 1, 1);
        var small = r.Grow(.5f);
        Assert.AreEqual(1.5f, small.Width);
        Assert.AreEqual(1.5f, small.Height);
        Assert.AreEqual(r.Center, small.Center);
    }

    [TestMethod]
    public void RectF_Overlap()
    {
        var r = new RectF(0, 0, 1, 1);
        var r2 = new RectF(0, 0, 2, 1);
        var small = r.Grow(.5f);
        Assert.AreEqual(.5f, r.OverlapPercentage(r2));
        Assert.AreEqual(1, r2.OverlapPercentage(r));

        Assert.AreEqual(1, r.NumberOfPixelsThatOverlap(r2));
        Assert.AreEqual(1, r2.NumberOfPixelsThatOverlap(r));
        Assert.AreEqual(2, r2.NumberOfPixelsThatOverlap(r2));

        Assert.IsTrue(r.Touches(r2));
        Assert.IsTrue(r2.Touches(r));

        Assert.IsFalse(r.Contains(r2));
        Assert.IsTrue(r2.Contains(r));
    }
}


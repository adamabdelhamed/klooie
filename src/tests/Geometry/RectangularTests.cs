
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace klooie.tests;

[TestClass]    
[TestCategory(Categories.Geometry)]
public class RectangularTests
{
    [TestMethod]
    public void Rectangular_FloatMoveHelpers()
    {
        var r = new Rectangular();

        r.MoveTo(1, 2);
        Assert.AreEqual(1, r.Left);
        Assert.AreEqual(2, r.Top);

        r.MoveBy(1, 0);
        Assert.AreEqual(2, r.Left);
        Assert.AreEqual(2, r.Top);

        r.MoveBy(0, 1);
        Assert.AreEqual(2, r.Left);
        Assert.AreEqual(3, r.Top);
    }

    [TestMethod]
    public void Rectangular_FloatResizeHelpers()
    {
        var r = new Rectangular();

        r.ResizeTo(1, 2);
        Assert.AreEqual(1, r.Width);
        Assert.AreEqual(2, r.Height);

        r.ResizeBy(1, 0);
        Assert.AreEqual(2, r.Width);
        Assert.AreEqual(2, r.Height);

        r.ResizeBy(0, 1);
        Assert.AreEqual(2, r.Width);
        Assert.AreEqual(3, r.Height);
    }
}



using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Linq;

namespace klooie.tests;

[TestClass]    
[TestCategory(Categories.Geometry)]
public class CircleTests
{

    [TestMethod]
    public void Circles_PositiveRadius()
    {
        try
        {
            new Circle(0, 0, -1);
            Assert.Fail("An exception should have been thrown");
        }
        catch(ArgumentException)
        {

        }
    }


    [TestMethod]
    public void Circles_Equality()
    {
        Assert.AreEqual(new Circle(0, 0, 0), new Circle(0, 0, 0));
        Assert.AreEqual(new Circle(1, 2, 3), new Circle(1, 2, 3));

        Assert.AreEqual(new Circle(0, 0, 0).GetHashCode(), new Circle(0, 0, 0).GetHashCode());
        Assert.AreEqual(new Circle(1, 2, 3).GetHashCode(), new Circle(1, 2, 3).GetHashCode());

        Assert.AreNotEqual(new Circle(1, 0, 0), new Circle(0, 0, 0));
        Assert.AreNotEqual(new Circle(0, 1, 0), new Circle(0, 0, 0));
        Assert.AreNotEqual(new Circle(0, 0, 1), new Circle(0, 0, 0));

        Assert.AreNotEqual(new Circle(0, 0, 0), new Circle(1, 0, 0));
        Assert.AreNotEqual(new Circle(0, 0, 0), new Circle(0, 1, 0));
        Assert.AreNotEqual(new Circle(0, 0, 0), new Circle(0, 0, 1));

        Assert.AreNotEqual(new Circle(1, 0, 0).GetHashCode(), new Circle(0, 0, 0).GetHashCode());
        Assert.AreNotEqual(new Circle(0, 1, 0).GetHashCode(), new Circle(0, 0, 0).GetHashCode());
        Assert.AreNotEqual(new Circle(0, 0, 1).GetHashCode(), new Circle(0, 0, 0).GetHashCode());

        Assert.AreNotEqual(new Circle(0, 0, 0).GetHashCode(), new Circle(1, 0, 0).GetHashCode());
        Assert.AreNotEqual(new Circle(0, 0, 0).GetHashCode(), new Circle(0, 1, 0).GetHashCode());
        Assert.AreNotEqual(new Circle(0, 0, 0).GetHashCode(), new Circle(0, 0, 1).GetHashCode());
    }

    [TestMethod]
   public void Circles_Intersections()
   {
        var circle = new Circle(1, 1, 5);

        var shouldMiss = new Edge(0, 100, 100, 100);
        Assert.AreEqual(0, circle.FindLineCircleIntersections(shouldMiss).Count());

        var shouldHitOnce = new Edge(-2, 6, 3, 6);
        Assert.AreEqual(1, circle.FindLineCircleIntersections(shouldHitOnce).Count());

        var shouldHitTwice = new Edge(-5,1,7,1);
        Assert.AreEqual(2, circle.FindLineCircleIntersections(shouldHitTwice).Count());
    }
}


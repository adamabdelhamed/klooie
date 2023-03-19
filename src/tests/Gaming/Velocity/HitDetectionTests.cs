using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Gaming)]
public class HitDetectionTests
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Setup()
    {
        ConsoleProvider.Current = new KlooieTestConsole()
        {
            BufferWidth = 80,
            WindowWidth = 80,
            WindowHeight = 51
        };
    }

    [TestMethod]
    public void HitDetection_Basic()
    {
        var from = new ColliderBox(new RectF(0, 0, 1, 1));
        var colliders = new ConsoleControl[]
        {
            new ColliderBox(new RectF(1.001f, 0, 1, 1))
        };
        var prediction = CollisionDetector.Predict(from, Angle.Right, colliders, 0, CastingMode.Precise);
        Assert.AreEqual(false, prediction.CollisionPredicted);

        prediction = CollisionDetector.Predict(from, Angle.Right, colliders, .001f, CastingMode.Precise);
        Assert.AreEqual(true, prediction.CollisionPredicted);
        AssertCloseEnough(0.001f, prediction.LKGD);
    }

    [TestMethod]
    public void HitDetection_LineIntersectionWhenSeparateParallel()
    {
        var a = new Edge(0, 0, 1, 0);
        var b = new Edge(1.001f, 0, 2, 0);
        Assert.IsFalse(CollisionDetector.TryFindIntersectionPoint(a, b, out float x, out float y));
    }

    [TestMethod]
    public void HitDetection_LineIntersectionWhenOverlappingParallel()
    {
        var a = new Edge(0, 0, 1, 0);
        var b = new Edge(.999f, 0, 2, 0);
        Assert.IsTrue(CollisionDetector.TryFindIntersectionPoint(a, b, out float x, out float y));
        Assert.AreEqual(.999f, x);
        Assert.AreEqual(0f, y);
    }

    [TestMethod]
    public void HitDetection_LineIntersectionWhenOverlappingALotParallel()
    {
        var a = new Edge(0, 0, 1, 0);
        var b = new Edge(.5f, 0, 2, 0);
        Assert.IsTrue(CollisionDetector.TryFindIntersectionPoint(a, b, out float x, out float y));
        Assert.AreEqual(.5f, x);
        Assert.AreEqual(0f, y);
    }

    [TestMethod]
    public void HitDetection_LineIntersectionSpecialCase()
    {
        var ray = new Edge(275.584778f, 0, 275.584778f, 8);
        var stationaryEdge = new Edge(275.3438f, 0, 275.3438f, 1);
        
        // For some reason this used to be IsTrue(), but looking at it I can't see why.
        // It now passes with the improvements made in this commit, but I'm nervous.
        Assert.IsFalse(CollisionDetector.TryFindIntersectionPoint(ray, stationaryEdge, out float x, out float y));
    }

    [TestMethod]
    public void HitDetection_LineIntersectionWhenBarelyTouchingParallel()
    {
        var a = new Edge(0, 0, 1, 0);
        var b = new Edge(1, 0, 2, 0);
        Assert.IsTrue(CollisionDetector.TryFindIntersectionPoint(a, b, out float x, out float y));
        Assert.AreEqual(1f, x);
        Assert.AreEqual(0f, y);
    }

    [TestMethod]
    public void HitDetection_LineIntersectionWhenBarelyTouchingPerpendicular()
    {
        var a = new Edge(0, 0, 1, 0);
        var b = new Edge(0, -.5f, 0, .5f);
        Assert.IsTrue(CollisionDetector.TryFindIntersectionPoint(a, b, out float x, out float y));
        Assert.AreEqual(0f, x);
        Assert.AreEqual(0f, y);
    }

    [TestMethod]
    public void HitDetection_LineIntersectionWhenBarelyTouching45()
    {
        var a = new Edge(0, 0, 1, 0);
        var b = new Edge(.5f, 0, 1, 1f);
        Assert.IsTrue(CollisionDetector.TryFindIntersectionPoint(a, b, out float x, out float y));
    }

    [TestMethod]
    public void HitDetection_LineIntersectionAtStartPoints()
    {
        var a = new Edge(0, 0, 1, 1);
        var b = new Edge(0, 0, -1, 1);
        Assert.IsTrue(CollisionDetector.TryFindIntersectionPoint(a, b, out float x, out float y));
        Assert.AreEqual(0f, x);
        Assert.AreEqual(0f, y);
    }

    [TestMethod]
    public void HitDetection_LineIntersectionAtEndPoints()
    {
        var a = new Edge(0, 0, 1, 1);
        var b = new Edge(-1, 0, 1, 1);
        Assert.IsTrue(CollisionDetector.TryFindIntersectionPoint(a, b, out float x, out float y));
        Assert.AreEqual(1f, x);
        Assert.AreEqual(1f, y);
    }

    [TestMethod]
    public void HitDetection_LineIntersectionCollinearNoOverlap()
    {
        var a = new Edge(0, 0, 1, 1);
        var b = new Edge(2, 2, 3, 3);
        Assert.IsFalse(CollisionDetector.TryFindIntersectionPoint(a, b, out float x, out float y));
    }

    [TestMethod]
    public void HitDetection_LineIntersectionNonParallelNoIntersection()
    {
        var a = new Edge(0, 0, 1, 1);
        var b = new Edge(2, 0, 2, 1);
        Assert.IsFalse(CollisionDetector.TryFindIntersectionPoint(a, b, out float x, out float y));
    }

    [TestMethod]
    public void HitDetection_LineIntersectionStartPointAndEndPoint()
    {
        var a = new Edge(0, 0, 1, 1);
        var b = new Edge(1, 1, 2, 2);
        Assert.IsTrue(CollisionDetector.TryFindIntersectionPoint(a, b, out float x, out float y));
        Assert.AreEqual(1f, x);
        Assert.AreEqual(1f, y);
    }

    
    public static void AssertCloseEnough(float expected, float actual)
    {
        var minAccepted = expected - CollisionDetector.VerySmallNumber;
        var maxExpected = expected + CollisionDetector.VerySmallNumber;
        Assert.IsTrue(actual >= minAccepted && actual <= maxExpected);
    }
}


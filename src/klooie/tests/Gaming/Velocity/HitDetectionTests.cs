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
        var prediction = HitDetection.PredictHit(from, Angle.Right, colliders, 0, CastingMode.Precise);
        Assert.AreEqual(HitType.None, prediction.Type);

        prediction = HitDetection.PredictHit(from, Angle.Right, colliders, .001f, CastingMode.Precise);
        Assert.AreEqual(HitType.Obstacle, prediction.Type);
        AssertCloseEnough(0.001f, prediction.LKGD);
    }

    public static void AssertCloseEnough(float expected, float actual)
    {
        var minAccepted = expected - HitDetection.VerySmallNumber;
        var maxExpected = expected + HitDetection.VerySmallNumber;
        Assert.IsTrue(actual >= minAccepted && actual <= maxExpected);
    }
}


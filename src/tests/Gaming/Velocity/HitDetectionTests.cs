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
    public void Setup() => TestContextHelper.GlobalSetup();

    [TestMethod]
    public void HitDetection_Basic()
    {
        var testLt = DefaultRecyclablePool.Instance.Rent();
        try
        { 
            var group = new ColliderGroup(testLt, null);

            var from = new GameCollider(new RectF(0, 0, 1, 1), false);
            from.ConnectToGroup(group);

            var to = new GameCollider(new RectF(1.001f, 0, 1, 1), false);
            to.ConnectToGroup(group);

            ConsoleControl[] colliders = [to];
              
            var prediction = CollisionDetector.Predict(from, Angle.Right, colliders, 0, CastingMode.Precise, colliders.Length, new CollisionPrediction());
            Assert.AreEqual(false, prediction.CollisionPredicted);

            prediction = CollisionDetector.Predict(from, Angle.Right, colliders, .01f, CastingMode.Precise, colliders.Length, prediction);
            Assert.AreEqual(true, prediction.CollisionPredicted);
            AssertCloseEnough(0.001f, prediction.LKGD);
        }
        finally
        {
            testLt.Dispose();
        }
    }
 

    
    public static void AssertCloseEnough(float expected, float actual)
    {
        var minAccepted = expected - (2 * CollisionDetector.VerySmallNumber);
        var maxExpected = expected + (2 * CollisionDetector.VerySmallNumber);
        Assert.IsTrue(actual >= minAccepted && actual <= maxExpected);
    }
}


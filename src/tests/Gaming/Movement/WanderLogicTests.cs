using klooie;
using klooie.Gaming;
using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tests.Gaming.Movement;
[TestClass]
[TestCategory(Categories.Gaming)]
public class WanderLogicTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Label_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        IVision vision = null;
        Velocity velocity = null;
        var wanderOptions = new WanderOptions()
        {
            Speed = () => 1,
            Velocity = velocity,
            Vision = vision,
            CloseEnough = 5,
            CuriousityPoint = () => null,
        };
        WanderLoopState state = WanderLoopState.Create(wanderOptions);
        var scores = WanderLogic.AdjustSpeedAndVelocity(state);
    });
}

public class TestVision : Recyclable, IVision
{
    public List<VisuallyTrackedObject> TrackedObjectsList { get; set; } = new List<VisuallyTrackedObject>();
    public float Range { get; set; } = 10;
}
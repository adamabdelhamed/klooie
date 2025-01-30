using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Gaming)]
public class TargetingTests
{
    [TestInitialize]
    public void Setup() => TestContextHelper.GlobalSetup();

    [TestMethod]
    public void TestTargetingEndsOnTargetDisposal()
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        { 
            var group = new ColliderGroup(lt, new ManualStopwatch());
            var shooter = new GameCollider(connectToMainColliderGroup: false);
            shooter.ConnectToGroup(group);

            var victim = new GameCollider(connectToMainColliderGroup: false);
            victim.ConnectToGroup(group);

            shooter.MoveTo(0, 0);
            victim.MoveTo(10, 0);

            var targeting = new Targeting() { Options = new TargetingOptions() { Source = shooter } };

            var targetFound = false;
            targeting.TargetAcquired.SubscribeOnce((target) =>
            {
                Assert.AreEqual(victim, target);
                targetFound = true;
            });
            targeting.Evaluate();
            Assert.IsTrue(targetFound);

            var detectedTargetChanged = false;
            targeting.TargetChanged.SubscribeOnce((target) =>
            {
                Assert.IsNull(target);
                Assert.IsNull(targeting.Target);
                detectedTargetChanged = true;
            });
            victim.Dispose();
            Assert.IsTrue(detectedTargetChanged);
        }
        finally
        {
            lt.Dispose();
        }
    }

    [TestMethod]
    public void TestTargetingEndsOnInvisible()
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            var group = new ColliderGroup(lt, new ManualStopwatch());
            var shooter = new GameCollider(connectToMainColliderGroup: false);
            shooter.ConnectToGroup(group);

            var victim = new GameCollider(connectToMainColliderGroup: false);
            victim.ConnectToGroup(group);

            shooter.MoveTo(0, 0);
            victim.MoveTo(10, 0);

            var targeting = new Targeting() { Options = new TargetingOptions() { Source = shooter } };

            var targetFound = false;
            targeting.TargetAcquired.SubscribeOnce((target) =>
            {
                Assert.AreEqual(victim, target);
                targetFound = true;
            });
            targeting.Evaluate();
            Assert.IsTrue(targetFound);

            var detectedTargetChanged = false;
            targeting.TargetChanged.SubscribeOnce((target) =>
            {
                Assert.IsNull(target);
                Assert.IsNull(targeting.Target);
                detectedTargetChanged = true;
            });
            victim.IsVisible = false;
            Assert.IsTrue(detectedTargetChanged);
        }
        finally
        {
            lt.Dispose();
        }
    }


    /// <summary>
    /// 1. Victim initially blocked by obstacle, then obstacle moves and validate that
    ///    targeting detects the target after the line of sight is cleared.
    /// </summary>
    [TestMethod]
    public void TestTargetingObstacleMovesAndThenVisible()
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            var group = new ColliderGroup(lt, new ManualStopwatch());
            var shooter = new GameCollider(connectToMainColliderGroup: false);
            shooter.ConnectToGroup(group);

            var obstacle = new GameCollider(connectToMainColliderGroup: false);
            obstacle.ConnectToGroup(group);

            var victim = new GameCollider(connectToMainColliderGroup: false);
            victim.AddTag("target");
            victim.ConnectToGroup(group);

            // Position: Shooter - Obstacle - Victim in a line
            shooter.MoveTo(0, 0);
            obstacle.MoveTo(10, 0);
            victim.MoveTo(20, 0);

            var targeting = new Targeting() { Options = new TargetingOptions() { Source = shooter, TargetTag = "target" } };

            // Initially blocked by obstacle
            targeting.Evaluate();
            Assert.IsNull(targeting.Target, "Target should not be acquired due to obstacle.");

            // Move obstacle out of the way
            obstacle.MoveTo(5, 5);

            bool targetAcquired = false;
            targeting.TargetAcquired.SubscribeOnce(t =>
            {
                targetAcquired = true;
                Assert.AreEqual(victim, t);
            });
            targeting.Evaluate();
            Assert.IsTrue(targetAcquired, "Target should be acquired after obstacle is moved.");
        }
          finally
        {
            lt.Dispose();
        }
    }

    /// <summary>
    /// 2. Test the Range option to ensure we don't detect targets that are too far away.
    /// </summary>
    [TestMethod]
    public void TestTargetingRespectsRange()
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            var group = new ColliderGroup(lt, new ManualStopwatch());

            var shooter = new GameCollider(connectToMainColliderGroup: false);
            shooter.ConnectToGroup(group);
            shooter.MoveTo(0, 0);


            var outOfRangeTarget = new GameCollider(connectToMainColliderGroup: false);
            outOfRangeTarget.ConnectToGroup(group);
            outOfRangeTarget.MoveByRadial(Angle.Right, 100);
            var distance = shooter.CalculateDistanceTo(outOfRangeTarget);

            var targeting = new Targeting
            {
                Options = new TargetingOptions
                {
                    Source = shooter,
                    Range = distance - CollisionDetector.VerySmallNumber // Just out of range
                }
            };

            targeting.Evaluate();
            Assert.IsNull(targeting.Target);

            outOfRangeTarget.MoveByRadial(Angle.Left, CollisionDetector.VerySmallNumber); // Just in range
            targeting.Evaluate();
            Assert.IsNotNull(targeting.Target);
        }
        finally
        {
            lt.Dispose();
        }
    }
}
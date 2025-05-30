using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.IO;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Gaming)]
public class CollisionDetectorTests
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void Initialize() => TestContextHelper.GlobalSetup();


    /// <summary>
    /// Positions two rectangles so they are very close to each other and then predicts a collision
    /// should the first rectangle move right by a little bit.
    /// </summary>
    [TestMethod]
    public void CollisionDetector_PredictHit()
    {
        var from = new RectF(0, 0, 1, 1);
        var colliders = new RectF[] { new RectF(1.001f, 0, 1, 1) };
        var prediction = CollisionPredictionPool.Instance.Rent();
        var visibility = .001f;
        try
        {
            
            CollisionDetector.Predict(from, Angle.Right, colliders, visibility, CastingMode.Precise, colliders.Length, prediction);
            Assert.AreEqual(true, prediction.CollisionPredicted);
        }
        finally
        {
            prediction.Dispose();
        }
    }

    [TestMethod]
    public void CollisionDetector_PredictMiss()
    {
        var from = new RectF(0, 0, 1, 1);
        var colliders = new RectF[] { new RectF(1.002f, 0, 1, 1) };
        var prediction = CollisionPredictionPool.Instance.Rent();
        var visibility = .001f;
        try
        {
            CollisionDetector.Predict(from, Angle.Right, colliders, visibility, CastingMode.Precise, colliders.Length, prediction);
            Assert.AreEqual(false, prediction.CollisionPredicted);
        }
        finally
        {
            prediction.Dispose();
        }
    }

    /// <summary>
    /// Tests a collision moving upward where an obstacle is directly above 'from'.
    /// </summary>
    [TestMethod]
    public void CollisionDetector_PredictUpCollision()
    {
        var from = new RectF(0, 10, 2, 2);
        var colliders = new RectF[] { new RectF(0, 8, 2, 2) };
        var prediction = CollisionPredictionPool.Instance.Rent();
        try
        {
            CollisionDetector.Predict(from, Angle.Up, colliders, 2f, CastingMode.Precise, colliders.Length, prediction);
            Assert.IsTrue(prediction.CollisionPredicted);
        }
        finally
        {
            prediction.Dispose();
        }
    }

    /// <summary>
    /// Tests a collision moving left with multiple obstacles, ensuring we detect the earliest collision.
    /// </summary>
    [TestMethod]
    public void CollisionDetector_PredictLeftCollision_MultipleObstacles()
    {
        var from = new RectF(10, 0, 2, 2);
        var colliders = new List<RectF>
            {
                new RectF(7, 0, 2, 2), // Closer obstacle
                new RectF(3, 0, 2, 2)  // Farther obstacle
            };
        var prediction = CollisionPredictionPool.Instance.Rent();
        try
        {
            CollisionDetector.Predict(from, Angle.Left, colliders, 10f, CastingMode.Precise, colliders.Count, prediction);
            Assert.IsTrue(prediction.CollisionPredicted);
            // The collision should be with the closer obstacle at X=7
            Assert.AreEqual(colliders[0], prediction.ColliderHit);
        }
        finally
        {
            prediction.Dispose();
        }
    }

    /// <summary>
    /// Tests that no collision is detected when obstacles are present but not in the path.
    /// </summary>
    [TestMethod]
    public void CollisionDetector_PredictNoCollision_MultipleObstacles()
    {
        var from = new RectF(0, 0, 1, 1);
        var colliders = new List<RectF>
            {
                new RectF(0, 2, 1, 1),
                new RectF(0, 4, 1, 1)
            };
        var prediction = CollisionPredictionPool.Instance.Rent();
        try
        {
            CollisionDetector.Predict(from, Angle.Right, colliders, 10f, CastingMode.Precise, colliders.Count, prediction);
            Assert.IsFalse(prediction.CollisionPredicted);
            Assert.IsNull(prediction.ColliderHit);
        }
        finally
        {
            prediction.Dispose();
        }
    }

    /// <summary>
    /// Tests different casting modes (Rough vs. Precise) on the same scenario and compares outcomes.
    /// </summary>
    [TestMethod]
    public void CollisionDetector_RoughVsPrecise()
    {
        var from = new RectF(0, 0, 2, 2);
        var colliders = new RectF[]
        {
                // This obstacle partially overlaps the edge path
                new RectF(2, 1, 2, 2)
        };

        var predictionRough = CollisionPredictionPool.Instance.Rent();
        var predictionPrecise = CollisionPredictionPool.Instance.Rent();

        try
        {
            // Rough cast
            CollisionDetector.Predict(from, Angle.Right, colliders, 2f, CastingMode.Rough, colliders.Length, predictionRough);
            // Precise cast
            CollisionDetector.Predict(from, Angle.Right, colliders, 2f, CastingMode.Precise, colliders.Length, predictionPrecise);

            // Depending on how "Rough" is implemented, it might detect collisions more eagerly
            // or might skip certain edge collisions. This test ensures they behave as expected.
            // Adjust as needed if your library's rough/precise modes produce known different outcomes.

            Assert.IsTrue(predictionRough.CollisionPredicted, "Rough cast should detect collision.");
            Assert.IsTrue(predictionPrecise.CollisionPredicted, "Precise cast should also detect collision.");
        }
        finally
        {
            predictionRough.Dispose();
            predictionPrecise.Dispose();
        }
    }

    /// <summary>
    /// Tests the HasLineOfSight extension method when there is a clear path.
    /// </summary>
    [TestMethod]
    public void CollisionDetector_HasLineOfSight_ClearPath()
    {
        var from = new RectF(0, 0, 2, 2);
        var to = new RectF(10, 0, 2, 2);
        var obstacles = new List<RectF>();
        Assert.IsTrue(from.HasLineOfSight(to, obstacles));
    }

    /// <summary>
    /// Tests the HasLineOfSight extension method when an obstacle is directly between from and to.
    /// </summary>
    [TestMethod]
    public void CollisionDetector_HasLineOfSight_Blocked()
    {
        var from = new RectF(0, 0, 2, 2);
        var to = new RectF(10, 0, 2, 2);
        var obstacles = new List<RectF>
            {
                // Obstacle spanning from x=4 to x=6 at y=0
                new RectF(4, 0, 2, 2)
            };
        Assert.IsFalse(from.HasLineOfSight(to, obstacles));
    }

    /// <summary>
    /// Tests GetLineOfSightObstruction, verifying we get the correct obstacle.
    /// </summary>
    [TestMethod]
    public void CollisionDetector_GetLineOfSightObstruction()
    {
        var from = new RectF(0, 0, 2, 2);
        var to = new RectF(10, 0, 2, 2);
        var obstacles = new List<RectF>
            {
                new RectF(3, 0, 2, 2),
                new RectF(6, 0, 2, 2)
            };

        var obstruction = from.GetLineOfSightObstruction(to, obstacles, CastingMode.Precise);
        Assert.IsNotNull(obstruction, "There should be an obstruction in the direct path.");
        Assert.AreEqual(obstacles[0], obstruction, "The first obstacle at x=3 is encountered first.");
    }

    /// <summary>
    /// Tests GetLineOfSightObstruction in a case where there's no actual block.
    /// </summary>
    [TestMethod]
    public void CollisionDetector_GetLineOfSightObstruction_NoBlock()
    {
        var from = new RectF(0, 0, 2, 2);
        var to = new RectF(10, 10, 2, 2);
        var obstacles = new List<RectF>
            {
                // Placed well away from the direct line (diagonal from (0,0) to (10,10))
                new RectF(5, 0, 2, 2),
                new RectF(0, 5, 2, 2)
            };

        var obstruction = from.GetLineOfSightObstruction(to, obstacles, CastingMode.Rough);
        Assert.IsNull(obstruction, "There should be no obstruction on the direct diagonal path.");
    }

    private string CurrentTestFYIRootPath => Path.Combine(UITestManager.GitRootPath, "CollisionDetectionOutput");

#if DEBUG
    [TestMethod]
    [TestCategory(Categories.Quarantined)]
    public void TestCloseMovementWithDebuggerSuccess()
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        { 
            var stopwatch = new ManualStopwatch();
            var group = new ColliderGroup(lt, stopwatch);
            ColliderGroupDebugger.TryInit(group, CurrentTestFYIRootPath, lt);

            var movingObject = new GameCollider(new RectF(0, 0, 1, 1), connectToMainColliderGroup: false);
            movingObject.ConnectToGroup(group);
            movingObject.Velocity.Angle = Angle.Right;
            movingObject.Velocity.Speed = 10;

            var obstacle = new GameCollider(new RectF(1.001f, 0, 1, 1), connectToMainColliderGroup: false);
            obstacle.ConnectToGroup(group);

            stopwatch.Elapsed+= TimeSpan.FromMilliseconds(50);
            group.Tick();
            Assert.IsTrue(movingObject.Left > 0);
            Assert.IsTrue(movingObject.Top == 0);
        }
        finally
        {
            lt.Dispose();
        }
    }

    [TestMethod]
    [TestCategory(Categories.Quarantined)]
    public void TestCollisionWithNotEnoughRoomToEncroach()
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            var stopwatch = new ManualStopwatch();
            var group = new ColliderGroup(lt, stopwatch);
            ColliderGroupDebugger.TryInit(group, CurrentTestFYIRootPath, lt);
            var failedEncroachDetected = false;
            var bounceMoveDetected = false;
            ColliderGroupDebugger.VelocityEventOccurred.Subscribe((ev) =>
            {
                if(failedEncroachDetected == false && ev is FailedMove == false)
                {
                    Assert.Fail("Expected a FailedMove event");
                }
                if(failedEncroachDetected == false)
                {
                    failedEncroachDetected = true;
                    return;
                }

                if (bounceMoveDetected == false && ev is SuccessfulMove == false)
                {
                    Assert.Fail("Expected a SuccessfulMove event");
                }
                if(bounceMoveDetected == false)
                {
                    bounceMoveDetected = true;
                    return;
                }

                Assert.Fail("Expected only two events");

            }, lt);
            var movingObject = new GameCollider(new RectF(0, 0, 1, 1), connectToMainColliderGroup: false);
            movingObject.ConnectToGroup(group);
            movingObject.Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.Bounce;
            movingObject.Velocity.Angle = Angle.Right;
            movingObject.Velocity.Speed = 10;

            var obstacle = new GameCollider(new RectF(1+CollisionDetector.VerySmallNumber/2f, 0, 1, 1), connectToMainColliderGroup: false);
            obstacle.ConnectToGroup(group);

            stopwatch.Elapsed += TimeSpan.FromMilliseconds(50);
            group.Tick();
            Assert.IsTrue(movingObject.Left == -.5); // moved to the left due to bounce
            Assert.IsTrue(movingObject.Top == 0); // didn't move up or down
            Assert.AreEqual(Angle.Left, movingObject.Velocity.Angle); // angle changed to left
            Assert.IsTrue(failedEncroachDetected);
            Assert.IsTrue(bounceMoveDetected);
            Assert.AreEqual(10, movingObject.Velocity.Speed);
        }
        finally
        {
            lt.Dispose();
        }
    }

    [TestMethod]
    [TestCategory(Categories.Quarantined)]
    public void TestCollisionWithEnoughRoomToEncroach()
    {
        var lt = DefaultRecyclablePool.Instance.Rent();
        try
        {
            var stopwatch = new ManualStopwatch();
            var group = new ColliderGroup(lt, stopwatch);
            ColliderGroupDebugger.TryInit(group, CurrentTestFYIRootPath, lt);

            var movingObject = new GameCollider(new RectF(0, 0, 1, 1), connectToMainColliderGroup: false);
            movingObject.ConnectToGroup(group);
            movingObject.Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.Bounce;
            movingObject.Velocity.Angle = Angle.Right;
            movingObject.Velocity.Speed = 10;

            var obstacle = new GameCollider(new RectF(1.1f, 0, 1, 1), connectToMainColliderGroup: false);
            obstacle.ConnectToGroup(group);

            stopwatch.Elapsed += TimeSpan.FromMilliseconds(50);
            group.Tick();
            Assert.IsTrue(movingObject.Left < 0); // Bounced to the left

            var currentDistance = movingObject.CalculateDistanceTo(obstacle.Bounds);
            Assert.IsTrue(movingObject.Top == 0); // didn't move up or down
            Assert.AreEqual(Angle.Left, movingObject.Velocity.Angle); // angle changed to left
            Assert.AreEqual(10, movingObject.Velocity.Speed);
        }
          finally
        {
            lt.Dispose();
        }
    }
#endif
}

public class ManualStopwatch : IStopwatch
{
    public bool SupportsMaxDT => true;
    public TimeSpan Elapsed { get; set; } = TimeSpan.Zero;
    public void Start() => throw new NotImplementedException();
    public void Stop() => throw new NotImplementedException();
}
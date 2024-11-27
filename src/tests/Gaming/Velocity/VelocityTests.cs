using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Linq;
using static klooie.tests.HitDetectionTests;
namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Gaming)]
public class VelocityTests
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
    public void Velocity_MinEvalTime() => GamingTest.Run(new GamingTestOptions()
    {
        Test = async (context) =>
        {
            var v = new GameCollider().Velocity;
            for (var s = 0; s < 200; s += 5)
            {
                v.Speed = s;
                Console.WriteLine($"Speed = {s}, EvalFrequency = {v.EvalFrequencySeconds} s");
            }
            Game.Current.Stop();
        },
        Mode = UITestMode.Headless,
    });

    [TestMethod]
    public void Velocity_BasicHorizontal()
    {
        using (var testLt = new Lifetime())
        {
            var stopwatch = new TestStopwatch();
            var group = new ColliderGroup(testLt, stopwatch);
            var c1 = new GameCollider(0, 0, 1, 1, group);
            c1.Velocity.Speed = 0;
            c1.Velocity.Angle = 0;
            AssertCloseEnough(0, c1.Left);
            AssertCloseEnough(0, c1.Top);

            stopwatch.Elapsed += TimeSpan.FromSeconds(1);
            group.Tick();
            AssertCloseEnough(0, c1.Left);
            AssertCloseEnough(0, c1.Top);

            c1.Velocity.Speed = 1;
            c1.Velocity.Angle = 0;

            stopwatch.Elapsed += TimeSpan.FromSeconds(1);
            group.Tick();
            AssertCloseEnough(1, c1.Left);
            AssertCloseEnough(0, c1.Top);
        }
    }

    [TestMethod]
    public void Velocity_BasicVertical()
    {
        using (var testLt = new Lifetime())
        {
            var stopwatch = new TestStopwatch();
            var group = new ColliderGroup(testLt, stopwatch);
            var c1 = new GameCollider(0, 0, 1, 1, group);
            c1.Velocity.Speed = 0;
            c1.Velocity.Angle = 0;
            AssertCloseEnough(0, c1.Left);
            AssertCloseEnough(0, c1.Top);

            stopwatch.Elapsed += TimeSpan.FromSeconds(1);
            group.Tick();
            AssertCloseEnough(0, c1.Left);
            AssertCloseEnough(0, c1.Top);

            c1.Velocity.Speed = 1;
            c1.Velocity.Angle = 90;

            stopwatch.Elapsed += TimeSpan.FromSeconds(1);
            group.Tick();
            AssertCloseEnough(0, c1.Left);
            AssertCloseEnough(.5f, c1.Top);
        }
    }

    [TestMethod]
    public void Velocity_Colliders()
    {
        using (var testLt = new Lifetime())
        {
            var sceneBounds = new RectF(0, 0, 500, 500);
            var r = new Random(420);
            var stopwatch = new TestStopwatch();
            var group = new ColliderGroup(testLt, stopwatch);

            var left = new GameCollider(group);
            var right = new GameCollider(group);
            var top = new GameCollider(group);
            var bottom = new GameCollider(group);

            left.MoveTo(sceneBounds.TopLeft);
            left.ResizeTo(2, sceneBounds.Height);

            right.MoveTo(sceneBounds.Right - 2, 0);
            right.ResizeTo(2, sceneBounds.Height);

            top.MoveTo(sceneBounds.TopLeft);
            top.ResizeTo(sceneBounds.Width, 1);

            bottom.MoveTo(0, sceneBounds.Bottom - 1);
            bottom.ResizeTo(sceneBounds.Width, 1);

            var totalChecks = 0;
            for (var i = 0; i < 50; i++)
            {
                var collider = new GameCollider(group);
                collider.MoveTo(r.Next((int)sceneBounds.Width / 2, (int)sceneBounds.Width - 5), r.Next((int)sceneBounds.Height / 2, (int)sceneBounds.Height - 5));
                if (collider.NudgeFree() == false) Assert.Fail("Failed to nudge");

                collider.Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.Bounce;
                collider.Velocity.Angle = r.Next(0, 360);
                collider.Velocity.Speed = r.Next(10, 50);

                var checkBounds = () =>
                {
                    totalChecks++;

                    var touching = group.EnumerateCollidersSlow(null).Where(c => c != collider && c.NumberOfPixelsThatOverlap(collider) > 0).ToArray();
                    if (touching.Any())
                    {
                        Assert.Fail("Colliders touching at time " + group.Now.TotalSeconds);
                    }

                    if (collider.NumberOfPixelsThatOverlap(left) > 0 || collider.NumberOfPixelsThatOverlap(top) > 0 || collider.NumberOfPixelsThatOverlap(right) > 0 || collider.NumberOfPixelsThatOverlap(bottom) > 0)
                    {
                        Assert.Fail("Collider overlapping wall at time " + group.Now.TotalSeconds);
                    }
                };

                collider.Velocity.BeforeMove.Subscribe(checkBounds, collider);
                collider.BoundsChanged.Sync(checkBounds, collider);
            }

            for(var i = 0; i < 10000; i++)
            {
                stopwatch.Elapsed += TimeSpan.FromMilliseconds(1);
                group.Tick();
            }
            Console.WriteLine(totalChecks);
       

            foreach(var collider in group.EnumerateCollidersSlow(null))
            {
                collider.Dispose();
            }
        }
    }
}

public class TestStopwatch : IStopwatch
{
    public TimeSpan Elapsed { get; set; }

    public bool SupportsMaxDT => false;
    public void Start() { }
    public void Stop() { }
}
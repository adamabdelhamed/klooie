﻿using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace klooie.tests;
/*
[TestClass]
[TestCategory(Categories.Slow)]
public class MovementTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void VelocityLifecycle() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI, 120, 5, async (context) =>
    {
        Game.Current.GamePanel.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(new GameCollider());
        await Game.Current.Delay(250);
        Game.Current.Stop();
    });

    [TestMethod]
    public void Movement_Basic() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI,120,5, async (context) =>
    {
        Game.Current.GamePanel.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(new GameCollider());
        cMover.Background = RGB.Red;
        cMover.ResizeTo(3, 1);
        cMover.MoveTo(2, 2);

        var vision = Vision.Create(cMover);

        var cStill = Game.Current.GamePanel.Add(new GameCollider());
        cStill.Background = RGB.Green;
        cStill.ResizeTo(3, 1);
        cStill.MoveTo(112, 2);

        var successLt = cMover.Velocity.OnCollision.CreateNextFireLifetime();
        var successLtLease = successLt.Lease;
        var lt = Recyclable.EarliestOf(Task.Delay(2000).ToLifetime(), successLt);
        var right = new Right();
        right.Bind(new MovementOptions()
        {
            Speed = () => 80,
            Velocity = cMover.Velocity,
            Vision = vision,
        });
        await Mover.InvokeOrTimeout(right, lt);
        var success = successLt.IsStillValid(successLtLease) == false;
        await Game.Current.Delay(250);
        Assert.IsTrue(success);
        Game.Current.Stop();
    });

    [TestMethod]
    public void Movement_TargetExpires() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI, 120, 5, async (context) =>
    {
        Game.Current.GamePanel.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(new GameCollider());
        cMover.Background = RGB.Red;
        cMover.ResizeTo(3, 1);
        cMover.MoveTo(2, 2);

        var vision = Vision.Create(cMover);

        Game.Current.Invoke(async () =>
        {
            await Game.Current.Delay(500);
            cMover.Dispose();
        });

        var right = new Right();
        right.Bind(new MovementOptions()
        {
            Speed = () => 80,
            Velocity = cMover.Velocity,
            Vision = vision,
        });
        await Mover.Invoke(right);
        Assert.IsTrue(cMover.IsStillValid(cMover.CurrentVersion)); // not rented
        await Game.Current.RequestPaintAsync();
        await Game.Current.Delay(500);
        Game.Current.Stop();
    });

    [TestMethod]
    public void Movement_WanderBasic() => GamingTest.Run(new GamingTestOptions()
    {
        TestId = TestContext.TestId(),
        Mode = UITestMode.RealTimeFYI,
        GameWidth = 120,
        GameHeight = 40,
        Test = async (context) =>
        {
            await WanderTest(20, 60_000, false, null, false);
            await Game.Current.RequestPaintAsync();
            await Game.Current.Delay(500);
            Game.Current.Stop();
        }
    });

    [TestMethod]
    public void Movement_WanderTight() => GamingTest.Run(new GamingTestOptions()
    {
        TestId = TestContext.TestId(),
        Mode = UITestMode.RealTimeFYI,
        GameWidth = 120,
        GameHeight = 40,
        Test = async (context) =>
        {
            await WanderTest(20, 5000, false, null, true);
            await Game.Current.RequestPaintAsync();
            await Game.Current.Delay(500);
            Game.Current.Stop();
        }
    });

    [TestMethod]
    public void Movement_WanderCamera()
    {
        var ev = Event<LocF>.Create();
        GameCollider c = null;
        var factory = () =>
        {
            c = new GameCollider();
            c.BoundsChanged.Sync(() => ev.Fire(c.Center()), c);
            return c;
        };
        GamingTest.Run(new GamingTestOptions()
        {
            TestId = TestContext.TestId(),
            Mode = UITestMode.RealTimeFYI,
            GameWidth = 120,
            GameHeight = 40,
            Camera = true,
            FocalPointChanged = ev,
            Test = async (context) =>
            {
                Game.Current.LayoutRoot.IsVisible = false;
                await WanderTest(20, 60_000, true, factory, false);
                await Game.Current.RequestPaintAsync();
                await Game.Current.Delay(500);
                Game.Current.Stop();
            }
        });
    }
    [TestMethod]
    public void Movement_WanderVariousSpeeds() => GamingTest.Run(new GamingTestOptions()
    {
        TestId = TestContext.TestId(),
        Mode = UITestMode.RealTimeFYI,
        GameWidth = 120,
        GameHeight = 40,
        Test = async(context)=>
        {
            for (var s = 5f; s < 200; s += 25)
            {
                await WanderTest(s,300, false, null, false);
            }
            await Game.Current.RequestPaintAsync();
            await Game.Current.Delay(500);
            Game.Current.Stop();
        }
    });
 
    public static async Task WanderTest(float speed, float duration, bool camera, Func<GameCollider> factory, bool extraTight)
    {
        Console.WriteLine("Speed: " + speed);

        Game.Current.LayoutRoot.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(factory != null ? factory() : new GameCollider());
        var cMoverLease = cMover.Lease;
        cMover.Background = RGB.Red;
        cMover.ResizeTo(3, 1);

        var vision = Vision.Create(cMover);

        if (extraTight)
        {
            AddTerrain(5f, 2, 1, vision);
        }
        else if (camera)
        {
            AddTerrain(15, 60, 30, vision);
        }
        else
        {
            AddTerrain(15, 6, 3, vision);
        }

        if (extraTight)
        {
            cMover.MoveTo(Game.Current.GameBounds.Left + 2, Game.Current.GameBounds.Top + 1) ;
        }
        else
        {
            cMover.MoveTo(Game.Current.Width / 2f, Game.Current.Height / 2f);
        }

        cMover.BoundsChanged.Subscribe(() =>
        {
            var buffer = ObstacleBufferPool.Instance.Rent();
            cMover.GetObstacles(buffer);
            var overlaps = buffer.ReadableBuffer
            .Where(o => o.OverlapPercentage(cMover) > 0).ToArray();
            buffer.Dispose();
            if (overlaps.Any())
            {
                Assert.Fail("overlaps detected");
            }
        }, cMover);
        Assert.IsTrue(cMover.NudgeFree(maxSearch: 50));
        cMover.Velocity.Angle = 0;
        var failed = false;
        var lastPosition = cMover.Center();
        Game.Current.Invoke(async () =>
        {
            var dueTime = Game.Current.MainColliderGroup.Now + TimeSpan.FromMilliseconds(duration);
            while (Game.Current.MainColliderGroup.Now < dueTime)
            {
                await Game.Current.Delay(1000);
                var newPosition = cMover.Center();
                var d = lastPosition.CalculateNormalizedDistanceTo(newPosition);

                failed = failed || d == 0; // we didn't get stuck
                if (extraTight) failed = false;
                Console.WriteLine(failed ? d + " fail" : d + " ok");
                lastPosition = newPosition;
            }
            cMover.Dispose();
        });
        await Game.Current.RequestPaintAsync();
        Game.Current.LayoutRoot.IsVisible = true;
        var wander = Wander.Create(new WanderOptions()
        {
            Speed = () => speed,
            Velocity = cMover.Velocity,
            Vision = vision,
        });
        var wanderEvaluator = new WanderEvaluator(wander);
        var wonderVisualizer = new WanderVisualizer(wander, -10);
        await Mover.Invoke(wander);
        Assert.IsTrue(cMover.IsStillValid(cMoverLease) == false);
        //Assert.IsFalse(failed, "Failed to keep moving");

        if(extraTight)
        {
            Assert.IsTrue(lastPosition.CalculateDistanceTo(Game.Current.GameBounds.Center) < 5, "Too far from center");
        }

        Game.Current.GamePanel.Controls.Clear();
        Console.WriteLine();
    }

    public static void AddTerrain(float spacing, float w, float h, Vision vision = null)
    {
        var bounds = Game.Current.GameBounds;

        var leftWall = Game.Current.GamePanel.Add(new OuterWall() { Background = RGB.White });
        leftWall.MoveTo(bounds.Left, bounds.Top);
        leftWall.ResizeTo(2, bounds.Height);
        leftWall.GiveWiggleRoom();
        if (vision != null) leftWall.Filters.Add(new VisionFilter(vision));
     
        var rightWall = Game.Current.GamePanel.Add(new OuterWall() { Background = RGB.White });
        rightWall.MoveTo(bounds.Right - 2, bounds.Top);
        rightWall.ResizeTo(2, bounds.Height);
        rightWall.GiveWiggleRoom();
        if (vision != null) rightWall.Filters.Add(new VisionFilter(vision));

        var topWall = Game.Current.GamePanel.Add(new OuterWall() { Background = RGB.White });
        topWall.MoveTo(bounds.Left, bounds.Top);
        topWall.ResizeTo(bounds.Width, 1);
        topWall.GiveWiggleRoom();
        if (vision != null) topWall.Filters.Add(new VisionFilter(vision));

        var bottonWall = Game.Current.GamePanel.Add(new OuterWall() { Background = RGB.White });
        bottonWall.MoveTo(bounds.Left, bounds.Bottom - 1);
        bottonWall.ResizeTo(Game.Current.GameBounds.Width, 1);
        bottonWall.GiveWiggleRoom();
        if (vision != null) bottonWall.Filters.Add(new VisionFilter(vision));

        var outerSpacing = Math.Max(5, spacing);
        for (var x = bounds.Left + outerSpacing; x < bounds.Right - outerSpacing; x += w + spacing)
        {
            for (var y = bounds.Top + outerSpacing / 2f; y < bounds.Bottom - outerSpacing / 2; y += h + (spacing / 2f))
            {
                if (new RectF(x, y, w, h).Center.CalculateDistanceTo(Game.Current.GameBounds.Center) > 5)
                {
                    var collider = Game.Current.GamePanel.Add(new Terrain());
                    collider.ResizeTo(w, h);
                    collider.MoveTo(x, y);
                    if (vision != null) collider.Filters.Add(new VisionFilter(vision));
                }
            }
        }
    }

    public class Terrain : GameCollider { }
    public class OuterWall : GameCollider { }

    public partial class Right : Movement
    {
        public void Bind(MovementOptions options)
        {
            base.Bind(options);
        }

        protected override async Task Move()
        {
            Options.Velocity.Angle = 0;
            Options.Velocity.Speed = Options.Speed();
            while(Options.Velocity != null)
            {
                await Task.Yield();
            }
        }
    }
}


*/
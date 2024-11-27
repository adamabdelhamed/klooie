using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Gaming)]
public class NavigateTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Navigate_Basic() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI,120,50, async (context) =>
    {
        Game.Current.GamePanel.Background = new RGB(20, 20, 20);
        await NavigateTest(50, false, null, false);
        Game.Current.Stop();
    });

    [TestMethod]
    public void Navigate_Tight()
    {
        for (var i = 0; i < 10; i++)
        {
            GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI, 120, 50, async (context) =>
            {
                Game.Current.GamePanel.Background = new RGB(20, 20, 20);
                await NavigateTest(50, false, null, true);
                Game.Current.Stop();
            });
            Console.WriteLine($"Iteration {i+1} succeeded");
        }
    }

    [TestMethod]
    public void Puppet_Basic() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI, 120, 50, async (context) =>
    {
        Game.Current.GamePanel.Background = new RGB(20, 20, 20);
        await PuppetTest(30, false, null, false);
        Game.Current.Stop();
    });

    [TestMethod]
    public void Navigate_Camera()
    {
        NavigateCamera(true);
    }

    public void NavigateCamera(bool headless)
    {
        var ev = new Event<LocF>();
        GameCollider c = null;
        var factory = () =>
        {
            c = new GameCollider();
            c.BoundsChanged.Sync(() => ev.Fire(c.Center()), c);
            return c;
        };
        GamingTest.Run(new GamingTestOptions()
        {
            Camera = true,
            FocalPointChanged = ev,
            Mode = headless ? UITestMode.RealTimeFYI : UITestMode.HeadOnly,
            TestId = TestContext.TestId(),
            Test = async (c) =>
            {
                Game.Current.GamePanel.Background = new RGB(20, 20, 20);
                await NavigateTest(250, true, factory, false);
                Game.Current.Stop();
            }
        });
    }

    [TestMethod]
    public void Navigate_Colliders() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI, 120, 50, async (context) =>
    {
        Game.Current.GamePanel.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(new GameCollider());
        cMover.Background = RGB.Red;
        cMover.ResizeTo(1, 1);
        cMover.MoveTo(Game.Current.GameBounds.Top + 4, Game.Current.GameBounds.Left + 2);
        Assert.IsTrue(cMover.NudgeFree(maxSearch: 50));
        var r = new Random(12);
        var left = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.Blue });
        var right = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.Blue });
        var top = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.Blue });
        var bottom = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.Blue });

        left.MoveTo(0, 0);
        left.ResizeTo(2, Game.Current.GameBounds.Height);

        right.MoveTo(Game.Current.GameBounds.Width - 2, 0);
        right.ResizeTo(2, Game.Current.GameBounds.Height);

        top.MoveTo(0, 0);
        top.ResizeTo(Game.Current.GameBounds.Width, 1);

        bottom.MoveTo(0, Game.Current.GameBounds.Bottom - 1);
        bottom.ResizeTo(Game.Current.GameBounds.Width, 1);

        for (var i = 0; i < 50; i++)
        {
            var collider = Game.Current.GamePanel.Add(new TextCollider("O".ToRed()) { CompositionMode = CompositionMode.BlendBackground });
            collider.MoveTo(r.Next(Game.Current.Width / 2, Game.Current.Width - 5),
                r.Next(Game.Current.Height / 2, Game.Current.Height - 5));
            if (collider.NudgeFree() == false) Assert.Fail("Failed to nudge");
            collider.Velocity.CollisionBehavior = Velocity.CollisionBehaviorMode.Bounce;
        }
        foreach(var collider in Game.Current.GamePanel.Controls.WhereAs<TextCollider>())
        {
            collider.BoundsChanged.Sync(() =>
            {
                if (collider.OverlapPercentage(left) > 0 || collider.OverlapPercentage(top) > 0 || collider.OverlapPercentage(right) > 0 || collider.OverlapPercentage(bottom) > 0)
                {
                    Assert.Fail();
                }
            }, collider);
            collider.Velocity.Angle = r.Next(0, 360);
            collider.Velocity.Speed = r.Next(10, 50);
        }

        await Game.Current.RequestPaintAsync();
        Game.Current.LayoutRoot.IsVisible = true;
        bool success = await Mover.InvokeOrTimeout(Navigate.Create(cMover.Velocity, () => 25, () => new GameCollider(Game.Current.GameBounds.BottomRight.Offset(-(4 + cMover.Width), -(2 + cMover.Height)).ToRect(cMover.Width, cMover.Height)), new NavigateOptions()
        {
            Show = true
        }), Task.Delay(10000).ToLifetime());
        Assert.IsTrue(success);
        Game.Current.Stop();
    });
    [TestMethod]
    public void Puppet_Camera()
    {
        var ev = new Event<LocF>();
        GameCollider c = null;
        var factory = () =>
        {
            c = new GameCollider();
            c.BoundsChanged.Sync(() => ev.Fire(c.Center()), c);
            return c;
        };
        GamingTest.Run(new GamingTestOptions()
        {
            Camera = true,
            FocalPointChanged = ev,
            Mode = UITestMode.RealTimeFYI,
            TestId = TestContext.TestId(),
            Test = async (c) =>
            {
                Game.Current.GamePanel.Background = new RGB(20, 20, 20);
                await PuppetTest(100, true, factory, false);
                Game.Current.Stop();
            }
        });
    }

    public Task PuppetTest(float speed, bool camera, Func<GameCollider> factory, bool extraTight)
    {
        return NavOrPuppetTest(false, speed, camera, factory, extraTight);
    }

    public Task NavigateTest(float speed, bool camera, Func<GameCollider> factory, bool extraTight)
    {
        return NavOrPuppetTest(true, speed, camera, factory, extraTight);
    }

    private static float NowDisplay => ConsoleMath.Round(Game.Current.MainColliderGroup.Now.TotalSeconds, 2);

    public async Task NavOrPuppetTest(bool nav, float speed, bool camera, Func<GameCollider> factory, bool extraTight)
    {
        bool success = false;
        if (extraTight)
        {
            MovementTests.AddTerrain(2.1f, 2, 1);
        }
        else if (camera)
        {
            MovementTests.AddTerrain(15, 60, 30);
        }
        else
        {
            MovementTests.AddTerrain(8, 10, 5);
        }
        Game.Current.LayoutRoot.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(factory != null ? factory() : new GameCollider());
        cMover.Background = RGB.Red;
        cMover.ResizeTo(.8f, .8f);
        cMover.MoveTo(Game.Current.GameBounds.Top + 2.5f, Game.Current.GameBounds.Left + 1.5f);
        cMover.MoveTo(cMover.X + .1f, cMover.Y + .1f);
        Assert.IsTrue(cMover.NudgeFree(maxSearch: 50));

        var path = new List<RectF>() { cMover.Bounds };
        cMover.BoundsChanged.Subscribe(() =>
         {
             if (success) return;
             path.Add(cMover.Bounds);
             var overlaps = cMover.GetOverlappingObstacles().ToArray();
             if (overlaps.Any())
             {
                 Assert.Fail($"{NowDisplay}: Overlap decected, cMoverBounds = {cMover.Bounds}, First overlapping object is a {overlaps.First().GetType().Name} at bounds {overlaps.First().Bounds}");
             }

             var touchingButNotOverlapping = cMover.GetObstacles()
             .Where(o => o.CalculateDistanceTo(cMover) == 0).ToArray();
             if(touchingButNotOverlapping.Any())
             {
                 Assert.Fail($"{NowDisplay}: Touching decected, cMoverBounds = {cMover.Bounds}, First overlapping object is a {touchingButNotOverlapping.First().GetType().Name} at bounds {touchingButNotOverlapping.First().Bounds}");
             }

         }, cMover);

        await Game.Current.RequestPaintAsync();
        Game.Current.LayoutRoot.IsVisible = true;
       
        var goal = extraTight ? Game.Current.GameBounds.Center.ToRect(1, 1) : Game.Current.GameBounds.BottomRight.Offset(-(4 + cMover.Width), -(2 + cMover.Height)).ToRect(cMover.Width, cMover.Height);

        Game.Current.Invoke(async () =>
        {
            while (path.None()) await Task.Delay(10);
            await Task.Delay(500);
            var lastIndex = path.Count - 1;
            var lastBounds = path.Last();
            var lastNow = Game.Current.MainColliderGroup.Now;
            while (cMover.ShouldContinue)
            {
                while (Game.Current.MainColliderGroup.Now - lastNow < TimeSpan.FromSeconds(2))
                {
                    await Task.Delay(10);
                }
                if (success) break;
                lastNow = Game.Current.MainColliderGroup.Now;

                var movesSinceLastCheck = path.Skip(lastIndex).ToArray();
                if (movesSinceLastCheck.Length < 2)  Assert.Fail($"{NowDisplay}: We appeared to get stuck");
                var traveled = 0f;
                for(var i = 0; i < movesSinceLastCheck.Length-1; i++)
                {
                    var d = movesSinceLastCheck[i].CalculateDistanceTo(movesSinceLastCheck[i + 1]);
                    traveled += d;
                }

                if(traveled == 0)
                {
                    Assert.Fail($"{NowDisplay}: cMover didn't move in the last 2 seconds");
                }

                lastIndex = path.Count - 1;
            }
        });
        
        if (nav)
        {
            success = await Mover.InvokeOrTimeout(Navigate.Create(cMover.Velocity, () => speed, ()=> new ColliderBox(goal), new NavigateOptions()
            {
                CloseEnough = 5,
                Show = true
            }), Task.Delay(camera ? 25000 : 10000).ToLifetime());
        }
        else
        {
            
            success = await Mover.InvokeOrTimeout(Puppet.Create(cMover.Velocity, () => speed, goal), Task.Delay(camera ? 60000 : 10000).ToLifetime());
        }
        Assert.IsTrue(success, $"{NowDisplay}:Failed to reach target");
        await Task.Delay(250);
        await Game.Current.RequestPaintAsync();
        Game.Current.GamePanel.Controls.Clear();
        await Game.Current.RequestPaintAsync();
        Console.WriteLine();
    }

 
    public class Right : Movement
    {
        public Right(Velocity v, SpeedEval innerSpeedEval) : base(v, innerSpeedEval) { }
        protected override async Task Move()
        {
            Velocity.Angle = 0;
            Velocity.Speed = Speed();
            while(ShouldContinue)
            {
                await YieldAsync();
            }
        }
    }
}


using klooie.Gaming;
using klooie.Gaming.Code;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Linq;
using System.Reflection;
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
        await NavigateTest(50, false);
        Game.Current.Stop();
    });

    [TestMethod]
    public void Puppet_Basic() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI, 120, 50, async (context) =>
    {
        Game.Current.GamePanel.Background = new RGB(20, 20, 20);
        await PuppetTest(30, false);
        Game.Current.Stop();
    });

    [TestMethod]
    public void Navigate_Camera()
    {
        var ev = new Event<LocF>();
        Character c = null;
        var factory = () =>
        {
            c = new Character();
            c.Sync(nameof(c.Bounds), () => ev.Fire(c.Center()), c);
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
                await NavigateTest(150, true, factory);
                Game.Current.Stop();
            }
        });
    }

    [TestMethod]
    public void Navigate_Colliders() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI, 120, 50, async (context) =>
    {
        Game.Current.GamePanel.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(new Character());
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
            collider.Sync(nameof(collider.Bounds), () =>
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
        Character c = null;
        var factory = () =>
        {
            c = new Character();
            c.Sync(nameof(c.Bounds), () => ev.Fire(c.Center()), c);
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
                await PuppetTest(100, true, factory);
                Game.Current.Stop();
            }
        });
    }

    public Task PuppetTest(float speed, bool camera, Func<Character> factory = null)
    {
        return NavOrPuppetTest(false, speed, camera, factory);
    }

    public Task NavigateTest(float speed, bool camera, Func<Character> factory = null)
    {
        return NavOrPuppetTest(true, speed, camera, factory);
    }

    public async Task NavOrPuppetTest(bool nav, float speed, bool camera, Func<Character> factory = null)
    {
        if (camera)
        {
            AddTerrain(15, 60, 30);
        }
        else
        {
            AddTerrain(8, 10, 5);
        }
        Game.Current.LayoutRoot.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(factory != null ? factory() : new Character());
        cMover.Background = RGB.Red;
        cMover.ResizeTo(1, 1);
        cMover.MoveTo(Game.Current.GameBounds.Top + 4, Game.Current.GameBounds.Left + 2);
        Assert.IsTrue(cMover.NudgeFree(maxSearch: 50));

        await Game.Current.RequestPaintAsync();
        Game.Current.LayoutRoot.IsVisible = true;
        bool success;
        if (nav)
        {
            success = await Mover.InvokeOrTimeout(Navigate.Create(cMover.Velocity, () => speed, () => new GameCollider(Game.Current.GameBounds.BottomRight.Offset(-(4 + cMover.Width), -(2 + cMover.Height)).ToRect(cMover.Width, cMover.Height)), new NavigateOptions()
            {
                Show = true
            }), Task.Delay(camera ? 60000 : 10000).ToLifetime());
        }
        else
        {
            success = await Mover.InvokeOrTimeout(Puppet.Create(cMover.Velocity, () => speed, Game.Current.GameBounds.BottomRight.Offset(-(4 + cMover.Width), -(2 + cMover.Height)).ToRect(cMover.Width, cMover.Height)), Task.Delay(camera ? 60000 : 10000).ToLifetime());
        }
        Assert.IsTrue(success);
        await Task.Delay(250);
        await Game.Current.RequestPaintAsync();
        Game.Current.GamePanel.Controls.Clear();
        await Game.Current.RequestPaintAsync();
        Console.WriteLine();
    }

    private static void AddTerrain(float spacing, float w, float h)
    {
        var bounds = Game.Current.GameBounds;

        var leftWall = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.White });
        leftWall.MoveTo(bounds.Left, bounds.Top);
        leftWall.ResizeTo(2, bounds.Height);
        leftWall.GiveWiggleRoom();

        var rightWall = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.White });
        rightWall.MoveTo(bounds.Right - 2, bounds.Top);
        rightWall.ResizeTo(2, bounds.Height);
        rightWall.GiveWiggleRoom();

        var topWall = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.White });
        topWall.MoveTo(bounds.Left, bounds.Top);
        topWall.ResizeTo(bounds.Width, 1);
        topWall.GiveWiggleRoom();

        var bottonWall = Game.Current.GamePanel.Add(new GameCollider() { Background = RGB.White });
        bottonWall.MoveTo(bounds.Left, bounds.Bottom - 1);
        bottonWall.ResizeTo(Game.Current.GameBounds.Width, 1);
        bottonWall.GiveWiggleRoom();

        for (var x = bounds.Left + spacing; x < bounds.Right - (spacing+w); x += w + spacing)
        {
            for (var y = bounds.Top + spacing / 2f; y < bounds.Bottom - (spacing+h) / 2; y += h + (spacing / 2f))
            {
                var collider = Game.Current.GamePanel.Add(new GameCollider());
                collider.ResizeTo(w, h);
                collider.MoveTo(x, y);
                collider.Background = RGB.DarkGreen;
            }
        }
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


﻿using klooie.Gaming;
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
public class MovementTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Movement_Basic() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI,120,5, async (context) =>
    {
        Game.Current.GamePanel.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(new Character());
        cMover.Background = RGB.Red;
        cMover.ResizeTo(3, 1);
        cMover.MoveTo(2, 2);

        var cStill = Game.Current.GamePanel.Add(new Character());
        cStill.Background = RGB.Green;
        cStill.ResizeTo(3, 1);
        cStill.MoveTo(112, 2);

        var successLt = cMover.Velocity.ImpactOccurred.CreateNextFireLifetime();
        var lt = Lifetime.EarliestOf(Task.Delay(2000).ToLifetime(), successLt);
        await Mover.InvokeOrTimeout(new Right(cMover.Velocity, () => 80), lt);
        var success = successLt.IsExpired;
        await Game.Current.Delay(250);
        Assert.IsTrue(success);
        Game.Current.Stop();
    });

    [TestMethod]
    public void Movement_TargetExpires() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.RealTimeFYI, 120, 5, async (context) =>
    {
        Game.Current.GamePanel.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(new Character());
        cMover.Background = RGB.Red;
        cMover.ResizeTo(3, 1);
        cMover.MoveTo(2, 2);
        Game.Current.Invoke(async () =>
        {
            await Game.Current.Delay(500);
            cMover.Dispose();
        });

        await Mover.InvokeWithShortCircuit(new Right(cMover.Velocity, () => 80));
        Assert.IsTrue(cMover.IsExpired);
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
            await WanderTest(20, 5000, false);
            await Game.Current.RequestPaintAsync();
            await Game.Current.Delay(500);
            Game.Current.Stop();
        }
    });

    [TestMethod]
    public void Movement_WanderCamera() => GamingTest.Run(new GamingTestOptions()
    {
        TestId = TestContext.TestId(),
        Mode = UITestMode.RealTimeFYI,
        GameWidth = 120,
        GameHeight = 40,
        Camera = true,
        CameraFocalPoint = ()=> Game.Current.GamePanel.Children.WhereAs<Character>().SingleOrDefault()?.Center(),
        Test = async (context) =>
        {
            Game.Current.LayoutRoot.IsVisible = false;
            await WanderTest(20, 8000, true);
            await Game.Current.RequestPaintAsync();
            await Game.Current.Delay(500);
            Game.Current.Stop();
        }
    });

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
                await WanderTest(s,300, false);
            }
            await Game.Current.RequestPaintAsync();
            await Game.Current.Delay(500);
            Game.Current.Stop();
        }
    });
 
    private async Task WanderTest(float speed, float duration, bool camera)
    {
        Console.WriteLine("Speed: " + speed);
        if (camera)
        {
            AddTerrain(15, 60, 30);
        }
        else
        {
            AddTerrain(15, 6, 3);
        }
        Game.Current.LayoutRoot.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(new Character());
        cMover.Background = RGB.Red;
        cMover.ResizeTo(3, 1);
        cMover.MoveTo(Game.Current.Width / 2f, Game.Current.Height / 2f);
        Assert.IsTrue(cMover.NudgeFree(maxSearch: 50));
        cMover.Velocity.Angle = 45;
        var failed = false;
        Game.Current.Invoke(async () =>
        {
            var dueTime = Game.Current.MainColliderGroup.Now + TimeSpan.FromMilliseconds(duration);
            var lastPosition = cMover.Center();
            while (Game.Current.MainColliderGroup.Now < dueTime)
            {
                await Game.Current.Delay(1000);
                var newPosition = cMover.Center();
                var d = lastPosition.CalculateNormalizedDistanceTo(newPosition);

                failed = failed || d < speed * .25f; // we didn't get stuck
                Console.WriteLine(failed ? d + " fail" : d + " ok");
                lastPosition = newPosition;
            }
            cMover.Dispose();
        });
        await Game.Current.RequestPaintAsync();
        Game.Current.LayoutRoot.IsVisible = true;
        await Mover.InvokeWithShortCircuit(Wander.Create(cMover.Velocity, () => speed, new WanderOptions()));
        Assert.IsTrue(cMover.IsExpired);
        Assert.IsFalse(failed);
        Game.Current.GamePanel.Controls.Clear();
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

        for (var x = bounds.Left + spacing; x < bounds.Right - spacing; x += w + spacing)
        {
            for (var y = bounds.Top + spacing / 2f; y < bounds.Bottom - spacing / 2; y += h + (spacing / 2f))
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

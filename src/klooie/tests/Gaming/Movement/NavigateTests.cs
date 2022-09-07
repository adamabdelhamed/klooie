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

    private async Task NavigateTest(float speed, bool camera)
    {
        Console.WriteLine("Speed: " + speed);
        if (camera)
        {
            AddTerrain(15, 60, 30);
        }
        else
        {
            AddTerrain(8, 10, 5);
        }
        Game.Current.LayoutRoot.Background = new RGB(20, 20, 20);
        var cMover = Game.Current.GamePanel.Add(new Character());
        cMover.Background = RGB.Red;
        cMover.ResizeTo(3, 1);
        cMover.MoveTo(Game.Current.GameBounds.Top+4,Game.Current.GameBounds.Left+2);
        Assert.IsTrue(cMover.NudgeFree(maxSearch: 50));
        cMover.Velocity.ImpactOccurred.SubscribeOnce((impact) =>
        {
           // Assert.Fail($"Collision: "+ impact);
        });

        await Game.Current.RequestPaintAsync();
        Game.Current.LayoutRoot.IsVisible = true;
        var success = await Mover.InvokeOrTimeout(Navigate.Create(cMover.Velocity, () => speed, () => new ColliderBox(Game.Current.GameBounds.BottomRight.Offset(-(4+cMover.Width), -(2+cMover.Height)).ToRect(cMover.Width, cMover.Height)), new NavigateOptions()
        {
            Show = true
        }), Task.Delay(10000).ToLifetime());
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


using klooie;
using klooie.Gaming;
using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace tests.Gaming.Movement;
[TestClass]
[TestCategory(Categories.Gaming)]
public class VisionTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [TestCategory(Categories.Quarantined)]
    public void VisionTestBasic() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified, 60, 30, async (context) =>
    {
        await SetupVisionTest(context, new RectF(30, 15, 2, 1), false);
        Game.Current.Stop();
    });

    [TestMethod]
    public void VisionTestPerfect() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified, 60, 30, async (context) =>
    {
        await SetupVisionTest(context, new RectF(30, 15, 2, 1), true);
        Game.Current.Stop();
    });

    [TestMethod]
    public void VisionPerfectScan_IgnoresOffAxisCrowdsButKeepsDirectBlockers() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.Headless, 80, 40, async (context) =>
    {
        var mover = Game.Current.GamePanel.Add(GameColliderPool.Instance.Rent());
        mover.MoveTo(10, 10, int.MaxValue);
        mover.ResizeTo(2, 2);
        mover.Velocity.Angle = Angle.Right;

        var scheduler = FrameTaskScheduler.Create(TimeSpan.FromSeconds(1));
        var vision = Vision.Create(scheduler, mover, autoScan: false);
        vision.AngleStep = 1;
        vision.AngleFuzz = 0;
        vision.MaxMemoryTime = TimeSpan.Zero;
        vision.Visibility = 30;
        vision.AngularVisibility = 90;

        var blockedTarget = Game.Current.GamePanel.Add(GameColliderPool.Instance.Rent());
        blockedTarget.MoveTo(26, 10, int.MaxValue);
        blockedTarget.ResizeTo(2, 2);

        var directBlocker = Game.Current.GamePanel.Add(GameColliderPool.Instance.Rent());
        directBlocker.MoveTo(18, 10, int.MaxValue);
        directBlocker.ResizeTo(2, 2);

        var visibleTarget = Game.Current.GamePanel.Add(GameColliderPool.Instance.Rent());
        visibleTarget.MoveTo(24, 16, int.MaxValue);
        visibleTarget.ResizeTo(2, 2);

        for (var i = 0; i < 40; i++)
        {
            var noise = Game.Current.GamePanel.Add(GameColliderPool.Instance.Rent());
            noise.MoveTo(16 + (i % 10) * 2, 20 + (i / 10) * 2, int.MaxValue);
            noise.ResizeTo(2, 2);
        }

        vision.Scan();

        Assert.IsFalse(vision.TryGetValue(blockedTarget, out _), "The direct blocker should still occlude the target.");
        Assert.IsTrue(vision.TryGetValue(visibleTarget, out _), "Off-axis crowding should not prevent the side target from being seen.");
        Game.Current.Stop();
    });

    private static async Task SetupVisionTest(UITestManager context, RectF moverPosition, bool perfect)
    {
        var mover = Game.Current.GamePanel.Add(GameColliderPool.Instance.Rent());
        var scheduler = FrameTaskScheduler.Create(TimeSpan.FromSeconds(1));
        var vision = Vision.Create(scheduler, mover, autoScan: false);
        if(perfect)
        {
            vision.AngleStep = 1;
            vision.MaxMemoryTime = TimeSpan.FromSeconds(0);
        }
        else
        {
            vision.AngleFuzz = 0;// remove randomness for testing
        }
        vision.Visibility = 15;
   
        var visionFilter = new VisionFilter(vision);
        mover.MoveTo(moverPosition.Left, moverPosition.Top, int.MaxValue);
        mover.ResizeTo(moverPosition.Width, moverPosition.Height);
        mover.Background = RGB.Magenta;

        foreach (var angle in new Angle[] {0,45,90, 135, 180, 225, 270, 315 })
        {
            var obstaclePosition = mover.Center().RadialOffset(angle, vision.Visibility * .9f).ToRect(6,3);
            var obstacle = Game.Current.GamePanel.Add(GameColliderPool.Instance.Rent());
            obstacle.MoveTo(obstaclePosition.Left, obstaclePosition.Top, int.MaxValue);
            obstacle.ResizeTo(obstaclePosition.Width, obstaclePosition.Height);
            obstacle.Filters.Add(visionFilter);
        }

        await context.PaintAndRecordKeyFrameAsync();

        foreach(var angle in new Angle[] { 0, 45, 90, 135, 180, 225, 270, 315 })
        {
            mover.Velocity.Angle = angle;
       
            vision.TrackedObjectsList.Clear();
            vision.Scan();
            var coneLifetime = DefaultRecyclablePool.Instance.Rent();

            var lineStart = mover.Center();
            var leftLineEnd = lineStart.RadialOffset(vision.FieldOfViewStart, vision.Visibility);
            var rightLineEnd = lineStart.RadialOffset(vision.FieldOfViewEnd, vision.Visibility);
            DrawLine(lineStart, leftLineEnd, RGB.Yellow, -1, coneLifetime);
            DrawLine(lineStart, rightLineEnd, RGB.Orange, -1, coneLifetime);

            await context.PaintAndRecordKeyFrameAsync();
            coneLifetime.Dispose();
        }
    }

    private static void DrawLine(LocF lineStart, LocF lineEnd, RGB lineColor, int z, ILifetime lt)
    {
        HashSet<Loc> seen = new HashSet<Loc>();
        var bufferLendth = ConsoleBitmap.DefineLineBuffered(ConsoleMath.Round(lineStart.Left), ConsoleMath.Round(lineStart.Top), ConsoleMath.Round(lineEnd.Left), ConsoleMath.Round(lineEnd.Top));
        for (var i = 0; i < bufferLendth; i++)
        {
            var point = ConsoleBitmap.LineBuffer[i];
            if (seen.Contains(point)) continue;
            seen.Add(point);
            var lineObject = Game.Current.GamePanel.Add(new ConsoleStringRenderer("o".ToConsoleString(lineColor)) { ZIndex = z, Foreground = lineColor, Bounds = new RectF(point.Left, point.Top, 1, 1) });
            lt.OnDisposed(lineObject, Recyclable.TryDisposeMe);
        }
    }
}


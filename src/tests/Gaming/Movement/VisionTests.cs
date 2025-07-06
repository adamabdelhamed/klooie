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
        vision.Range = 15;
   
        var visionFilter = new VisionFilter(vision);
        mover.MoveTo(moverPosition.Left, moverPosition.Top, int.MaxValue);
        mover.ResizeTo(moverPosition.Width, moverPosition.Height);
        mover.Background = RGB.Magenta;

        foreach (var angle in new Angle[] {0,45,90, 135, 180, 225, 270, 315 })
        {
            var obstaclePosition = mover.Center().RadialOffset(angle, vision.Range * .9f).ToRect(6,3);
            var obstacle = Game.Current.GamePanel.Add(GameColliderPool.Instance.Rent());
            obstacle.MoveTo(obstaclePosition.Left, obstaclePosition.Top, int.MaxValue);
            obstacle.ResizeTo(obstaclePosition.Width, obstaclePosition.Height);
            obstacle.Filters.Add(visionFilter);
        }

        await context.PaintAndRecordKeyFrameAsync();

        foreach(var angle in new Angle[] { 0, 45, 90, 135, 180, 225, 270, 315 })
        {
            mover.Velocity.Angle = angle;
            vision.TrackedObjectsDictionary.Clear();
            vision.TrackedObjectsList.Clear();
            vision.Scan();
            var coneLifetime = DefaultRecyclablePool.Instance.Rent();

            var lineStart = mover.Center();
            var leftLineEnd = lineStart.RadialOffset(vision.FieldOfViewStart, vision.Range);
            var rightLineEnd = lineStart.RadialOffset(vision.FieldOfViewEnd, vision.Range);
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


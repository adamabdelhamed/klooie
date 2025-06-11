using klooie;
using klooie.Gaming;
using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tests.Gaming.Movement;

[TestClass]
[TestCategory(Categories.Gaming)]
public class WanderLogicTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void WanderLogicBasic() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesFYI, 90, 30, async (context) =>
    {
        // World is a Rect with Bounds (0, 0, Game.Current.LayoutRoot.Width, Game.Current.LayoutRoot.Height) with width and height configured above.
        await SetupWanderLogicTest(context, new RectF(5, 20, 2, 1), Angle.Right, null, new RectF(10, 20, 2, 1));
        Game.Current.Stop();
    });

    [TestMethod]
    public void WanderLogicHeadingIntoLargeWall() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesFYI, 90, 30, async (context) =>
    {
        // World is a Rect with Bounds (0, 0, Game.Current.LayoutRoot.Width, Game.Current.LayoutRoot.Height) with width and height configured above.
        await SetupWanderLogicTest(context, new RectF(5, 20, 2, 1), Angle.Right, null, new RectF(18, -200, 2, 300));
        Game.Current.Stop();
    });

    private static async Task SetupWanderLogicTest(UITestManager context, RectF moverPosition, Angle moverAngle, RectF? curiosityPoint, params RectF[] obstaclePositions)
    {
        Game.Current.GamePanel.Background = new RGB(50, 50, 50);
        var mover = AddMoverAndObstacles(moverPosition, curiosityPoint, obstaclePositions);
        var vision = Vision.Create(mover, autoScan: false);
        var targeting = TargetingPool.Instance.Rent();
        targeting.Bind(new TargetingOptions() { Source = mover, Vision = vision });
        vision.Range = 15;
        vision.Scan();
        var visionFilter = new VisionFilter(vision);
        Game.Current.GamePanel.Controls.Where(c => c != mover).ForEach(c => c.Filters.Add(visionFilter));

        await context.PaintAndRecordKeyFrameAsync();

        var state = WanderMovementState.Create(targeting, (s) => curiosityPoint, () => 1);
        mover.Velocity.Speed = 20;
        var scores = WanderLogic.AdjustSpeedAndVelocity(state);
        mover.Velocity.Speed = 0;
        var maxScore = scores.Items.Max(s => s.Total);

        int z = -1;
        foreach (var score in scores.Items)
        {
            var seen = new HashSet<Loc>();
            var isMax = score.Total == maxScore;
            var lineColor = ColorForScore(score.Total, isMax);
            var lineStart = moverPosition.TopLeft;
            var lineEnd = moverPosition.TopLeft.RadialOffset(score.Angle, vision.Range);
            var bufferLength = ConsoleBitmap.DefineLineBuffered(
                ConsoleMath.Round(lineStart.Left), ConsoleMath.Round(lineStart.Top),
                ConsoleMath.Round(lineEnd.Left), ConsoleMath.Round(lineEnd.Top));
            for (var i = 0; i < bufferLength; i++)
            {
                var point = ConsoleBitmap.LineBuffer[i];
                if (seen.Contains(point)) continue;
                seen.Add(point);
                Game.Current.GamePanel.Add(new ConsoleStringRenderer("o".ToConsoleString(lineColor))
                {
                    ZIndex = isMax ? 10 : z,
                    Foreground = lineColor,
                    Bounds = new RectF(point.Left, point.Top, 1, 1)
                });
            }
            z--;

            // Centralize label coloring (use total for all, green for max)
            RGB LabelColor(float val) => RGB.White.ToOther(RGB.Green, val);

            var stack = Game.Current.GamePanel.Add(new StackPanel() {   AutoSize = StackPanel.AutoSizeMode.Both }).DockToBottom().DockToRight();
            stack.Add(new ConsoleStringRenderer($"Angle: {score.Angle} degrees".ToConsoleString(LabelColor(score.Total))));
            stack.Add(new ConsoleStringRenderer($"Collision Score: {score.Collision}    Weight: {score.Weights.CollisionWeight}".ToConsoleString(LabelColor(score.Collision))));
            stack.Add(new ConsoleStringRenderer($"Forward Score: {score.Forward}    Weight: {score.Weights.ForwardWeight}".ToConsoleString(LabelColor(score.Forward))));
            if (score.Weights.InertiaWeight > 0)
            {
                stack.Add(new ConsoleStringRenderer($"Inertia Score: {score.Inertia}    Weight: {score.Weights.InertiaWeight}".ToConsoleString(LabelColor(score.Inertia))));
            }
            if(score.Weights.CuriosityWeight > 0)
            {
                stack.Add(new ConsoleStringRenderer($"Curiosity Point: {curiosityPoint?.ToString() ?? "None"}".ToConsoleString(LabelColor(score.Curiosity))));
            }
            stack.Add(new ConsoleStringRenderer($"Total Score: {score.Total}".ToConsoleString(lineColor))); // Use the same color as the line for visual tie-in
            await context.PaintAndRecordKeyFrameAsync();
            stack.Dispose();
        }
    }

    private static RGB ColorForScore(float score, bool isMax)
    {
        if (isMax) return RGB.Green;

        var neutral = new RGB(128, 128, 128);
        if (score <= 0.5f)
        {
            // Interpolate from Red (low) to Gray (neutral)
            // t = score / 0.5, so t goes from 0 (red) to 1 (neutral)
            float t = score / 0.5f;
            return InterpolateColor(RGB.Red, neutral, t);
        }
        else
        {
            // Interpolate from Gray (neutral) to Green (high)
            // t = (score - 0.5) / 0.5, so t goes from 0 (neutral) to 1 (green)
            float t = (score - 0.5f) / 0.5f;
            return InterpolateColor(neutral, RGB.Green, t);
        }
    }

    // Simple linear color interpolation between two RGBs
    private static RGB InterpolateColor(RGB from, RGB to, float t)
    {
        byte r = (byte)Math.Round(from.R + (to.R - from.R) * t);
        byte g = (byte)Math.Round(from.G + (to.G - from.G) * t);
        byte b = (byte)Math.Round(from.B + (to.B - from.B) * t);
        return new RGB(r, g, b);
    }


    private static GameCollider AddMoverAndObstacles(RectF moverPosition, RectF? curiosityPoint, RectF[] obstaclePositions)
    {
        var mover = Game.Current.GamePanel.Add(GameColliderPool.Instance.Rent());
        mover.MoveTo(moverPosition.Left, moverPosition.Top, int.MaxValue);
        mover.ResizeTo(moverPosition.Width, moverPosition.Height);
        mover.Background = RGB.Magenta;

        if(curiosityPoint.HasValue)
        {
            Game.Current.GamePanel.Add(new ConsoleControl() { Bounds = curiosityPoint.Value, Background = RGB.Green });
        }

        foreach (var obstaclePosition in obstaclePositions)
        {
            var obstacle = Game.Current.GamePanel.Add(GameColliderPool.Instance.Rent());
            obstacle.MoveTo(obstaclePosition.Left, obstaclePosition.Top, int.MaxValue);
            obstacle.ResizeTo(obstaclePosition.Width, obstaclePosition.Height);
            obstacle.Background = RGB.DarkGray;
        }

        return mover;
    }
}

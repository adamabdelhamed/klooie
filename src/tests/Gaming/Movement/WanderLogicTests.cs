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
    public void WanderLogicBasic() => GamingTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified,60,30, async (context) =>
    {
        await SetupWanderLogicTest(context, new RectF(5,20,2,1), Angle.Right, new RectF(10,20,2,1));
        Game.Current.Stop();
    });

    private static async Task SetupWanderLogicTest(UITestManager context, RectF moverPosition, Angle moverAngle, params RectF[] obstaclePositions)
    {
        var mover = AddMoverAndObstacles(moverPosition, obstaclePositions);
        await context.PaintAndRecordKeyFrameAsync();
        
        var vision = Vision.Create(mover, autoScan: false);
        vision.Range = 15;
        vision.Scan();
        foreach(var trackedObject in vision.TrackedObjectsList) trackedObject.Target.Background = RGB.Orange;
        await context.PaintAndRecordKeyFrameAsync();

        var wanderOptions = new WanderOptions() { Speed = () => 1, Velocity = mover.Velocity, Vision = vision, CloseEnough = 5 };
        WanderLoopState state = WanderLoopState.Create(wanderOptions);
        mover.Velocity.Speed = 20;
        var scores = WanderLogic.AdjustSpeedAndVelocity(state);
        mover.Velocity.Speed = 0;
        var maxScore = scores.Items.Max(s => s.Total);
        var z = -1;
        foreach (var score in scores.Items)
        {
            var seen = new HashSet<Loc>();
            var strength = score.Total / maxScore;
            strength = Math.Max(strength, .4f);
            var lineColor = score.Total == maxScore ? RGB.Green : new RGB(0,0,(byte)ConsoleMath.Round(255 * strength));
            var lineStart = moverPosition.TopLeft;
            var lineEnd = moverPosition.TopLeft.RadialOffset(score.Angle, vision.Range);
            var bufferLendth = ConsoleBitmap.DefineLineBuffered(ConsoleMath.Round(lineStart.Left), ConsoleMath.Round(lineStart.Top), ConsoleMath.Round(lineEnd.Left), ConsoleMath.Round(lineEnd.Top));
            for(var i = 0; i < bufferLendth; i++)
            {
                var point = ConsoleBitmap.LineBuffer[i];
                if(seen.Contains(point)) continue;
                seen.Add(point);
                Game.Current.GamePanel.Add(new ConsoleStringRenderer("o".ToConsoleString(lineColor)) { ZIndex = score.Total == maxScore ? 10 : z, Foreground = lineColor, Bounds = new RectF(point.Left, point.Top,1,1) });
            }
            z--;
            var stack = Game.Current.GamePanel.Add(new StackPanel() { Y = 5, AutoSize = StackPanel.AutoSizeMode.Both });
            stack.Add(new ConsoleStringRenderer($"Angle: { score.Angle } degrees".ToWhite()));
            stack.Add(new ConsoleStringRenderer($"Collision Score: {score.Collision}    Weight: {WanderWeights.Default.CollisionWeight}".ToWhite()));
            stack.Add(new ConsoleStringRenderer($"Forward Score: {score.Forward}    Weight: {WanderWeights.Default.ForwardWeight}".ToWhite()));
            stack.Add(new ConsoleStringRenderer($"Inertia Score: {score.Inertia}    Weight: {WanderWeights.Default.InertiaWeight}".ToWhite()));
            stack.Add(new ConsoleStringRenderer($"Curiosity Score: {score.Curiosity}    Weight: {WanderWeights.Default.CuriosityWeight}".ToWhite()));
            stack.Add(new ConsoleStringRenderer($"Total Score: {score.Total}".ToWhite()));
            await context.PaintAndRecordKeyFrameAsync();
            stack.Dispose();
        }
    }

    private static GameCollider AddMoverAndObstacles(RectF moverPosition, RectF[] obstaclePositions)
    {
        var mover = Game.Current.GamePanel.Add(GameColliderPool.Instance.Rent());
        mover.MoveTo(moverPosition.Left, moverPosition.Top, int.MaxValue);
        mover.ResizeTo(moverPosition.Width, moverPosition.Height);
        mover.Background = RGB.Magenta;

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

 
using klooie.Gaming;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class PixelControlTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void PixelControl_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var pixelControl = ConsoleApp.Current.LayoutRoot.Add(new PixelControl()
        {
            Value = new ConsoleCharacter('A', RGB.Red, RGB.White)
        }).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();

        for (var i = (int)'B'; i <= (int)'Z'; i++)
        {
            pixelControl.Value = new ConsoleCharacter((char)i, RGB.Red, RGB.White);
            await context.PaintAndRecordKeyFrameAsync();
        }

        try
        {
            pixelControl.Width = 2;
            Assert.Fail("Expected exception");
        }
        catch (InvalidOperationException ex)
        {
        }

        try
        {
            pixelControl.Height = 2;
            Assert.Fail("Expected exception");
        }
        catch (InvalidOperationException ex)
        {
        }

        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void PixelControl_DrawCircle() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        await Task.Yield();
        foreach (var angle in Angle.Enumerate360Angles(0, 25))
        {
            var distanceFromCenter = 25;
            var obstacle = ConsoleApp.Current.LayoutRoot.Add(new ConsoleControl());
            var spot = ConsoleApp.Current.LayoutRoot.Center().RadialOffset(angle, distanceFromCenter).GetRounded();
            obstacle.MoveTo(spot.Left, spot.Top);
            obstacle.ResizeTo(2, 1);
            obstacle.Background = RGB.Red;
        }
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void PixelControl_DrawCircleWithGameAndCamera() => GamingTest.Run(new GamingTestOptions()
    {
        TestId = TestContext.TestId(),
        Mode = UITestMode.KeyFramesVerified,
        Camera = true,
        GameHeight = 60,
        GameWidth = 90,
        Test = async (context) =>
        {
            await Task.Yield();
            (Game.Current.GamePanel as Camera).PointAt(new LocF());
            var center = Game.Current.GameBounds.Center;
            var radius = 40;

            // Finer steps: 0.25 degree increments = 1440 steps around the circle
            for (Angle angle = 0.25f; angle.Value != 0; angle = angle.Add(0.25f))
            {
                var spot = center.RadialOffset(angle, radius);
                var rounded = spot.GetRounded();
                if (angle.Value >= 260 && angle.Value <= 280)
                {
                    spot = center.RadialOffset(angle, radius);
                    Console.WriteLine($"");
                    Console.Write($"Top: {spot.Top} r({rounded.Top}), Angle: {angle.Value}, Distance: {radius}");
                }

                var obstacle = Game.Current.GamePanel.Add(new ConsoleControl());
                obstacle.MoveTo(rounded.Left, rounded.Top);
                obstacle.ResizeTo(4, 2); // Or 2,1 for thinner rings
                obstacle.Background = RGB.Red;
            }
            await context.PaintAndRecordKeyFrameAsync();
            ConsoleApp.Current.Stop();
        }
    });

}

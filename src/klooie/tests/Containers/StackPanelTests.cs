using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class StackPanelTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void StackPanel_VerticalAutoSizeNoMargin()
    {
        var app = new KlooieTestHarness(TestContext, true);
        app.SecondsBetweenKeyframes = .05f;
        app.Invoke(async ()=>
        {
            var stack = app.LayoutRoot.Add(new StackPanel() { AutoSize = true, Orientation = Orientation.Vertical }).CenterBoth();

            foreach(var color in new RGB[] { RGB.Red, RGB.Red, RGB.Yellow, RGB.Yellow, RGB.Green, RGB.Green, RGB.Blue, RGB.Blue, RGB.Magenta, RGB.Magenta })
            {
                stack.Add(new ConsoleControl() { Background = color, Width = 50 });
                await app.PaintAndRecordKeyFrameAsync();
            }

            app.Stop();
        });

        app.Run();
        app.AssertThisTestMatchesLKG();
    }

    [TestMethod]
    public void StackPanel_HorizontalAutoSizeNoMargin()
    {
        var app = new KlooieTestHarness(TestContext, true);
        app.SecondsBetweenKeyframes = .05f;
        app.Invoke(async () =>
        {
            var stack = app.LayoutRoot.Add(new StackPanel() { AutoSize = true, Orientation = Orientation.Horizontal }).CenterBoth();

            foreach (var color in new RGB[] { RGB.Red, RGB.Red, RGB.Yellow, RGB.Yellow, RGB.Green, RGB.Green, RGB.Blue, RGB.Blue, RGB.Magenta, RGB.Magenta })
            {
                stack.Add(new ConsoleControl() { Background = color, Height = 20, Width = 4 });
                await app.PaintAndRecordKeyFrameAsync();
            }

            app.Stop();
        });

        app.Run();
        app.AssertThisTestMatchesLKG();
    }

    [TestMethod]
    public void StackPanel_VerticalAutoSize2Margin()
    {
        var app = new KlooieTestHarness(TestContext, true);
        app.SecondsBetweenKeyframes = .05f;
        app.Invoke(async () =>
        {
            var stack = app.LayoutRoot.Add(new StackPanel() { AutoSize = true, Orientation = Orientation.Vertical, Margin = 2 }).CenterBoth();

            foreach (var color in new RGB[] { RGB.Red, RGB.Red, RGB.Yellow, RGB.Yellow, RGB.Green, RGB.Green, RGB.Blue, RGB.Blue, RGB.Magenta, RGB.Magenta })
            {
                stack.Add(new ConsoleControl() { Background = color, Width = 50 });
                await app.PaintAndRecordKeyFrameAsync();
            }

            app.Stop();
        });

        app.Run();
        app.AssertThisTestMatchesLKG();
    }

    [TestMethod]
    public void StackPanel_HorizontalAutoSize2Margin()
    {
        var app = new KlooieTestHarness(TestContext, true);
        app.SecondsBetweenKeyframes = .05f;
        app.Invoke(async () =>
        {
            var stack = app.LayoutRoot.Add(new StackPanel() { AutoSize = true, Orientation = Orientation.Horizontal, Margin = 2 }).CenterBoth();

            foreach (var color in new RGB[] { RGB.Red, RGB.Red, RGB.Yellow, RGB.Yellow, RGB.Green, RGB.Green, RGB.Blue, RGB.Blue, RGB.Magenta, RGB.Magenta })
            {
                stack.Add(new ConsoleControl() { Background = color, Height = 20, Width = 4 });
                await app.PaintAndRecordKeyFrameAsync();
            }

            app.Stop();
        });

        app.Run();
        app.AssertThisTestMatchesLKG();
    }
}

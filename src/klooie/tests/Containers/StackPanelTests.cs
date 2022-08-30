using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class StackPanelTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void StackPanel_VerticalAutoSizeNoMargin() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        context.SecondsBetweenKeyframes = .05f;
        var stack = ConsoleApp.Current.LayoutRoot.Add(new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both, Orientation = Orientation.Vertical }).CenterBoth();

        foreach(var color in new RGB[] { RGB.Red, RGB.Red, RGB.Yellow, RGB.Yellow, RGB.Green, RGB.Green, RGB.Blue, RGB.Blue, RGB.Magenta, RGB.Magenta })
        {
            stack.Add(new ConsoleControl() { Background = color, Width = 50 });
            await context.PaintAndRecordKeyFrameAsync();
        }

        ConsoleApp.Current.Stop();
    });
 
    [TestMethod]
    public void StackPanel_HorizontalAutoSizeNoMargin() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        context.SecondsBetweenKeyframes = .05f;
        var stack = ConsoleApp.Current.LayoutRoot.Add(new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both, Orientation = Orientation.Horizontal }).CenterBoth();

        foreach (var color in new RGB[] { RGB.Red, RGB.Red, RGB.Yellow, RGB.Yellow, RGB.Green, RGB.Green, RGB.Blue, RGB.Blue, RGB.Magenta, RGB.Magenta })
        {
            stack.Add(new ConsoleControl() { Background = color, Height = 20, Width = 4 });
            await context.PaintAndRecordKeyFrameAsync();
        }

        ConsoleApp.Current.Stop();
    });
 

    [TestMethod]
    public void StackPanel_VerticalAutoSize2Margin()=> AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async(context) =>
    {
        context.SecondsBetweenKeyframes = .05f;
        var stack = ConsoleApp.Current.LayoutRoot.Add(new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both, Orientation = Orientation.Vertical, Margin = 2 }).CenterBoth();

        foreach (var color in new RGB[] { RGB.Red, RGB.Red, RGB.Yellow, RGB.Yellow, RGB.Green, RGB.Green, RGB.Blue, RGB.Blue, RGB.Magenta, RGB.Magenta })
        {
            stack.Add(new ConsoleControl() { Background = color, Width = 50 });
            await context.PaintAndRecordKeyFrameAsync();
        }

        ConsoleApp.Current.Stop();
    });

    

    [TestMethod]
    public void StackPanel_HorizontalAutoSize2Margin() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        context.SecondsBetweenKeyframes = .05f;
        Console.WriteLine($"App height is {ConsoleApp.Current.Height}");
        var stack = ConsoleApp.Current.LayoutRoot.Add(new StackPanel() { AutoSize = StackPanel.AutoSizeMode.Both, Orientation = Orientation.Horizontal, Margin = 2 }).CenterBoth();
        Console.WriteLine($"Stack height is {stack.Height} before adding anything");
        foreach (var color in new RGB[] { RGB.Red, RGB.Red, RGB.Yellow, RGB.Yellow, RGB.Green, RGB.Green, RGB.Blue, RGB.Blue, RGB.Magenta, RGB.Magenta })
        {
            stack.Add(new ConsoleControl() { Background = color, Height = 20, Width = 4 });
            await context.PaintAndRecordKeyFrameAsync();
        }
        Console.WriteLine($"Stack height is {stack.Height} after adding everything");
        ConsoleApp.Current.Stop();
    });

      
}

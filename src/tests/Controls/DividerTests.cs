using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class DividerTests
{
    public TestContext TestContext { get; set; }

    
    [TestMethod]
    public void Divider_Horizontal() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var divider = ConsoleApp.Current.LayoutRoot.Add(new Divider(Orientation.Horizontal)).FillHorizontally().DockToTop(padding: 5);
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Divider_Vertical() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var divider = ConsoleApp.Current.LayoutRoot.Add(new Divider(Orientation.Vertical)).FillVertically().DockToLeft(padding: 5);
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Divider_HorizontalWithColorAndFocus() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var divider = ConsoleApp.Current.LayoutRoot.Add(new Divider(Orientation.Horizontal) { Foreground = RGB.Green }).FillHorizontally().DockToTop(padding: 5);
        await context.PaintAndRecordKeyFrameAsync();
        divider.CanFocus = true;
        divider.Focus();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Divider_VerticalWithColorAndFocus() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var divider = ConsoleApp.Current.LayoutRoot.Add(new Divider(Orientation.Vertical) { Foreground = RGB.Green }).FillVertically().DockToLeft(padding: 5);
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        divider.CanFocus = true;
        divider.Focus();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Divider_HorizontalPreventsInvalidSize() => AppTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
    {
        try
        {
            var divider = ConsoleApp.Current.LayoutRoot.Add(new Divider(Orientation.Horizontal)).FillVertically();
            Assert.Fail("An exception should have been thrown");
        }
        catch(ArgumentException ex)
        {
            Assert.IsTrue(ex.Message.Contains(nameof(Divider.Height)));
        }
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Divider_VerticalPreventsInvalidSize() => AppTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
    {
        try
        {
            var divider = ConsoleApp.Current.LayoutRoot.Add(new Divider(Orientation.Vertical)).FillHorizontally();
            Assert.Fail("An exception should have been thrown");
        }
        catch (ArgumentException ex)
        {
            Assert.IsTrue(ex.Message.Contains(nameof(Divider.Width)));
        }
        ConsoleApp.Current.Stop();
    });
}

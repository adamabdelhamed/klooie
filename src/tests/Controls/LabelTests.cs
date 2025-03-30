using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class LabelTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Label_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var label = LabelPool.Instance.Rent();
        var lease = label.Lease;
        label.Text = "Hello World".ToGreen(bg: RGB.Orange);
        ConsoleApp.Current.LayoutRoot.Add(label).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
        Assert.IsFalse(label.IsStillValid(lease));
        Assert.IsTrue(label.ShouldStop);
    });

    [TestMethod]
    public void Label_UnstyledUsedControlFGAndBG() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        ConsoleApp.Current.LayoutRoot.Add(new Label("Hello World".ToConsoleString()) { Foreground = RGB.Orange, Background = RGB.Magenta }).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Label_StyledDoesntUseControlFGAndBG() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        ConsoleApp.Current.LayoutRoot.Add(new Label(ConsoleString.Parse("Hello [Red]w[D]orld")) { Foreground = RGB.Orange, Background = RGB.Magenta }).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Label_AutoSizeCanBeDisabled() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        ConsoleApp.Current.LayoutRoot.Add(new Label("Hello World".ToConsoleString(), autoSize:false) { Width = "Hello".Length,  Foreground = RGB.Orange, Background = RGB.Magenta }).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });
}

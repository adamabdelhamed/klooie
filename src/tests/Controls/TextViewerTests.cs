using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class TextViewerTests
{
    public TestContext TestContext { get; set; }

    private const string loremIpsum = "START: Lorem ipsum dolor sit amet, consectetur adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.END";
    private static readonly ConsoleString loremIpsumStyled = ConsoleString.Parse("START: Lorem ipsum dolor sit amet, [Red]consectetur[D] adipiscing elit, sed do eiusmod tempor incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.END");


    [TestMethod]
    public void TextViewer_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        ConsoleApp.Current.LayoutRoot.Add(new TextViewer(loremIpsum.ToGreen(bg: RGB.Orange)) { Width = 50 }).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });


    [TestMethod]
    public void TextViewer_UnstyledUsedControlFGAndBG() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        ConsoleApp.Current.LayoutRoot.Add(new TextViewer(loremIpsum.ToConsoleString()) { Width = 50, Foreground = RGB.Orange, Background = RGB.Magenta }).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void TextViewer_StyledDoesntUseControlFGAndBG() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        ConsoleApp.Current.LayoutRoot.Add(new TextViewer(loremIpsumStyled) { Width = 50, Foreground = RGB.Orange, Background = RGB.Magenta }).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void TextViewer_AutoSizeCanBeDisabled() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
         ConsoleApp.Current.LayoutRoot.Add(new TextViewer(loremIpsum.ToConsoleString(), autoSize: TextViewer.AutoSizeMode.None) { Width = 50, Foreground = RGB.Orange, Background = RGB.Magenta }).CenterBoth();
         await context.PaintAndRecordKeyFrameAsync();
         ConsoleApp.Current.Stop();
    });
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class FixedAspectRatioTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void FixedAspectRatioPanel_Basic() => AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified,130,40, async (context) =>
    {
        context.SecondsBetweenKeyframes = .05f;
        var fixedPanel = ConsoleApp.Current.LayoutRoot.Add(new FixedAspectRatioPanel(2, new ConsolePanel() { Background = RGB.Green })).CenterBoth();
        ConsoleApp.Current.LayoutRoot.Background = RGB.DarkYellow;
        fixedPanel.Background = RGB.Red;
        fixedPanel.Width = ConsoleApp.Current.LayoutRoot.Width;
        fixedPanel.Height = ConsoleApp.Current.LayoutRoot.Height;
        await context.PaintAndRecordKeyFrameAsync();

        while (fixedPanel.Width > 2)
        {
            fixedPanel.Width -= 2;
            await context.PaintAndRecordKeyFrameAsync();
        }

        ConsoleApp.Current.Stop();
    });
}

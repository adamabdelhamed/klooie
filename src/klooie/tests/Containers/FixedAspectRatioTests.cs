using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Slow)]
public class FixedAspectRatioTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void FixedAspectRatioPanel_Basic()
    {
        KlooieTestHarness.SetConsoleSize(130, 40);
        var app = new KlooieTestHarness(this.TestContext, true);
        app.SecondsBetweenKeyframes = .05f;
        app.InvokeNextCycle(async () =>
        {
            var fixedPanel = app.LayoutRoot.Add(new FixedAspectRatioPanel(2, new ConsolePanel() { Background = RGB.Green })).CenterBoth();
            app.LayoutRoot.Background = RGB.DarkYellow;
            fixedPanel.Background = RGB.Red;
            fixedPanel.Width = app.LayoutRoot.Width;
            fixedPanel.Height = app.LayoutRoot.Height;
            await app.PaintAndRecordKeyFrameAsync();

            while (fixedPanel.Width > 2)
            {
                fixedPanel.Width -= 2;
                await app.PaintAndRecordKeyFrameAsync();
            }

            app.Stop();
        });
        app.Run();
        app.AssertThisTestMatchesLKG();
    }
}

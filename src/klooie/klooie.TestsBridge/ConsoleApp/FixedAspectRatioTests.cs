using klooie;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using PowerArgs.Cli;

namespace ArgsTests.CLI.Controls
{
    [TestClass]
    [TestCategory(Categories.Slow)]
    public class FixedAspectRatioTests
    {
        public TestContext TestContext { get; set; }
        
        [TestMethod]
        public void TestFixedAspectRatio()
        {
            CliTestHarness.SetConsoleSize(130, 40);
            var app = new CliTestHarness(this.TestContext, true);
            app.SecondsBetweenKeyframes = .05f;
            app.InvokeNextCycle(async () =>
            {
                var fixedPanel = app.LayoutRoot.Add(new FixedAspectRatioPanel(2, new ConsolePanel() { Background = RGB.Green })).CenterBoth();
                app.LayoutRoot.Background = RGB.DarkYellow;
                fixedPanel.Background = RGB.Red;
                fixedPanel.Width = app.LayoutRoot.Width;
                fixedPanel.Height = app.LayoutRoot.Height;
                await app.PaintAndRecordKeyFrameAsync();

                while(fixedPanel.Width > 2)
                {
                    fixedPanel.Width -= 2;
                    await app.PaintAndRecordKeyFrameAsync();
                }

                app.Stop();
            });
            app.Start().Wait();
            app.AssertThisTestMatchesLKG();
        }
    }
}

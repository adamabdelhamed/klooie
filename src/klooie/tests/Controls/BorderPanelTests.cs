using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class BorderPanelTests
{
    public TestContext TestContext { get; set; }

    
    [TestMethod]
    public void BorderPanel_Empty() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var border = ConsoleApp.Current.LayoutRoot.Add(new BorderPanel() { BorderColor = RGB.Magenta }).Fill();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void BorderPanel_Filled() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var filling = new ConsoleControl() { Background = RGB.DarkMagenta };
        var border = ConsoleApp.Current.LayoutRoot.Add(new BorderPanel(filling) { BorderColor = RGB.Magenta }).Fill();
        filling.Fill();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

}

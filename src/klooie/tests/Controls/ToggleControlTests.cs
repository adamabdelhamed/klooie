using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class ToggleControlTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void ToggleControl_Basic() => AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified,15,3, async (context) =>
    {
        var toggle = ConsoleApp.Current.LayoutRoot.Add(new ToggleControl()).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();
        
        toggle.On = true;
        await Task.Delay(250);
        await context.PaintAndRecordKeyFrameAsync();

        toggle.On = false;
        await Task.Delay(250);
        await context.PaintAndRecordKeyFrameAsync();

        toggle.Focus();
        await Task.Delay(250);
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });
}

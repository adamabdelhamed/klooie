using klooie;
using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace tests;

[TestClass]
public class ProtectedConsolePanelTests
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void TestInitialize() => TestContextHelper.GlobalSetup();


    [TestMethod]
    public void ProtectedPanel_Connected() => AppTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
    {
        var protectedPanel = ConsoleApp.Current.LayoutRoot.Add(new ProtectedConsolePanel()).Fill();
        await ConsoleApp.Current.RequestPaintAsync();
 
        var parent = protectedPanel.Parent;
        while (true)
        {
            if (parent.Parent == null) break;
            parent = parent.Parent;
        }
        Assert.AreSame(ConsoleApp.Current.LayoutRoot, parent);

        ConsoleApp.Current.Stop();
    });
}

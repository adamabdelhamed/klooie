using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class FocusTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void ConsoleApp_FocusStack() => AppTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
    {
        Assert.IsTrue(ConsoleApp.Current.LayoutRoot.IsFocusStackAtMyLevel);
        Assert.IsTrue(ConsoleApp.Current.FocusStackDepth == ConsoleApp.Current.LayoutRoot.FocusStackDepth);

        var child = ConsoleApp.Current.LayoutRoot.Add(new ConsoleControl());
        Assert.IsTrue(child.IsFocusStackAtMyLevel);
        Assert.AreEqual(child.FocusStackDepth, ConsoleApp.Current.LayoutRoot.FocusStackDepth);
        
        ConsoleApp.Current.Stop();
    });
}

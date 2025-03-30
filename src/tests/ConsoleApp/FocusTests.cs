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

    [TestMethod]
    public void FocusManager_StackIntegrity() => AppTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
    {
        var lt1 = new Recyclable();
        var lt2 = new Recyclable();

        var firstItemFired = false;
        var secondItemFired = false;

        ConsoleApp.Current.PushKeyForLifetime(System.ConsoleKey.Enter,()=> { firstItemFired = true; }, lt1);
        ConsoleApp.Current.PushKeyForLifetime(System.ConsoleKey.Enter, () => { secondItemFired = true; }, lt2);

        lt1.Dispose();
        await ConsoleApp.Current.SendKey(System.ConsoleKey.Enter);
        Assert.IsFalse(firstItemFired);
        Assert.IsTrue(secondItemFired);

        ConsoleApp.Current.Stop();
    });

}

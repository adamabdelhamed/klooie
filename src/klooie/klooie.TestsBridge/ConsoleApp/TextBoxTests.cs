using klooie.tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace ArgsTests.CLI.Controls;
[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class TextBoxTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void TestRenderTextBox() => AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified,9,1,async(context)=>
    { 
        ConsoleApp.Current.LayoutRoot.Add(new TextBox() { Value = "SomeText".ToWhite() }).Fill();
        await context.PaintAndRecordKeyFrameAsync();
        Assert.IsTrue(context.Find("SomeText".ToWhite()).HasValue);
        ConsoleApp.Current.Stop();
    });
}


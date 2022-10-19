using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class PixelControlTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void PixelControl_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var pixelControl = ConsoleApp.Current.LayoutRoot.Add(new PixelControl()
        {
            Value = new ConsoleCharacter('A', RGB.Red, RGB.White)
        }).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();

        for(var i = (int)'B'; i <= (int)'Z';i++)
        {
            pixelControl.Value = new ConsoleCharacter((char)i, RGB.Red, RGB.White);
            await context.PaintAndRecordKeyFrameAsync();
        }

        try
        {
            pixelControl.Width = 2;
            Assert.Fail("Expected exception");
        }
        catch (InvalidOperationException ex)
        { 
        }

        try
        {
            pixelControl.Height = 2;
            Assert.Fail("Expected exception");
        }
        catch (InvalidOperationException ex)
        {
        }

        ConsoleApp.Current.Stop();
    });
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class ColorPickerTests
{
    public TestContext TestContext { get; set; }

    
    [TestMethod]
    public void ColorPicker_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        context.SecondsBetweenKeyframes = .05f;
        var picker = ConsoleApp.Current.LayoutRoot.Add(new ColorPicker()).CenterBoth();
        picker.Width = 30;
        await context.PaintAndRecordKeyFrameAsync();
        picker.Focus();
        await context.PaintAndRecordKeyFrameAsync();
        await ConsoleApp.Current.SendKey(ConsoleKey.Enter);
        for (var i = 0; i < 10; i++)
        {
            await ConsoleApp.Current.SendKey(ConsoleKey.DownArrow);
            await context.PaintAndRecordKeyFrameAsync();
        }
        await ConsoleApp.Current.SendKey(ConsoleKey.Enter);
        await context.PaintAndRecordKeyFrameAsync();
        Assert.AreEqual(RGB.Green, picker.Value);
        ConsoleApp.Current.Stop();
    });
}

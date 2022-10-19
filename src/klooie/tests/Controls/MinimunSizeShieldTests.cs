using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class MinimumSizeShieldTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void MinimumSizeShield_Basic() => AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified,120,40, async (context) =>
    {
        context.SecondsBetweenKeyframes = .05f;
        ConsoleApp.Current.LayoutRoot.Background = RGB.White;
        var somePanel = ConsoleApp.Current.LayoutRoot.Add(new ConsolePanel() { Background = RGB.Magenta });
        var shield = somePanel.Add(new MinimumSizeShield(new MinimumSizeShieldOptions()
        {
            MinWidth = ConsoleApp.Current.LayoutRoot.Width-15,
            MinHeight = ConsoleApp.Current.LayoutRoot.Height-5,
        })).Fill();
        shield.Background = RGB.DarkRed;

        somePanel.ResizeTo(ConsoleApp.Current.LayoutRoot.Width, ConsoleApp.Current.LayoutRoot.Height);
        await context.PaintAndRecordKeyFrameAsync();

        while(somePanel.Width > 1)
        {
            somePanel.Width--;
            await context.PaintAndRecordKeyFrameAsync();
        }

        while (somePanel.Width < ConsoleApp.Current.LayoutRoot.Width)
        {
            somePanel.Width++;
            await context.PaintAndRecordKeyFrameAsync();
        }


        while (somePanel.Height > 1)
        {
            somePanel.Height--;
            await context.PaintAndRecordKeyFrameAsync();
        }

        while (somePanel.Height < ConsoleApp.Current.LayoutRoot.Height)
        {
            somePanel.Height++;
            await context.PaintAndRecordKeyFrameAsync();
        }
        ConsoleApp.Current.Stop();
    });
}

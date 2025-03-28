﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class SliderTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Slider_Basic() => AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified,15,3, async (context) =>
    {
        context.SecondsBetweenKeyframes = .25f;
        var slider = ConsoleApp.Current.LayoutRoot.Add(new Slider()
        {
            Min = 0,
            Max = 10,
            Increment = 1,
            
        }).CenterBoth();

        var label = ConsoleApp.Current.LayoutRoot.Add(new Label());
        slider.ValueChanged.Sync(() => label.Text = $" {slider.Value.ToString()} ".ToBlack(RGB.Orange), Recyclable.EarliestOf(label, slider));
        slider.BoundsChanged.Sync(() => label.MoveTo(slider.X, slider.Bottom()), Recyclable.EarliestOf(label, slider));


        await context.PaintAndRecordKeyFrameAsync();
        slider.Focus();
        await context.PaintAndRecordKeyFrameAsync();

        for(var i = 0; i < 11; i++)
        {
            await ConsoleApp.Current.SendKey(ConsoleKey.RightArrow);
            await context.PaintAndRecordKeyFrameAsync();
        }

        for (var i = 0; i < 11; i++)
        {
            await ConsoleApp.Current.SendKey(ConsoleKey.LeftArrow);
            await context.PaintAndRecordKeyFrameAsync();
        }

        ConsoleApp.Current.Stop();
    });
}

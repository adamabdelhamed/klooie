﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
namespace klooie.tests;
[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class ScrollablePanelTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void ScrollablePanel_Basic()
    {
        KlooieTestHarness.SetConsoleSize(80, 10);
        var app = new KlooieTestHarness(this.TestContext, true);
        app.SecondsBetweenKeyframes = .1f;
        app.LayoutRoot.Background = RGB.Red;
        app.InvokeNextCycle(async () =>
        {
                // We want to have a stack panel that can scroll if it gets too big

                // Step 1 - Create a scroll panel and size it how you like
                var scrollPanel = app.LayoutRoot.Add(new ScrollablePanel() { Background = RGB.DarkBlue }).Fill();

                // Step 2 - Add scrollable content to the ScrollableContent container
                var stack = scrollPanel.ScrollableContent.Add(new StackPanel() { Background = RGB.Yellow, AutoSize = StackPanel.AutoSizeMode.Both });

                // IMPORTANT - The ScrollableContent container is the thing that will scroll if it's bigger than the view
                //             so make sure it's height gets bigger as its content grows.
                stack.SubscribeForLifetime(nameof(stack.Bounds), () => scrollPanel.ScrollableContent.Height = stack.Height, stack);

                // Step 3 - Add 100 focusable rows to the stack panel. Making the rows focusable is critical since
                //          the scroll panel will automatically take care of scrolling to the currently focused control
                //          if that control is within the ScrollableContent.
                var rows = 100;
            for (var i = 1; i <= rows; i++)
            {
                var label = stack.Add(new Label() { CanFocus = true, Text = $"row {i} of {rows}".ToWhite() });
                label.Focused.SubscribeForLifetime(() => label.Text = label.Text.ToCyan(), label);
                label.Focused.SubscribeForLifetime(() => label.Text = label.Text.ToWhite(), label);
            }

            await app.SendKey(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));
                // Step 4 - Tab through all the rows. The scrollbar and scrollable content should automatically
                //        - keep the focused row in view.
                for (var i = 0; i < rows; i++)
            {
                app.SendKey(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));
                await app.PaintAndRecordKeyFrameAsync();
            }
            app.Stop();
        });

        app.Run();
        app.AssertThisTestMatchesLKG();
    }
}
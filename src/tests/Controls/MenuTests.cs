using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class MenuTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Menu_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var items = new List<string>()
        {
            "Item1",
            "Item2",
            "Item3",
            "Item4",
            "Item5",
        };
        var menu = ConsoleApp.Current.LayoutRoot.Add(new Menu<string>(items)).Fill();
        await context.PaintAndRecordKeyFrameAsync();
        menu.Focus();
        await context.PaintAndRecordKeyFrameAsync();

        var validations = 0;
        var expectedValidations = items.Count;
        for (var i = 0; i < items.Count;i++)
        {
            Assert.IsTrue(menu.HasFocus);
            menu.SelectedIndexChanged.SubscribeOnce(() =>
            {
                var itemActivated = menu.SelectedItem;
                Assert.AreEqual(items[i], itemActivated);
                Assert.AreEqual(items[i], menu.SelectedItem);
                Assert.AreEqual(i, menu.SelectedIndex);
                validations++;
            });
            await ConsoleApp.Current.SendKey(ConsoleKey.Enter);
            await ConsoleApp.Current.SendKey(ConsoleKey.DownArrow);
            await context.PaintAndRecordKeyFrameAsync();
        }

        Assert.AreEqual(expectedValidations, validations);
        ConsoleApp.Current.Stop();
    });
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class DropdownTests
{
    public TestContext TestContext { get; set; }

    
    [TestMethod]
    public void Dropdown_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var options = new List<DialogChoice>()
        {
            new DialogChoice(){ DisplayText = "RedItem".ToRed(), Id = "red" },
            new DialogChoice(){ DisplayText = "BlueItem".ToBlue(), Id = "blue" },
            new DialogChoice(){ DisplayText = "GreenItem".ToGreen(), Id = "green" },
        };
        var dropdown = ConsoleApp.Current.LayoutRoot.Add(new Dropdown(options)).CenterBoth();
        dropdown.Width = 3 + options.Select(o => o.DisplayText.Length).Max();
        Assert.AreEqual("red", dropdown.Value.Id);

        await context.PaintAndRecordKeyFrameAsync();
        dropdown.Focus();
        Assert.IsTrue(dropdown.HasFocus);

        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.DownArrow.KeyInfo(), "blue");
        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.DownArrow.KeyInfo(), "green");
        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.DownArrow.KeyInfo(), "red");

        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.UpArrow.KeyInfo(), "green");
        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.UpArrow.KeyInfo(), "blue");
        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.UpArrow.KeyInfo(), "red");

        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.Tab.KeyInfo(), "blue");
        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.Tab.KeyInfo(), "green");
        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.Tab.KeyInfo(), "red");

        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.Tab.KeyInfo(shift:true), "green");
        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.Tab.KeyInfo(shift: true), "blue");
        await OpenDropdownChangeAndAssertValue(context, dropdown, ConsoleKey.Tab.KeyInfo(shift: true), "red");

        ConsoleApp.Current.Stop();
    });

    private async Task OpenDropdownChangeAndAssertValue(UITestManager context, Dropdown d, ConsoleKeyInfo dropdownOpenAction, string expectedId)
    {
        await ConsoleApp.Current.SendKey(ConsoleKey.Enter);
        await context.PaintAndRecordKeyFrameAsync();
        await ConsoleApp.Current.SendKey(dropdownOpenAction);
        await context.PaintAndRecordKeyFrameAsync();
        await ConsoleApp.Current.SendKey(ConsoleKey.Enter);
        await context.PaintAndRecordKeyFrameAsync();
        Assert.AreEqual(expectedId, d.Value.Id);
    }
}

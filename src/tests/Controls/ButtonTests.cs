using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class ButtonTests
{
    public TestContext TestContext { get; set; }

    
    [TestMethod]
    public void Button_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        int pressCount = 0;
        var button = ConsoleApp.Current.LayoutRoot.Add(new Button() { Text = "Hello World".ToConsoleString() }).CenterBoth();
        button.Pressed.SubscribeOnce(() => pressCount++);
        await context.PaintAndRecordKeyFrameAsync();
        button.Focus();
        Assert.IsTrue(button.HasFocus);
        Assert.AreEqual(0, pressCount);
        await ConsoleApp.Current.SendKey(System.ConsoleKey.Enter);
        Assert.AreEqual(1, pressCount);

        button.Unfocus();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Button_ShortcutNaked() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, (context) => Button_WithShortcut(new KeyboardShortcut(ConsoleKey.A,null), context) );

    [TestMethod]
    public void Button_ShortcutAlt() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, (context) => Button_WithShortcut(new KeyboardShortcut(ConsoleKey.A, ConsoleModifiers.Alt), context));

    [TestMethod]
    public void Button_ShortcutShift() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, (context) => Button_WithShortcut(new KeyboardShortcut(ConsoleKey.A, ConsoleModifiers.Shift), context));

    [TestMethod]
    public void Button_ShortcutNumbers() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, (context) => Button_WithShortcut(new KeyboardShortcut(ConsoleKey.D1, null), context, ConsoleKey.NumPad1.KeyInfo()));


    private async Task Button_WithShortcut(KeyboardShortcut shortcut, UITestManager context, ConsoleKeyInfo? toSend = null)
    {
        int pressCount = 0;
        var button = ConsoleApp.Current.LayoutRoot.Add(new Button(shortcut) { Text = "Hello World".ToConsoleString() }).CenterBoth();
        button.Pressed.SubscribeOnce(() => pressCount++);
        await context.PaintAndRecordKeyFrameAsync();
        button.Focus();
        Assert.IsTrue(button.HasFocus);
        Assert.AreEqual(0, pressCount);
        var alt = shortcut.Modifier.HasValue && shortcut.Modifier.Value.HasFlag(System.ConsoleModifiers.Alt);
        var shift = shortcut.Modifier.HasValue && shortcut.Modifier.Value.HasFlag(System.ConsoleModifiers.Shift);
        var toPress = toSend.HasValue ? toSend.Value : shortcut.Key.KeyInfo(shift, alt, false);
        await ConsoleApp.Current.SendKey(toPress);
        Assert.AreEqual(1, pressCount);

        button.Unfocus();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    }
}

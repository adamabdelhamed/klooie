using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class DialogTests
{
    public TestContext TestContext { get; set; }

    /// <summary>
    /// This test will store the whole video so we can visually look at the information
    /// </summary>
    [TestMethod]
    public void Dialog_Basic() => DialogBasic(UITestMode.RealTimeFYI);

    /// <summary>
    /// This test will record keyframes so that we can check for regressions
    /// </summary>
    [TestMethod]
    public void Dialog_BasicKeyFramed() => DialogBasic(UITestMode.KeyFramesVerified);

    [TestMethod]
    public void Dialog_ShowMessage() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified,async (context) =>
    {
        Dialog.Shown.Subscribe(async () => await context.PaintAndRecordKeyFrameAsync(), ConsoleApp.Current);
        await context.PaintAndRecordKeyFrameAsync();
        await MessageDialog.Show(new ShowMessageOptions("Hello world".ToGreen()) { UserChoices = DialogChoice.Close, SpeedPercentage = 0, MaxLifetime = Task.Delay(300).ToLifetime() });
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });
    


    [TestMethod]
    public void Dialog_ShowLargeMessage() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var msg = "";
        for (var i = 1; i <= 100; i++)
        {
            msg += $"Line {i}\n";
        }

        context.SecondsBetweenKeyframes = .02f;
        await context.PaintAndRecordKeyFrameAsync();
        var dialogTask = MessageDialog.Show(new ShowMessageOptions(msg.ToGreen()) { SpeedPercentage = 0 });
        await Task.Delay(20);
        await context.PaintAndRecordKeyFrameAsync();
        await ConsoleApp.Current.SendKey(ConsoleKey.Tab);
        for (var i = 0; i < 100;i++)
        {
            await ConsoleApp.Current.SendKey(ConsoleKey.DownArrow);
            await context.PaintAndRecordKeyFrameAsync();
        }
        ConsoleApp.Current.Stop();
    });
    

    [TestMethod]
    public void Dialog_Yes()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            app.Invoke(async () =>
            {
                await Task.Delay(300);
                await app.SendKey(ConsoleKey.Tab, shift: true);
                await app.SendKey(ConsoleKey.Enter);
            });
            var choice = await MessageDialog.Show(new ShowMessageOptions($"Yes or no?".ToGreen()) { UserChoices = DialogChoice.YesNo, SpeedPercentage = 0 });
            Assert.IsTrue("yes".Equals(choice.Id, StringComparison.OrdinalIgnoreCase));
            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Dialog_No()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            Dialog.Shown.SubscribeOnce(async () => await app.SendKey(ConsoleKey.Enter));
            var choice = await MessageDialog.Show(new ShowMessageOptions($"Yes or no?".ToGreen()) { UserChoices = DialogChoice.YesNo, SpeedPercentage = 0 });
            Assert.IsTrue("no".Equals(choice.Id, StringComparison.OrdinalIgnoreCase));
            app.Stop();
        });
        app.Run();
    }


    [TestMethod]
    public void Dialog_Cancel()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            Dialog.Shown.SubscribeOnce(async () => await app.SendKey(ConsoleKey.Escape));
            var choice = await MessageDialog.Show(new ShowMessageOptions($"Yes or no?".ToGreen()) { UserChoices = DialogChoice.YesNo, SpeedPercentage = 0 });
            Assert.IsNull(choice);
            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Dialog_ShowMessagePorted1() => AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified,80,20,async (context) =>
    {
        Task dialogTask;
        // show hello world message, wait for a paint, then take a keyframe of the screen, which 
        // should have the dialog shown
        dialogTask = MessageDialog.Show("Hello world");
        await context.PaintAndRecordKeyFrameAsync();
        Assert.IsFalse(dialogTask.IsFulfilled());

        // simulate an enter keypress, which should clear the dialog
        await ConsoleApp.Current.SendKey(ConsoleKey.Enter);
        await context.PaintAndRecordKeyFrameAsync();
        await dialogTask;
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Dialog_ShowTextInputPorted() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified,async (context) =>
    {
        Task<ConsoleString?> dialogTask;
        dialogTask = TextInputDialog.Show(new ShowTextInputOptions("Rich text input prompt text".ToGreen()) { SpeedPercentage = 0 });
        await context.PaintAndRecordKeyFrameAsync();
        Assert.IsFalse(dialogTask.IsFulfilled());
        await ConsoleApp.Current.SendKey(ConsoleKey.A.KeyInfo(shift: true));
        await context.PaintAndRecordKeyFrameAsync();
        await ConsoleApp.Current.SendKey(ConsoleKey.D);
        await context.PaintAndRecordKeyFrameAsync();
        await ConsoleApp.Current.SendKey(ConsoleKey.A);
        await context.PaintAndRecordKeyFrameAsync();
        await ConsoleApp.Current.SendKey(ConsoleKey.M);
        await context.PaintAndRecordKeyFrameAsync();
        Assert.IsFalse(dialogTask.IsFulfilled());
        await ConsoleApp.Current.SendKey(ConsoleKey.Enter);
        var stringVal = (await dialogTask).ToString();
        await context.PaintAndRecordKeyFrameAsync();
        Assert.AreEqual("Adam", stringVal);
        ConsoleApp.Current.Stop();
    });
    

    private void DialogBasic(UITestMode mode) => AppTest.Run(TestContext.TestId(), mode, async (context) =>
    {
        await Task.Delay(25);
        if (mode == UITestMode.KeyFramesVerified) await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.LayoutRoot.Background = RGB.Green;
        var label = ConsoleApp.Current.LayoutRoot.Add(new Label() { Text = "Background text".ToYellow(), CanFocus = true }).DockToTop(padding: 1).CenterHorizontally();
        ConsoleApp.Current.ClearFocus();
        await Task.Delay(250);
        if (mode == UITestMode.KeyFramesVerified) await context.PaintAndRecordKeyFrameAsync();
        Assert.IsFalse(label.HasFocus);
        await ConsoleApp.Current.SendKey(ConsoleKey.Tab);
        if (mode == UITestMode.KeyFramesVerified) await context.PaintAndRecordKeyFrameAsync();
        Assert.IsTrue(label.HasFocus);
        await Task.Delay(250);
        var options = new DialogOptions();
        await Dialog.Show(() =>
        {
            Assert.IsFalse(label.HasFocus);

            var ret = new ConsolePanel() { Width = 60, Height = 7, Background = RGB.Red, IsVisible = false };
            ret.SubscribeOnce(nameof(ret.IsVisible), async () =>
            {
                if (mode == UITestMode.KeyFramesVerified) await context.PaintAndRecordKeyFrameAsync();
                Assert.IsTrue(ret.IsVisible);
                await ConsoleApp.Current.SendKey(ConsoleKey.Tab);
                Assert.IsFalse(label.HasFocus);
                await Task.Delay(100);
                ret.Dispose();
            });
            return ret;
        }, options);
        if (mode == UITestMode.KeyFramesVerified) await context.PaintAndRecordKeyFrameAsync();
        Assert.IsTrue(label.HasFocus);
        ConsoleApp.Current.Stop();
    });
}

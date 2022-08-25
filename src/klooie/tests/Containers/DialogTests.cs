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
    public void Dialog_Basic() => DialogBasic(false);

    /// <summary>
    /// This test will record keyframes so that we can check for regressions
    /// </summary>
    [TestMethod]
    public void Dialog_BasicKeyFramed() => DialogBasic(true);

    [TestMethod]
    public void Dialog_ShowMessage()
    {
        var app = new KlooieTestHarness(TestContext, true);
        Dialog.Shown.SubscribeForLifetime(async () => await app.PaintAndRecordKeyFrameAsync(), app);

        app.Invoke(async () =>
        {
            await app.PaintAndRecordKeyFrameAsync();
            await MessageDialog.Show(new ShowMessageOptions("Hello world".ToGreen()) { UserChoices = DialogChoice.Close, SpeedPercentage = 0, MaxLifetime = Task.Delay(300).ToLifetime() });
            await app.PaintAndRecordKeyFrameAsync();
            app.Stop();
        });

        app.Run();
       app.AssertThisTestMatchesLKG();
    }


    [TestMethod]
    public void Dialog_ShowLargeMessage()
    {
        var msg = "";
        for(var i = 1; i <= 100; i++)
        {
            msg += $"Line {i}\n";
        }
        var app = new KlooieTestHarness(TestContext, true);
        app.SecondsBetweenKeyframes = .02f;
        app.Invoke(async () =>
        {
            await app.PaintAndRecordKeyFrameAsync();
            var dialogTask = MessageDialog.Show(new ShowMessageOptions(msg.ToGreen()) { SpeedPercentage = 0 });
            await Task.Delay(20);
            await app.PaintAndRecordKeyFrameAsync();
            await app.SendKey(ConsoleKey.Tab);
            for (var i = 0; i < 100;i++)
            {
                await app.SendKey(ConsoleKey.DownArrow);
                await app.PaintAndRecordKeyFrameAsync();
            }
            app.Stop();
        });

        app.Run();
        app.AssertThisTestMatchesLKG();
    }

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
    public void Dialog_ShowMessagePorted1()
    {
        KlooieTestHarness.SetConsoleSize(80, 20);
        var app = new KlooieTestHarness(this.TestContext, true);

        app.InvokeNextCycle(async () =>
        {
            Task dialogTask;

            // show hello world message, wait for a paint, then take a keyframe of the screen, which 
            // should have the dialog shown
            dialogTask = MessageDialog.Show("Hello world");
            await app.PaintAndRecordKeyFrameAsync();
            Assert.IsFalse(dialogTask.IsFulfilled());

            // simulate an enter keypress, which should clear the dialog
            await app.SendKey(ConsoleKey.Enter);
            await app.PaintAndRecordKeyFrameAsync();
            await dialogTask;
            app.Stop();
        });

        app.Start().Wait();
        app.AssertThisTestMatchesLKG();
    }



    [TestMethod]
    public void Dialog_ShowTextInputPorted()
    {
        var app = new KlooieTestHarness(this.TestContext, true);

        app.InvokeNextCycle(async () =>
        {
            Task<ConsoleString> dialogTask;
            dialogTask = TextInputDialog.Show("Rich text input prompt text".ToGreen(), new ShowTextInputOptions() { SpeedPercentage = 0 });
            await app.PaintAndRecordKeyFrameAsync();
            Assert.IsFalse(dialogTask.IsFulfilled());
            await app.SendKey(ConsoleKey.A.KeyInfo(shift: true));
            await app.PaintAndRecordKeyFrameAsync();
            await app.SendKey(ConsoleKey.D);
            await app.PaintAndRecordKeyFrameAsync();
            await app.SendKey(ConsoleKey.A);
            await app.PaintAndRecordKeyFrameAsync();
            await app.SendKey(ConsoleKey.M);
            await app.PaintAndRecordKeyFrameAsync();
            Assert.IsFalse(dialogTask.IsFulfilled());
            await app.SendKey(ConsoleKey.Enter);
            var stringVal = (await dialogTask).ToString();
            await app.RequestPaintAsync();
            app.RecordKeyFrame();
            Assert.AreEqual("Adam", stringVal);
            app.Stop();
        });

        app.Start().Wait();
        app.AssertThisTestMatchesLKG();
    }

    private void DialogBasic(bool keyframes)
    {
        var app = new KlooieTestHarness(TestContext, keyframes);
        app.Invoke(async () =>
        {
            await Task.Delay(25);
            if (keyframes) await app.PaintAndRecordKeyFrameAsync();
            app.LayoutRoot.Background = RGB.Green;
            var label = app.LayoutRoot.Add(new Label() { Text = "Background text".ToYellow(), CanFocus = true }).DockToTop(padding: 1).CenterHorizontally();
            app.ClearFocus();
            await Task.Delay(250);
            if (keyframes) await app.PaintAndRecordKeyFrameAsync();
            Assert.IsFalse(label.HasFocus);
            await app.SendKey(ConsoleKey.Tab);
            if (keyframes) await app.PaintAndRecordKeyFrameAsync();
            Assert.IsTrue(label.HasFocus);
            await Task.Delay(250);
            var options = new DialogOptions();
            await Dialog.Show(() =>
            {
                Assert.IsFalse(label.HasFocus);

                var ret = new ConsolePanel() { Width = 60, Height = 7, Background = RGB.Red, IsVisible = false };
                ret.SubscribeOnce(nameof(ret.IsVisible), async () =>
                {
                    if (keyframes) await app.PaintAndRecordKeyFrameAsync();
                    Assert.IsTrue(ret.IsVisible);
                    await app.SendKey(ConsoleKey.Tab);
                    Assert.IsFalse(label.HasFocus);
                    await Task.Delay(100);
                    ret.Dispose();
                });
                return ret;
            }, options);
            if (keyframes) await app.PaintAndRecordKeyFrameAsync();
            Assert.IsTrue(label.HasFocus);
            app.Stop();
        });

        app.Run();
        if (keyframes)
        {
            app.AssertThisTestMatchesLKG();
        }
        else
        {
            app.AssertThisTestMatchesLKGFirstAndLastFrame();
        }
    }
}

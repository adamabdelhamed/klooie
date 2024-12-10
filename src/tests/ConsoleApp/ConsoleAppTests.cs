using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class ConsoleAppTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void ConsoleApp_PaintsPanel()
    {
        var correctColor = new RGB(1, 2, 3);
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            app.LayoutRoot.Background = correctColor;
            await app.RequestPaintAsync();
            for(var x = 0; x < app.Bitmap.Width; x++)
            {
                for (var y = 0; y < app.Bitmap.Height; y++)
                {
                    Assert.AreEqual(' ', app.Bitmap.GetPixel(x, y).Value);
                    Assert.AreEqual(correctColor, app.Bitmap.GetPixel(x, y).BackgroundColor);
                }
            }
            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void ConsoleApp_ExceptionsPreservedSync()
    {
        ConsoleApp app = new ConsoleApp();
        app.Invoke(() => throw new FormatException("Some fake exception"));
        try
        {
            app.Run();
            Assert.Fail("An exception should have been thrown");
        }
        catch (FormatException ex)
        {
            Assert.AreEqual("Some fake exception", ex.Message);
        }
    }

    [TestMethod]
    public void ConsoleApp_KeyInputGlobal()
    {
        ConsoleApp app = new ConsoleApp();
        var detected = false;
        app.Invoke(async () =>
        {
            app.PushKeyForLifetime(ConsoleKey.Enter, () => detected = true, app);
            await app.SendKey(new ConsoleKeyInfo('!', ConsoleKey.Enter, false, false, false));
            app.Stop();
        });

        app.Run();
        Assert.IsTrue(detected);
    }

    [TestMethod]
    public void ConsoleApp_KeyInputControl()
    {
        ConsoleApp app = new ConsoleApp();
        var detected = false;
        app.Invoke(async () =>
        {
            var c = app.LayoutRoot.Add(new ConsoleControl() { CanFocus = true });
            c.KeyInputReceived.Subscribe(k =>
            {
                if (k.Key == ConsoleKey.Enter)
                {
                    detected = true;
                    app.Stop();
                }
            }, c);
            Assert.IsFalse(c.HasFocus);
            await app.SendKey(new ConsoleKeyInfo('!', ConsoleKey.Enter, false, false, false));
            Assert.IsFalse(detected);
            c.Focus();
            Assert.IsTrue(c.HasFocus);
            await app.SendKey(new ConsoleKeyInfo('!', ConsoleKey.Enter, false, false, false));
            Assert.IsTrue(detected);
            app.Stop();
        });

        app.Run();
        Assert.IsTrue(detected);
    }

    [TestMethod]
    public void ConsoleApp_SetTimeout()
    {
        var app = new ConsoleApp();
        var count = 0;
        app.Invoke(() =>
        {
            app.SetTimeout(() => { count++; app.Stop(); }, TimeSpan.FromMilliseconds(50));
        });
        app.Run();
        Assert.AreEqual(1, count);
    }



    [TestMethod]
    public void ConsoleApp_SetInterval()
    {
        var app = new ConsoleApp();
       
        var count = 0;
        app.Invoke(() =>
        {
            app.SetInterval(() => { count++; if (count == 5) { app.Stop(); } }, TimeSpan.FromMilliseconds(50));
        });

        app.Run();
        Assert.AreEqual(5, count);
    }

    [TestMethod]
    [Timeout(1000)]
    public void ConsoleApp_SetIntervalCancelling()
    {
        var app = new ConsoleApp();

        var count = 0;
        IDisposable handle = null;
        handle = app.SetInterval(async () =>
        {
            count++;
            if (count == 5)
            {
                handle.Dispose();
                await Task.Delay(20);
                app.Stop();
            }
        }, TimeSpan.FromMilliseconds(5));
        app.Run();
        Assert.AreEqual(5, count);
    }

    [TestMethod]
    public void ConsoleApp_BasicFocus() => AppTest.RunHeadless(TestContext.TestId(),async (context) =>
    { 
        Assert.IsNull(ConsoleApp.Current.FocusedControl);
        var c = ConsoleApp.Current.LayoutRoot.Add(new ConsoleControl() { CanFocus = true });
        Assert.IsFalse(c.HasFocus);
        c.Focus();
        Assert.AreSame(c, ConsoleApp.Current.FocusedControl);
        Assert.IsTrue(c.HasFocus);
        ConsoleApp.Current.ClearFocus();
        Assert.IsNull(ConsoleApp.Current.FocusedControl);
        Assert.IsFalse(c.HasFocus);
        ConsoleApp.Current.Stop();
     });

    [TestMethod]
    public void ConsoleApp_FocusStackPushSilencesControlsLowerLayers() => AppTest.RunHeadless(TestContext.TestId(), async (context) =>
    {
       var c = ConsoleApp.Current.LayoutRoot.Add(new ConsoleControl() { CanFocus = true });
        Assert.IsFalse(c.HasFocus);

        // set focus and expect it to be reflected in the API
        c.Focus();
        Assert.AreSame(c, ConsoleApp.Current.FocusedControl);
        Assert.IsTrue(c.HasFocus);

        // push to the focus stack and expect focus to be cleared
        var focusThief = ConsoleApp.Current.LayoutRoot.Add(new ConsoleControl() { CanFocus = false, FocusStackDepth = ConsoleApp.Current.LayoutRoot.FocusStackDepth + 1 });
        Assert.IsNull(ConsoleApp.Current.FocusedControl);
        Assert.IsFalse(c.HasFocus);

        // pop the focus stack and expect focus to be restored back to the control
        focusThief.Dispose();
        Assert.AreSame(c, ConsoleApp.Current.FocusedControl);
        Assert.IsTrue(c.HasFocus);

        ConsoleApp.Current.Stop();
    });


    [TestMethod]
    public void ConsoleApp_FocusStackPushSilencesGlobalKeyHandlersLowerLayers() => AppTest.RunHeadless(TestContext.TestId(), async (context) =>
    {
        // count starts at zero and should stay zero after subscribing
        var count = 0;
        ConsoleApp.Current.PushKeyForLifetime(ConsoleKey.Enter, () => count++, ConsoleApp.Current);
        Assert.AreEqual(0, count);

        // send a key and expect count to increment by 1
        await ConsoleApp.Current.SendKey(new ConsoleKeyInfo('!', ConsoleKey.Enter, false, false, false));
        Assert.AreEqual(1, count);

        // push the focus stack and expect the next key to be ignored
        var focusThief = ConsoleApp.Current.LayoutRoot.Add(new ConsoleControl() { CanFocus = false, FocusStackDepth = ConsoleApp.Current.LayoutRoot.FocusStackDepth + 1 });
        await ConsoleApp.Current.SendKey(new ConsoleKeyInfo('!', ConsoleKey.Enter, false, false, false));
        Assert.AreEqual(1, count);

        // pop the focus stack and expect the next key to increment count
        focusThief.Dispose();
        await ConsoleApp.Current.SendKey(new ConsoleKeyInfo('!', ConsoleKey.Enter, false, false, false));
        Assert.AreEqual(2, count);

        ConsoleApp.Current.Stop();
    });
 

    [TestMethod]
    public void ConsoleApp_FocusCycling() => AppTest.RunHeadless(TestContext.TestId(), async (context) =>
    {
        var controls = new ConsoleControl[5];
        for(var i = 0; i < controls.Length; i++)
        {
            controls[i] = ConsoleApp.Current.LayoutRoot.Add(new ConsoleControl() { CanFocus = true });
        }

        await ConsoleApp.Current.RequestPaintAsync();

        // cycle through each control and make sure it gets focus
        for(var i = 0; i < controls.Length; i++)
        {
            ConsoleApp.Current.MoveFocus();
            Assert.AreSame(controls[i], ConsoleApp.Current.FocusedControl);
        }

        // cycle through again to make sure it wraps around
        for (var i = 0; i < controls.Length; i++)
        {
            ConsoleApp.Current.MoveFocus();
            Assert.AreSame(controls[i], ConsoleApp.Current.FocusedControl);
        }

        // cycle backwards
        for (var i = controls.Length-2; i >= 0; i--)
        {
            ConsoleApp.Current.MoveFocus(forward: false);
            Assert.AreSame(controls[i], ConsoleApp.Current.FocusedControl);
        }

        // cycle backwards again to make sure backwards wrapping around works
        for (var i = controls.Length - 1; i >= 0; i--)
        {
            ConsoleApp.Current.MoveFocus(forward: false);
            Assert.AreSame(controls[i], ConsoleApp.Current.FocusedControl);
        }

        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Console_AppEnsureCantReuseControls()
    {
        ConsoleApp app = new ConsoleApp();
        app.Invoke(() =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel());
            var button = panel.Add(new Button());
            panel.Controls.Remove(button);

            try
            {
                app.LayoutRoot.Add(button);
                Assert.Fail("An exception should have been thrown");
            }
            catch (ObjectDisposedException ex)
            {
                Console.WriteLine(ex.Message);
                app.Stop();
            }
        });
        app.Run();
    }

    [TestMethod]
    [TestCategory(Categories.ConsoleApp)]
    public void ConsoleApp_LifecycleTestBasic()
    {
        ConsoleApp app = new ConsoleApp();
        app.Invoke(() =>
        {
            int addCounter = 0, removeCounter = 0;

            app.LayoutRoot.DescendentAdded.Subscribe((c) => { addCounter++; }, app);
            app.LayoutRoot.DescendentRemoved.Subscribe((c) => { removeCounter++; }, app);
            ConsolePanel panel = app.LayoutRoot.Add(new ConsolePanel());
            // direct child
            Assert.AreEqual(1, addCounter);
            Assert.AreEqual(0, removeCounter);

            var button = panel.Add(new Button());

            // grandchild
            Assert.AreEqual(2, addCounter);
            Assert.AreEqual(0, removeCounter);

            var innerPanel = new ConsolePanel();
            var innerInnerPanel = innerPanel.Add(new ConsolePanel());

            // no change since not added to the app yet
            Assert.AreEqual(2, addCounter);
            Assert.AreEqual(0, removeCounter);

            panel.Add(innerPanel);

            // both child and grandchild found on add
            Assert.AreEqual(4, addCounter);
            Assert.AreEqual(0, removeCounter);

            // remove a nested child
            innerPanel.Controls.Remove(innerInnerPanel);
            Assert.AreEqual(4, addCounter);
            Assert.AreEqual(1, removeCounter);

            app.LayoutRoot.Controls.Clear();
            Assert.AreEqual(4, addCounter);
            Assert.AreEqual(4, removeCounter);
            app.Stop();
        });
        app.Run();
    }
}

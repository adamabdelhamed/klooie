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
    public async Task ConsoleApp_ExceptionsPreservedAsync()
    {
        ConsoleApp app = new ConsoleApp();
        app.Invoke(() => throw new FormatException("Some fake exception"));
        try
        {
            await app.Start();
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
            c.KeyInputReceived.SubscribeForLifetime(k =>
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
        var promise = app.Start();
        var count = 0;
        app.SetTimeout(() => { count++; app.Stop(); }, TimeSpan.FromMilliseconds(50));
        promise.Wait();
        Assert.AreEqual(1, count);
    }



    [TestMethod]
    public void ConsoleApp_SetInterval()
    {
        var app = new ConsoleApp();
        var promise = app.Start();
        var count = 0;
        app.SetInterval(() => { count++; if (count == 5) { app.Stop(); } }, TimeSpan.FromMilliseconds(50));
        promise.Wait();
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
    public void ConsoleApp_BasicFocus()
    {
        var app = new ConsoleApp();
        app.Invoke(() =>
        {
            Assert.IsNull(app.FocusedControl);
            var c = app.LayoutRoot.Add(new ConsoleControl() { CanFocus = true });
            Assert.IsFalse(c.HasFocus);
            c.Focus();
            Assert.AreSame(c, app.FocusedControl);
            Assert.IsTrue(c.HasFocus);
            app.ClearFocus();
            Assert.IsNull(app.FocusedControl);
            Assert.IsFalse(c.HasFocus);
            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void ConsoleApp_FocusStackPushSilencesControlsLowerLayers()
    {
        var app = new ConsoleApp();
        app.Invoke(() =>
        {
            var c = app.LayoutRoot.Add(new ConsoleControl() { CanFocus = true });
            Assert.IsFalse(c.HasFocus);

            // set focus and expect it to be reflected in the API
            c.Focus();
            Assert.AreSame(c, app.FocusedControl);
            Assert.IsTrue(c.HasFocus);

            // push to the focus stack and expect focus to be cleared
            app.PushFocusStack();
            Assert.IsNull(app.FocusedControl);
            Assert.IsFalse(c.HasFocus);

            // pop the focus stack and expect focus to be restored back to the control
            app.PopFocusStack();
            Assert.AreSame(c, app.FocusedControl);
            Assert.IsTrue(c.HasFocus);

            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void ConsoleApp_FocusStackPushSilencesGlobalKeyHandlersLowerLayers()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            // count starts at zero and should stay zero after subscribing
            var count = 0;
            app.PushKeyForLifetime(ConsoleKey.Enter, () => count++, app);
            Assert.AreEqual(0, count);

            // send a key and expect count to increment by 1
            await app.SendKey(new ConsoleKeyInfo('!', ConsoleKey.Enter, false, false, false));
            Assert.AreEqual(1, count);

            // push the focus stack and expect the next key to be ignored
            app.PushFocusStack();
            await app.SendKey(new ConsoleKeyInfo('!', ConsoleKey.Enter, false, false, false));
            Assert.AreEqual(1, count);

            // pop the focus stack and expect the next key to increment count
            app.PopFocusStack();
            await app.SendKey(new ConsoleKeyInfo('!', ConsoleKey.Enter, false, false, false));
            Assert.AreEqual(2, count);

            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void ConsoleApp_FocusCycling()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var controls = new ConsoleControl[5];
            for(var i = 0; i < controls.Length; i++)
            {
                controls[i] = app.LayoutRoot.Add(new ConsoleControl() { CanFocus = true });
            }

            await app.RequestPaintAsync();

            // cycle through each control and make sure it gets focus
            for(var i = 0; i < controls.Length; i++)
            {
                app.MoveFocus();
                Assert.AreSame(controls[i], app.FocusedControl);
            }

            // cycle through again to make sure it wraps around
            for (var i = 0; i < controls.Length; i++)
            {
                app.MoveFocus();
                Assert.AreSame(controls[i], app.FocusedControl);
            }

            // cycle backwards
            for (var i = controls.Length-2; i >= 0; i--)
            {
                app.MoveFocus(forward: false);
                Assert.AreSame(controls[i], app.FocusedControl);
            }

            // cycle backwards again to make sure backwards wrapping around works
            for (var i = controls.Length - 1; i >= 0; i--)
            {
                app.MoveFocus(forward: false);
                Assert.AreSame(controls[i], app.FocusedControl);
            }

            app.Stop();
        });
        app.Run();
    }
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using PowerArgs.Cli;
using System;
using System.Linq;
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
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class LayoutTests
{
    [TestInitialize]
    public void Setup() => UITestManager.SetConsoleSize(80, 50);

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Layout_FillBasic()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var c = app.LayoutRoot.Add(new ConsoleControl() { Width = 1, Height = 1 });
            Assert.AreNotEqual(c.Width, app.Width);
            Assert.AreNotEqual(c.Height, app.Height);
            c.Fill();
            Assert.AreEqual(c.Width, app.Width);
            Assert.AreEqual(c.Height, app.Height);
            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_FillSync()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var c = panel.Add(new ConsoleControl()).Fill();
            Assert.AreEqual(9, panel.Width);
            Assert.AreEqual(10, panel.Height);
            Assert.AreEqual(c.Width, panel.Width);
            Assert.AreEqual(c.Height, panel.Height);
            panel.ResizeBy(1, 1);
            Assert.AreEqual(c.Width, panel.Width);
            Assert.AreEqual(c.Height, panel.Height);

            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_FillMaxSmallerThanParent()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            Assert.IsTrue(app.LayoutRoot.Width > 1);
            Assert.IsTrue(app.LayoutRoot.Height > 1);

            var c = app.LayoutRoot.Add(new ConsoleControl() { Width = 1, Height = 1 });
            Assert.AreNotEqual(c.Width, app.Width);
            Assert.AreNotEqual(c.Height, app.Height);

            var maxW = app.Width / 2;
            var maxH = app.Height / 2;
            
            c.FillMax(maxW, maxH);
            Assert.AreEqual(maxW, c.Width);
            Assert.AreEqual(maxH, c.Height);

            AssertCentered(c);
            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_FillMaxSameSizeAsParent()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            Assert.IsTrue(app.LayoutRoot.Width > 1);
            Assert.IsTrue(app.LayoutRoot.Height > 1);

            var c = app.LayoutRoot.Add(new ConsoleControl() { Width = 1, Height = 1 });
            Assert.AreNotEqual(c.Width, app.Width);
            Assert.AreNotEqual(c.Height, app.Height);

            var maxW = app.Width;
            var maxH = app.Height;

            c.FillMax(maxW, maxH);
            Assert.AreEqual(c.Parent.Width, c.Width);
            Assert.AreEqual(c.Parent.Height, c.Height);
            AssertCentered(c);
            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_FillMaxLargerThanParent()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            Assert.IsTrue(app.LayoutRoot.Width > 1);
            Assert.IsTrue(app.LayoutRoot.Height > 1);

            var c = app.LayoutRoot.Add(new ConsoleControl() { Width = 1, Height = 1 });
            Assert.AreNotEqual(c.Width, app.Width);
            Assert.AreNotEqual(c.Height, app.Height);

            var maxW = app.Width * 2;
            var maxH = app.Height * 2;

            c.FillMax(maxW, maxH);
            Assert.AreEqual(c.Parent.Width, c.Width);
            Assert.AreEqual(c.Parent.Height, c.Height);
            AssertCentered(c);
            app.Stop();
        });
        app.Run();
    }

    private void AssertCentered(ConsoleControl c)
    {
        var expectedLeft = ConsoleMath.Round((c.Parent.Width - c.Width) / 2f);
        var expectedTop = ConsoleMath.Round((c.Parent.Height - c.Height) / 2f);
        Assert.AreEqual(expectedLeft, c.Left);
        Assert.AreEqual(expectedTop, c.Top);
    }

    [TestMethod]
    public void Layout_FillDeferred()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            try
            {
                var c = new ConsoleControl().Fill();
                Assert.Fail("ArgumentException should have been thrown");
            }
            catch(ArgumentException)
            {

            }

            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_FillPadded()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var thickness = new Thickness(1, 2, 3, 4);
            Assert.AreEqual(3, thickness.HorizontalPadding);
            Assert.AreEqual(7, thickness.VerticalPadding);
            var c = panel.Add(new ConsoleControl()).Fill(padding: thickness);
            Assert.AreEqual(9, panel.Width);
            Assert.AreEqual(10, panel.Height);
            Assert.AreEqual(c.Width, panel.Width - thickness.HorizontalPadding);
            Assert.AreEqual(c.Height, panel.Height - thickness.VerticalPadding);
            panel.ResizeBy(1, 1);
            Assert.AreEqual(c.Width, panel.Width - thickness.HorizontalPadding);
            Assert.AreEqual(c.Height, panel.Height - thickness.VerticalPadding);

            app.Stop();
        });
        app.Run();
    }


    [TestMethod]
    public void Layout_FillHorizontally()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var c = panel.Add(new ConsoleControl() { Width = 1, Height = 1 }).FillHorizontally();
            Assert.AreEqual(9, panel.Width);
            Assert.AreEqual(10, panel.Height);
            Assert.AreEqual(c.Width, panel.Width);
            Assert.AreEqual(1, c.Height);
            panel.ResizeBy(1, 1);
            Assert.AreEqual(11, panel.Height);
            Assert.AreEqual(c.Width, panel.Width);
            Assert.AreEqual(1, c.Height);

            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_FillVertically()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 10, Height = 9 });
            var c = panel.Add(new ConsoleControl() { Width = 1, Height = 1 }).FillVertically();
            Assert.AreEqual(9, panel.Height);
            Assert.AreEqual(10, panel.Width);
            Assert.AreEqual(c.Height, panel.Height);
            Assert.AreEqual(1, c.Width);
            panel.ResizeBy(1, 1);
            Assert.AreEqual(11, panel.Width);
            Assert.AreEqual(c.Height, panel.Height);
            Assert.AreEqual(1, c.Width);

            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_FillHorizontallyPadded()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var padding = 2;
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var c = panel.Add(new ConsoleControl() { Width = 1, Height = 1 }).FillHorizontally(new Thickness(padding,padding,0,0));
            Assert.AreEqual(9, panel.Width);
            Assert.AreEqual(10, panel.Height);
            Assert.AreEqual(c.Width, panel.Width-2*padding);
            Assert.AreEqual(1, c.Height);
            panel.ResizeBy(1, 1);
            Assert.AreEqual(c.Width, panel.Width - 2 * padding);
            Assert.AreEqual(1, c.Height);

            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_FillVerticallyPadded()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var padding = 2;
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var c = panel.Add(new ConsoleControl() { Width = 1, Height = 1 }).FillVertically(new Thickness(0,0, padding, padding));
            Assert.AreEqual(9, panel.Width);
            Assert.AreEqual(10, panel.Height);
            Assert.AreEqual(c.Height, panel.Height - 2 * padding);
            Assert.AreEqual(1, c.Width);
            panel.ResizeBy(1, 1);
            Assert.AreEqual(c.Height, panel.Height - 2 * padding);
            Assert.AreEqual(1, c.Width);

            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_DockToRight()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var c = panel.Add(new ConsoleControl()).DockToRight();
            Assert.AreEqual(panel.Width - c.Width, c.X);
            await app.RequestPaintAsync();
            panel.Width++;
            Assert.AreEqual(panel.Width - c.Width, c.X);

            var panel2 = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var padding = 1;
            var c2 = panel2.Add(new ConsoleControl()).DockToRight(padding: padding);
            Assert.AreEqual(panel2.Width - (c2.Width + padding), c2.X);
            await app.RequestPaintAsync();
            panel2.Width++;
            Assert.AreEqual(panel2.Width - (c2.Width + padding), c2.X);

            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_DockToBottom()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var c = panel.Add(new ConsoleControl()).DockToBottom();
            Assert.AreEqual(panel.Height - c.Height, c.Y);
            await app.RequestPaintAsync();
            panel.Height++;
            Assert.AreEqual(panel.Height - c.Height, c.Y);

            var panel2 = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var padding = 1;
            var c2 = panel2.Add(new ConsoleControl()).DockToBottom(padding: padding);
            Assert.AreEqual(panel2.Height - (c2.Height + padding), c2.Y);
            await app.RequestPaintAsync();
            panel2.Height++;
            Assert.AreEqual(panel2.Height - (c2.Height + padding), c2.Y);

            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_DockToLeft()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var c = panel.Add(new ConsoleControl()).DockToLeft();
            Assert.AreEqual(0, c.X);
            await app.RequestPaintAsync();
            panel.Width++;
            Assert.AreEqual(0, c.X);

            var panel2 = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var padding = 1;
            var c2 = panel2.Add(new ConsoleControl()).DockToLeft(padding: padding);
            Assert.AreEqual(padding, c2.X);
            await app.RequestPaintAsync();
            panel2.Width++;
            Assert.AreEqual(padding, c2.X);

            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_DockToTop()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var c = panel.Add(new ConsoleControl()).DockToTop();
            Assert.AreEqual(0, c.Y);
            await app.RequestPaintAsync();
            panel.Height++;
            Assert.AreEqual(0, c.Y);

            var panel2 = app.LayoutRoot.Add(new ConsolePanel() { Width = 9, Height = 10 });
            var padding = 1;
            var c2 = panel2.Add(new ConsoleControl()).DockToTop(padding: padding);
            Assert.AreEqual(padding, c2.Y);
            await app.RequestPaintAsync();
            panel2.Height++;
            Assert.AreEqual(padding, c2.Y);

            app.Stop();
        });
        app.Run();
    }


    [TestMethod]
    public void Layout_CenterBoth()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 10, Height = 9 });
            var c = panel.Add(new ConsoleControl() { Width = 1, Height = 1 }).CenterBoth();
            Assert.AreEqual(ConsoleMath.Round((panel.Width - c.Width) / 2f), c.Left);
            Assert.AreEqual(ConsoleMath.Round((panel.Height - c.Height) / 2f), c.Top);
            panel.ResizeBy(1, 1);
            Assert.AreEqual(ConsoleMath.Round((panel.Width - c.Width) / 2f), c.Left);
            Assert.AreEqual(ConsoleMath.Round((panel.Height - c.Height) / 2f), c.Top);
            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_CenterHorizontally()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 10, Height = 9 });
            var c = panel.Add(new ConsoleControl() { Width = 1, Height = 1 }).CenterHorizontally();
            Assert.AreEqual(ConsoleMath.Round((panel.Width - c.Width) / 2f), c.Left);
            Assert.AreEqual(0, c.Top);
            panel.ResizeBy(1, 1);
            Assert.AreEqual(ConsoleMath.Round((panel.Width - c.Width) / 2f), c.Left);
            Assert.AreEqual(0, c.Top);
            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void Layout_CenterVertically()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var panel = app.LayoutRoot.Add(new ConsolePanel() { Width = 10, Height = 9 });
            var c = panel.Add(new ConsoleControl() { Width = 1, Height = 1 }).CenterVertically();
            Assert.AreEqual(0, c.Left);
            Assert.AreEqual(ConsoleMath.Round((panel.Height - c.Height) / 2f), c.Top);
            panel.ResizeBy(1, 1);
            Assert.AreEqual(0, c.Left);
            Assert.AreEqual(ConsoleMath.Round((panel.Height - c.Height) / 2f), c.Top);
            app.Stop();
        });
        app.Run();
    }
}

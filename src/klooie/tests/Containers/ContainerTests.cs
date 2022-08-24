using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System.Linq;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class ContainerTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Container_CompositionPaintOver()
    {
        var app = new ConsoleApp();

        app.Invoke(async () =>
        {
            var bottomYellowTextWithMagentaBg = app.LayoutRoot.Add(new Label() { ZIndex = 0, Text = "Text".ToYellow(RGB.Magenta) }).Fill();
            var topRedTextWithGreenBg = app.LayoutRoot.Add(new Label() { CompositionMode = CompositionMode.PaintOver, ZIndex = 1, Text = "Text".ToRed(RGB.Green) }).Fill();
            await app.RequestPaintAsync();

            for (var x = 0; x < topRedTextWithGreenBg.Text.Length; x++)
            {
                Assert.AreEqual(topRedTextWithGreenBg.Text[x], app.LayoutRoot.Bitmap.GetPixel(x, 0));
            }

            app.Stop();
        });

        app.Run();
    }

    [TestMethod]
    public void Container_CompositionBlendBackground()
    {
        var app = new ConsoleApp();

        app.Invoke(async () =>
        {
            var bottomYellowTextWithMagentaBg = app.LayoutRoot.Add(new Label() { ZIndex = 0, Text = "Text".ToYellow(RGB.Magenta) }).Fill();
            var topRedTextWithGreenBg = app.LayoutRoot.Add(new Label() { CompositionMode = CompositionMode.BlendBackground, ZIndex = 1, Text = "Text".ToRed(RGB.Green) }).Fill();
            await app.RequestPaintAsync();

            for (var x = 0; x < topRedTextWithGreenBg.Text.Length; x++)
            {
                Assert.AreEqual(new ConsoleCharacter(topRedTextWithGreenBg.Text[x].Value,RGB.Red,RGB.Magenta), app.LayoutRoot.Bitmap.GetPixel(x, 0));
            }

            app.Stop();
        });

        app.Run();
    }

    [TestMethod]
    public void Container_CompositionBlendVisible()
    {
        var app = new ConsoleApp();

        app.Invoke(async () =>
        {
            var bottomYellowTextWithMagentaBg = app.LayoutRoot.Add(new Label() { ZIndex = 0, Text = "Text".ToYellow(RGB.Magenta) }).Fill();
            var topMostlyBlankTextWithPanelColoredBG = app.LayoutRoot.Add(new Label() { CompositionMode = CompositionMode.BlendVisible, ZIndex = 1, Text = "1   ".ToConsoleString(RGB.DarkGreen, app.LayoutRoot.Background) }).Fill();
            await app.RequestPaintAsync();

            for (var x = 0; x < topMostlyBlankTextWithPanelColoredBG.Text.Length; x++)
            {
                if (x == 0)
                {
                    Assert.AreEqual(topMostlyBlankTextWithPanelColoredBG.Text[x], app.LayoutRoot.Bitmap.GetPixel(x, 0));
                }
                else
                {
                    Assert.AreEqual(bottomYellowTextWithMagentaBg.Text[x], app.LayoutRoot.Bitmap.GetPixel(x, 0));
                }
            }

            app.Stop();
        });

        app.Run();
    }

    [TestMethod]
    public void Container_ChildrenAndDescendents()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var p1 = app.LayoutRoot.Add(new ConsolePanel()).Fill();
            var p2 = p1.Add(new ConsolePanel()).Fill();
            Assert.AreEqual(1, app.LayoutRoot.Children.Count());
            Assert.AreSame(p1, app.LayoutRoot.Children.Single());

            Assert.AreEqual(2, app.LayoutRoot.Descendents.Count());
            Assert.AreSame(p2, app.LayoutRoot.Descendents.Last());

            Assert.AreEqual(1, p1.Descendents.Count());
            Assert.AreSame(p2, p1.Descendents.Single());

            Assert.AreEqual(0, p2.Children.Count());
            Assert.AreEqual(0, p2.Descendents.Count());
            app.Stop();
        });
        app.Run();
    }
}

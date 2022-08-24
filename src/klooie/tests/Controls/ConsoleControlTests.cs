using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using PowerArgs.Cli;
using System.Collections.Generic;
using System.Reflection;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class ConsoleControlTests
{
    public TestContext TestContext { get; set; }

    private static HashSet<string>? GetTagsField(ConsoleControl c) =>
         c?.GetType()?.GetField("tags", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(c) as HashSet<string>;

    [TestMethod]
    public void ConsoleControl_TagsLazy()
    {
        using (var c = new ConsoleControl())
        {
            Assert.IsNull(GetTagsField(c));
            Assert.IsFalse(c.HasSimpleTag("foo"));
            Assert.IsFalse(c.HasValueTag("foo"));
            Assert.IsFalse(c.RemoveTag("foo"));
            Assert.IsNull(GetTagsField(c));
            c.AddTag("foo");
            Assert.IsNotNull(GetTagsField(c));
            Assert.IsTrue(c.HasSimpleTag("foo"));
            Assert.IsFalse(c.HasValueTag("foo"));
            Assert.IsTrue(c.RemoveTag("foo"));
            Assert.IsFalse(c.HasSimpleTag("foo"));
        }
    }

    [TestMethod]
    public void ConsoleControl_ValueTags()
    {
        using (var c = new ConsoleControl())
        {
            Assert.IsFalse(c.HasValueTag("name"));
            c.AddValueTag("name", "Adam");
            Assert.IsTrue(c.HasValueTag("name"));
            Assert.IsFalse(c.HasSimpleTag("name"));
            Assert.IsTrue(c.TryGetTagValue("name", out string value));
            Assert.AreEqual("Adam", value);
        }
    }

    [TestMethod]
    public void TestAbsolutePositioning()
    {
        var app = new ConsoleApp();
        app.Invoke(() =>
        {
            var innerPanel = app.LayoutRoot.Add(new ConsolePanel() { X = 2, Y = 3, Width = 100, Height = 100 });
            var control = innerPanel.Add(new ConsoleControl() { X = 3, Y = 4 });
            Assert.AreEqual(5, control.AbsoluteX);
            Assert.AreEqual(7, control.AbsoluteY);
            app.Stop();
        });
        app.Run();
    }

    [TestMethod]
    public void TestFilters()
    {
        var app = new ConsoleApp();
        app.Invoke(async () =>
        {
            var redFilter = new BackgroundColorFilter(RGB.Red);
            var greenFilter = new BackgroundColorFilter(RGB.Green);

            var control1 = app.LayoutRoot.Add(new ConsoleControl() { X = 0, Y = 0, Width = 1, Height = 1 });
            var control2 = app.LayoutRoot.Add(new ConsoleControl() { X = 1, Y = 0, Width = 1, Height = 1 });
            var control3 = app.LayoutRoot.Add(new ConsoleControl() { X = 2, Y = 0, Width = 1, Height = 1 });

            control1.Filters.Add(redFilter);
            control2.Filters.Add(greenFilter);

            await app.RequestPaintAsync();

            Assert.AreEqual(RGB.Red, control1.Bitmap.GetPixel(0, 0).BackgroundColor);
            Assert.AreEqual(RGB.Green, control2.Bitmap.GetPixel(0, 0).BackgroundColor);
            Assert.AreEqual(ConsoleString.DefaultBackgroundColor, control3.Bitmap.GetPixel(0, 0).BackgroundColor);

            control1.Filters.Clear();
            control2.Filters.Clear();

            await app.RequestPaintAsync();
            Assert.AreEqual(ConsoleString.DefaultBackgroundColor, control1.Bitmap.GetPixel(0, 0).BackgroundColor);
            Assert.AreEqual(ConsoleString.DefaultBackgroundColor, control2.Bitmap.GetPixel(0, 0).BackgroundColor);
            Assert.AreEqual(ConsoleString.DefaultBackgroundColor, control3.Bitmap.GetPixel(0, 0).BackgroundColor);

            app.Stop();
        });
        app.Run();
    }
}

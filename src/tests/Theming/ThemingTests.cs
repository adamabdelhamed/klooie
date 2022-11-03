using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using klooie.Theming;
using System.Linq;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Theming)]
public class ThemingTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Theming_TextBox() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var tb = ConsoleApp.Current.LayoutRoot.Add(new TextBox() { Width = 20, Value = "Adam".ToConsoleString() }).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();
        var t = Theme.FromStyles(StyleBuilder.Create().For<TextBox>().FG(RGB.Red).BG(RGB.Green));
        Assert.AreEqual(2, t.WhereNeverApplied().Count());
        t.Apply();
        Assert.AreEqual(0, t.WhereNeverApplied().Count());
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });
 

    [TestMethod]
    public void Theming_Builder_Basic()
    {
        var styles = StyleBuilder.Create()
            .For<Container>().BG(RGB.Yellow)
            .ToArray();

        Assert.AreEqual(1, styles.Length);
        Assert.AreEqual(typeof(Container), styles[0].Type);
        Assert.AreEqual(nameof(Container.Background), styles[0].PropertyName);
        Assert.AreEqual(RGB.Yellow, styles[0].Value);
        Assert.IsNull(styles[0].Tag);
        Assert.IsNull(styles[0].Within);
        Assert.IsNull(styles[0].WithinTag);
    }

    [TestMethod]
    public void Theming_Builder_WithAll()
    {
        var styles = StyleBuilder.Create()
            .For<Container>()
                .Within<ConsolePanel>().Tag("theTag").WithinTag("parentTag").BG(RGB.Yellow)
            .ToArray();

        Assert.AreEqual(1, styles.Length);
        Assert.AreEqual(typeof(Container), styles[0].Type);
        Assert.AreEqual(nameof(Container.Background), styles[0].PropertyName);
        Assert.AreEqual(RGB.Yellow, styles[0].Value);
        Assert.AreEqual("theTag", styles[0].Tag);
        Assert.AreEqual(typeof(ConsolePanel), styles[0].Within);
        Assert.AreEqual("parentTag", styles[0].WithinTag);
    }

    [TestMethod]
    public void Theming_Builder_Clear()
    {
        var styles = StyleBuilder.Create()
            .For<Container>()
                .Within<ConsolePanel>().Tag("theTag").WithinTag("parentTag").BG(RGB.Yellow).Clear()
                .Within<BorderPanel>().BG(RGB.Red)
            .ToArray();

        Assert.AreEqual(2, styles.Length);
        Assert.AreEqual(typeof(Container), styles[1].Type);
        Assert.AreEqual(nameof(Container.Background), styles[1].PropertyName);
        Assert.AreEqual(RGB.Red, styles[1].Value);
        Assert.AreEqual(null, styles[1].Tag);
        Assert.AreEqual(typeof(BorderPanel), styles[1].Within);
        Assert.AreEqual(null, styles[1].WithinTag);
    }

    [TestMethod]
    public void Theming_Builder_Pop()
    {
        var styles = StyleBuilder.Create()
            .For<Container>().Within<BorderPanel>()
                .Tag("t1").BG(RGB.Yellow).Pop()
                .Tag("t2").BG(RGB.Red)
            .ToArray();

        Assert.AreEqual(2, styles.Length);
        Assert.AreEqual(typeof(Container), styles[0].Type);
        Assert.AreEqual(nameof(Container.Background), styles[0].PropertyName);
        Assert.AreEqual(typeof(Container), styles[1].Type);
        Assert.AreEqual(nameof(Container.Background), styles[1].PropertyName);

        Assert.AreEqual(RGB.Yellow, styles[0].Value);
        Assert.AreEqual("t1", styles[0].Tag);
        Assert.AreEqual(typeof(BorderPanel), styles[0].Within);
        Assert.AreEqual(null, styles[0].WithinTag);

        Assert.AreEqual(RGB.Red, styles[1].Value);
        Assert.AreEqual("t2", styles[1].Tag);
        Assert.AreEqual(typeof(BorderPanel), styles[1].Within);
        Assert.AreEqual(null, styles[1].WithinTag);
    }

    [TestMethod]
    public void Theming_Builder_Validation() => ExpectInvalid(() =>
    {
        // didn't set a target
        StyleBuilder.Create().BG(RGB.Red).ToArray();

        // didn't add any styles for Container
        StyleBuilder.Create().For<Container>().For<TextBox>().ToArray();

        // didn't add any styles for 't1'
        StyleBuilder.Create().For<Container>().BG(RGB.Red).Tag("t1").For<TextBox>().ToArray();

        // can't set qualifier twice
        StyleBuilder.Create().For<Container>().Tag("t1").Tag("t2").BG(RGB.Red).ToArray();
        StyleBuilder.Create().For<Container>().WithinTag("t1").WithinTag("t2").BG(RGB.Red).ToArray();
        StyleBuilder.Create().For<Container>().Within<ConsolePanel>().Within<BorderPanel>().BG(RGB.Red).ToArray();
    });

    [TestMethod]
    public void Theming_NeverApplied() => AppTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
    {
        await ConsoleApp.Current.RequestPaintAsync();
        var t = Theme.FromStyles(StyleBuilder.Create().For<TextBox>().FG(RGB.Red).BG(RGB.Green));
        Assert.AreEqual(2, t.WhereNeverApplied().Count());
        t.Apply();
        await ConsoleApp.Current.RequestPaintAsync();
        Assert.AreEqual(2, t.WhereNeverApplied().Count());
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Theming_Extension() => AppTest.RunHeadless(async () =>
    {
        var custom = ConsoleApp.Current.LayoutRoot.Add(new TestThemeableControl());
        custom.CustomThemeableColor = RGB.Red;
        Assert.AreEqual(RGB.Red, custom.CustomThemeableColor);

        var styles = StyleBuilder.Create()
            .For<ConsoleControl>().BG(RGB.White)
            .ForX<TestThemeableControl>().CustomThemeable(RGB.Magenta)
            .ToArray();

        Assert.AreEqual(2, styles.Length);
        Assert.IsTrue(styles[0].Type == typeof(ConsoleControl));

        var theme = Theme.FromStyles(styles);
        theme.Apply();
        Assert.AreEqual(RGB.Magenta, custom.CustomThemeableColor);
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void Theming_Ignore() => AppTest.RunHeadless(async () =>
    {
        var custom = ConsoleApp.Current.LayoutRoot.Add(new TestThemeableControl());
        var theme = Theme.FromStyles(StyleBuilder.Create().For<Label>().BG(RGB.White));
        theme.Apply();
        Assert.AreEqual(RGB.Magenta, custom.Descendents.WhereAs<Label>().Single().Background);
        ConsoleApp.Current.Stop();
    });

    private void ExpectInvalid(Action a)
    {
        try
        {
            a();
            Assert.Fail($"An exception of type {nameof(InvalidOperationException)} should have been thrown");
        }
        catch(InvalidOperationException)
        {

        }
    }
}


[ThemeIgnore(typeof(Label))]
public class TestThemeableControl : ProtectedConsolePanel
{
    public RGB CustomThemeableColor { get => Get<RGB>(); set => Set(value); }
 
    public TestThemeableControl()
    {
        ProtectedPanel.Add(new Label() { Background = RGB.Magenta });
    }

    public class Builder : StyleBuilder<Builder>
    {
        public Builder(StyleBuilder toWrap) : base(toWrap) { }
        public StyleBuilder<Builder> CustomThemeable(RGB color) => Property(nameof(CustomThemeableColor), color);
    }
}

public static class TestThemeableControlExtensions
{
    public static TestThemeableControl.Builder ForX<T>(this StyleBuilder builder) where T : TestThemeableControl =>
        new TestThemeableControl.Builder(builder).For<T>();
}
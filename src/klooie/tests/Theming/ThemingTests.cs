using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using klooie.Theming;
namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Theming)]
public class ThemingTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void Theming_TextBox()
    {
        var app = new KlooieTestHarness(TestContext, true);

        app.InvokeNextCycle(async () =>
        {
            var tb = app.LayoutRoot.Add(new TextBox() { Width = 20, Value = "Adam".ToConsoleString() }).CenterBoth();
            await app.PaintAndRecordKeyFrameAsync();
            Theme.FromStyles(StyleBuilder.Create().For<TextBox>().FG(RGB.Red).BG(RGB.Green)).Apply();
            await app.PaintAndRecordKeyFrameAsync();
            app.Stop();
        });

        app.Run();
        app.AssertThisTestMatchesLKG();
    }

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
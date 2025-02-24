using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class TabControlTests
{
    public TestContext TestContext { get; set; }

     
    [TestMethod]
    public void TabControl_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var options = new TabControlOptions("Tab1", "Tab2", "Tab3")
        {
            BodyFactory = TabBodyFactory
        };

        var tabControl = ConsoleApp.Current.LayoutRoot.Add(new TabControl(options) { Background = new RGB(30,30,30) }).Fill();
        await context.PaintAndRecordKeyFrameAsync();
        for (var i = 0; i < 6; i++)
        {
            await ConsoleApp.Current.SendKey(System.ConsoleKey.Tab);
            await context.PaintAndRecordKeyFrameAsync();
        }

        for (var i = 0; i < 5; i++)
        {
            await ConsoleApp.Current.SendKey(System.ConsoleKey.Tab, shift: true);
            await context.PaintAndRecordKeyFrameAsync();
        }

        for (var i = 0; i < 5; i++)
        {
            await ConsoleApp.Current.SendKey(System.ConsoleKey.RightArrow);
            await context.PaintAndRecordKeyFrameAsync();
        }

        for (var i = 0; i < 5; i++)
        {
            await ConsoleApp.Current.SendKey(System.ConsoleKey.LeftArrow);
            await context.PaintAndRecordKeyFrameAsync();
        }

        ConsoleApp.Current.Stop();
    });


    [TestMethod]
    public void TabControl_Connected() => AppTest.Run(TestContext.TestId(), UITestMode.Headless, async (context) =>
    {
        ConsoleControl body = null;
        var options = new TabControlOptions("Tab1")
        {
            BodyFactory = (s) => body = new ConsoleControl()
        };

        var tabControl = ConsoleApp.Current.LayoutRoot.Add(new TabControl(options) { Background = new RGB(30, 30, 30) }).Fill();
        await ConsoleApp.Current.RequestPaintAsync();
        Assert.IsNotNull(body);

        var parent = body.Parent;
        while (true)
        {
            if(parent.Parent == null) break;
            parent = parent.Parent;
        }
        Assert.AreSame(ConsoleApp.Current.LayoutRoot, parent);

        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void TabControl_LeftAligned() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var options = new TabControlOptions("Tab1", "Tab2", "Tab3")
        {
            TabAlignment = TabAlignment.Left,
            BodyFactory = TabBodyFactory,
        };

        var tabControl = ConsoleApp.Current.LayoutRoot.Add(new TabControl(options) { Background = new RGB(30, 30, 30) }).Fill();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });


    [TestMethod]
    public void TabControl_DynamicAddRemove() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var options = new TabControlOptions("Tab1", "Tab2", "Tab3")
        {
            BodyFactory = TabBodyFactory,
        };

        var tabControl = ConsoleApp.Current.LayoutRoot.Add(new TabControl(options) { Background = new RGB(30, 30, 30) }).Fill();
        await context.PaintAndRecordKeyFrameAsync();
        options.Tabs.Add("Tab4");
        await context.PaintAndRecordKeyFrameAsync();
        options.Tabs.RemoveAt(0);
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    private static ConsoleControl TabBodyFactory(string tabString)
    {
        var ret = new ConsolePanel() { Background = RGB.Gray };
        ret.Add(new Label(tabString.ToDarkGreen()) { CompositionMode = CompositionMode.BlendBackground }).CenterBoth();
        return ret;
    }

    [TestMethod]
    public void TabControl_DownArrow() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var options = new TabControlOptions("Tab1", "Tab2")
        {
            BodyFactory = s =>
            {
                var ret = new ConsolePanel();
                ret.Add(new Button() { Text = "Some Button".ToWhite() }).CenterBoth();
                return ret;
            },
        };

        var tabControl = ConsoleApp.Current.LayoutRoot.Add(new TabControl(options)).Fill();
        tabControl.Focus();
        await context.PaintAndRecordKeyFrameAsync();
        await ConsoleApp.Current.SendKey(System.ConsoleKey.DownArrow.KeyInfo());
        await context.PaintAndRecordKeyFrameAsync();
        Assert.IsTrue(ConsoleApp.Current.FocusedControl is Button);
        ConsoleApp.Current.Stop();
    });
}

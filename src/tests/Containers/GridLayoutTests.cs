using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class GridLayoutTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void GridLayout_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        context.SecondsBetweenKeyframes = .03f;
        var grid = ConsoleApp.Current.LayoutRoot.Add(new GridLayout("25%;50%;25%", "20%;1p;1r") { Width = 50, Height = 10  }).CenterBoth();

        var leftNav = grid.Add(new ConsoleControl() { Background = RGB.Red }, 0, 0, rowSpan: grid.NumRows);
        var splitter = grid.Add(new ConsoleControl() { Background = RGB.White }, 1, 0, rowSpan: grid.NumRows);
        var body = grid.Add(new ConsoleControl() { Background = RGB.Green }, 2, 0, rowSpan: grid.NumRows);

        var colSplitter = grid.Add(new ConsoleControl() { Background = RGB.Yellow }, 0, 1, columnSpan: grid.NumColumns);

        await context.PaintAndRecordKeyFrameAsync();
        while(grid.Height < ConsoleApp.Current.Height && grid.Width < ConsoleApp.Current.Width)
        {
            grid.ResizeBy(2, 1);
            await context.PaintAndRecordKeyFrameAsync();
        }

        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();

        grid.Remove(leftNav);
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();

        grid.Remove(splitter);
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();

        grid.Remove(body);
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();
        await context.PaintAndRecordKeyFrameAsync();

        grid.Remove(colSplitter);
        await context.PaintAndRecordKeyFrameAsync();

        ConsoleApp.Current.Stop();
    });
    
}

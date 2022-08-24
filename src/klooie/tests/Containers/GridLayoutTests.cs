using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class GridLayoutTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void GridLayout_Basic()
    {
        var app = new KlooieTestHarness(TestContext, true);
        app.SecondsBetweenKeyframes = .03f;
        app.Invoke(async () =>
        {
            var grid = app.LayoutRoot.Add(new GridLayout("25%;50%;25%", "20%;1p;1r") { Width = 50, Height = 10  }).CenterBoth();

            var leftNav = grid.Add(new ConsoleControl() { Background = RGB.Red }, 0, 0, rowSpan: grid.NumRows);
            var splitter = grid.Add(new ConsoleControl() { Background = RGB.White }, 1, 0, rowSpan: grid.NumRows);
            var body = grid.Add(new ConsoleControl() { Background = RGB.Green }, 2, 0, rowSpan: grid.NumRows);

            var colSplitter = grid.Add(new ConsoleControl() { Background = RGB.Yellow }, 0, 1, columnSpan: grid.NumColumns);

            await app.PaintAndRecordKeyFrameAsync();
            while(grid.Height < app.Height && grid.Width < app.Width)
            {
                grid.ResizeBy(2, 1);
                await app.PaintAndRecordKeyFrameAsync();
            }

            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();

            grid.Remove(leftNav);
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();

            grid.Remove(splitter);
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();

            grid.Remove(body);
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();
            await app.PaintAndRecordKeyFrameAsync();

            grid.Remove(colSplitter);
            await app.PaintAndRecordKeyFrameAsync();

            app.Stop();
        });

        app.Run();
        app.AssertThisTestMatchesLKG();
    }
}

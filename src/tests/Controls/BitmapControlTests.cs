using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;
using System.Threading.Tasks;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class BitmapControlTests
{
    public TestContext TestContext { get; set; }

    
    [TestMethod]
    public void BitmapControl_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var bitmap = ConsoleBitmap.Create(100, 100);
        bitmap.Fill(RGB.Magenta);
        bitmap.DrawString("0,0".ToBlack(RGB.Magenta), 0, 0);
        ConsoleApp.Current.LayoutRoot.Add(new BitmapControl(bitmap) { Width = 20, Height = 10 }).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });

    [TestMethod]
    public void BitmapControl_Offset() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var bitmap = ConsoleBitmap.Create(100, 100);
        bitmap.Fill(RGB.Magenta);
        bitmap.DrawString("0,0".ToBlack(RGB.Magenta), 0, 0); // should be invisible
        ConsoleApp.Current.LayoutRoot.Add(new BitmapControl(bitmap) { OffsetX = 2, OffsetY = 1, Width = 20, Height = 10 }).CenterBoth();
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });
}

using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.Drawing)]
public class ConsoleBitmapTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void ConsoleBitmap_DrawLines()
    {
        var bitmap = new ConsoleBitmap(80, 30);
        var centerX = bitmap.Width / 2;
        var centerY = bitmap.Height / 2;


        AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified,bitmap.Width, bitmap.Height, async (context) =>
        {
            ConsoleApp.Current.LayoutRoot.Add(new BitmapControl() { Bitmap = bitmap }).Fill();
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Gray), centerX, centerY, 0, centerY / 2);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Red), centerX, centerY, 0, 0);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Yellow), centerX, centerY, centerX / 2, 0);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Green), centerX, centerY, centerX, 0);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Magenta), centerX, centerY, (int)(bitmap.Width * .75), 0);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Cyan), centerX, centerY, bitmap.Width - 1, 0);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Gray), centerX, centerY, bitmap.Width - 1, centerY / 2);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.White), centerX, centerY, 0, bitmap.Height / 2);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Blue), centerX, centerY, bitmap.Width - 1, bitmap.Height / 2);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Gray), centerX, centerY, 0, (int)(bitmap.Height * .75));
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Red), centerX, centerY, 0, bitmap.Height - 1);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Yellow), centerX, centerY, centerX / 2, bitmap.Height - 1);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Green), centerX, centerY, centerX, bitmap.Height - 1);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Magenta), centerX, centerY, (int)(bitmap.Width * .75), bitmap.Height - 1);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Cyan), centerX, centerY, bitmap.Width - 1, bitmap.Height - 1);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Gray), centerX, centerY, bitmap.Width - 1, (int)(bitmap.Height * .75));

            await context.PaintAndRecordKeyFrameAsync();
            ConsoleApp.Current.Stop();
        });
    }

    [TestMethod]
    public void ConsoleBitmap_DrawLinesReverse()
    {
        var bitmap = new ConsoleBitmap(80, 30);
        var centerX = bitmap.Width / 2;
        var centerY = bitmap.Height / 2;


        AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified, bitmap.Width, bitmap.Height, async (context) =>
        {
            var app = ConsoleApp.Current;
            app.LayoutRoot.Add(new BitmapControl() { Bitmap = bitmap }).Fill();
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Gray), 0, centerY / 2, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();
            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Red), 0, 0, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();
            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Yellow), centerX / 2, 0, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();
            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Green), centerX, centerY, centerX, 0);
            await context.PaintAndRecordKeyFrameAsync();
            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Magenta), (int)(bitmap.Width * .75), 0, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();
            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Cyan), bitmap.Width - 1, 0, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();
            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Gray), bitmap.Width - 1, centerY / 2, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.White), 0, bitmap.Height / 2, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Blue), bitmap.Width - 1, bitmap.Height / 2, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Gray), 0, (int)(bitmap.Height * .75), centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();
            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Red), 0, bitmap.Height - 1, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Yellow), centerX / 2, bitmap.Height - 1, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Green), centerX, bitmap.Height - 1, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Magenta), (int)(bitmap.Width * .75), bitmap.Height - 1, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Cyan), bitmap.Width - 1, bitmap.Height - 1, centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();

            bitmap.DrawLine(new ConsoleCharacter('X', ConsoleColor.Gray), bitmap.Width - 1, (int)(bitmap.Height * .75), centerX, centerY);
            await context.PaintAndRecordKeyFrameAsync();
            app.Stop();
        });
    }

    [TestMethod]
    public void ConsoleBitmap_DrawRect()
    {
        var bitmap = new ConsoleBitmap(80, 30);

        AppTest.RunCustomSize(TestContext.TestId(), UITestMode.KeyFramesVerified,80,30,async (context) =>
        {
            ConsoleApp.Current.LayoutRoot.Add(new BitmapControl() { Bitmap = bitmap }).Fill();
            var pen = new ConsoleCharacter('X', ConsoleColor.Green);
            for (var i = 0; i < 500000; i++)
            {
                bitmap.DrawRect(pen, 0, 0, bitmap.Width, bitmap.Height);
            }
            await context.PaintAndRecordKeyFrameAsync();
            ConsoleApp.Current.Stop();
        });
    }

    [TestMethod]
    public void ConsoleBitmap_Resize()
    {
        var bitmap = new ConsoleBitmap(2, 1);
        bitmap.Fill(RGB.Red);
        bitmap.Resize(4, 1);
        Assert.AreEqual(RGB.Red, bitmap.GetPixel(0, 0).BackgroundColor);
        Assert.AreEqual(RGB.Red, bitmap.GetPixel(1, 0).BackgroundColor);
        Assert.AreNotEqual(RGB.Red, bitmap.GetPixel(2, 0).BackgroundColor);
        Assert.AreNotEqual(RGB.Red, bitmap.GetPixel(3, 0).BackgroundColor);
    }

    [TestMethod]
    public void ConsoleBitmap_DrawStringH()
    {
        var bitmap = new ConsoleBitmap(3, 1);
        bitmap.DrawString("Adamab", -1, 0);
        Assert.AreEqual('d', bitmap.GetPixel(0, 0).Value);
        Assert.AreEqual('a', bitmap.GetPixel(1, 0).Value);
        Assert.AreEqual('m', bitmap.GetPixel(2, 0).Value);
    }

    [TestMethod]
    public void ConsoleBitmap_DrawStringV()
    {
        var bitmap = new ConsoleBitmap(1, 3);
        bitmap.DrawString("Adamab", 0, -1, true);
        Assert.AreEqual('d', bitmap.GetPixel(0, 0).Value);
        Assert.AreEqual('a', bitmap.GetPixel(0, 1).Value);
        Assert.AreEqual('m', bitmap.GetPixel(0, 2).Value);
    }

    [TestMethod]
    public void ConsoleBitmap_Clone()
    {
        var b = new ConsoleBitmap(2, 2);
        b.DrawPoint(new ConsoleCharacter('a'), 0, 0);
        b.DrawPoint(new ConsoleCharacter('b'), 1, 0);
        b.DrawPoint(new ConsoleCharacter('c'), 0, 1);
        b.DrawPoint(new ConsoleCharacter('d'), 1, 1);

        var clone = b.Clone();
        Assert.AreNotSame(b, clone);
        Assert.AreEqual(b, clone);
        Assert.AreEqual(b.Width, clone.Width);
        Assert.AreEqual(b.Height, clone.Height);
        for(var x = 0; x < b.Width; x++)
        {
            for(var y = 0; y < b.Height; y++)
            {
                Assert.AreEqual(b.GetPixel(x, y), clone.GetPixel(x, y));
            }
        }

        clone.SetPixel(1, 1, new ConsoleCharacter('e'));
        Assert.AreNotEqual(b, clone);
    }

    [TestMethod]
    public void ConsoleBitmap_Equality()
    {
        var a = new ConsoleBitmap(2, 1);
        var b = new ConsoleBitmap(2, 1);

        Assert.AreEqual(a, b);
        a.Fill(RGB.Green);
        Assert.AreNotEqual(a, b);
        b.Fill(RGB.Green);
        Assert.AreEqual(a, b);

        var small = new ConsoleBitmap(1, 1);
        var big = new ConsoleBitmap(2, 2);
        Assert.AreNotEqual(small, big);

        var aRed = new ConsoleBitmap(1, 1);
        var aGreen = new ConsoleBitmap(1, 1);
        aRed.DrawPoint(new ConsoleCharacter('a', RGB.Red), 0, 0);
        aGreen.DrawPoint(new ConsoleCharacter('a', RGB.Green), 0, 0);
        Assert.AreNotEqual(aRed, aGreen);

        var aRedBG = new ConsoleBitmap(1, 1);
        var aGreenBG = new ConsoleBitmap(1, 1);
        aRedBG.DrawPoint(new ConsoleCharacter('a', backgroundColor: RGB.Red), 0, 0);
        aGreenBG.DrawPoint(new ConsoleCharacter('a', backgroundColor: RGB.Green), 0, 0);
        Assert.AreNotEqual(aRedBG, aGreenBG);
    }
}

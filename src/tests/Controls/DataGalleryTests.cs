using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerArgs;
using System;

namespace klooie.tests;

[TestClass]
[TestCategory(Categories.ConsoleApp)]
public class DataGalleryTests
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void DataGallery_Basic() => AppTest.Run(TestContext.TestId(), UITestMode.KeyFramesVerified, async (context) =>
    {
        var gallery = ConsoleApp.Current.LayoutRoot.Add(new DataGallery<string>((str,index) =>
        {
            var tile = new ConsolePanel() { Width = 20, Height = 10, Background = new RGB(30, 30, 30) };
            tile.Add(new Label() { Text = str.ToWhite(), CompositionMode = CompositionMode.BlendBackground }).CenterBoth();
            return tile;
        })).Fill();
        gallery.Background = RGB.Black;

        gallery.Take = 4;
        gallery.Show(new string[]
        {
            "Item1",
            "Item2",
            "Item3",
            "Item4",
            "Item5",
            "Item6",
            "Item7",
            "Item8",
        });
        await context.PaintAndRecordKeyFrameAsync();
        gallery.Take = 100;
        gallery.Show(new string[]
        {
                "Item1",
                "Item2",
                "Item3",
                "Item4",
                "Item5",
                "Item6",
                "Item7",
                "Item8",
        });
        await context.PaintAndRecordKeyFrameAsync();
        ConsoleApp.Current.Stop();
    });
}

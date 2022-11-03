//#Sample -Id DataGallerySample
using PowerArgs;
namespace klooie.Samples;

public class DataGallerySample : ConsoleApp
{
    protected override async Task Startup()
    {
        var gallery = LayoutRoot.Add(new DataGallery<string>((theString, index) =>
        {
            var tile = new ConsolePanel() { Width = 18, Height = 8, Background = new RGB(20, 20, 20) };
            tile.Add(new Label(theString.ToOrange()) { CompositionMode = CompositionMode.BlendBackground }).CenterBoth();
            return tile;
        })
        { Background = new RGB(50,50,50) }).Fill();

        await Task.Delay(1500);
        var tiles = new string[]
        {
            "These",
            "tiles",
            "wrap",
            "nicely",
            "in",
            "a",
            "flow",
            "layout"
        };
        for (var i = 1; i <= tiles.Length; i++)
        {
            gallery.Show(tiles.Take(i));
            await Task.Delay(200);
        }
        await Task.Delay(1500);
        Stop();
    }
}

// Entry point for your application
public static class DataGallerySampleProgram
{
    public static void Main() => new DataGallerySample().Run();
}
//#EndSample

public class DataGallerySampleRunner : IRecordableSample
{
    public string OutputPath => @"Containers\DataGallerySample.gif";
    public int Width => 62;
    public int Height => 30;
    public ConsoleApp Define() => new DataGallerySample();

}
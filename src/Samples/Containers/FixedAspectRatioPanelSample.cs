//#Sample -Id FixedAspectRatioPanelSample
using PowerArgs;
namespace klooie.Samples;

public class FixedAspectRatioPanelSample : ConsoleApp
{
    protected override async Task Startup()
    {
        // this panel is resizable, simulating what happens in a real app where the user can
        // resize the window to whatever shape they prefer
        var resizablePanel = LayoutRoot.Add(new ConsolePanel() { Width = 1, Height = 1 });

        // this magenta colored panel is designed to only support a 2 * 1 aspect ratio
        var magentaPanelWithFixedAspectRatio = new ConsolePanel() { Background = RGB.Magenta };

        // We use a FixedAspectRatioPanel to host the magenta panel. As the resizable panel changes size
        // this fixed aspect ratio panel will keep our magenta panel at the correct aspect ratio.
        var whiteFixedAspectRatioPanel = resizablePanel.Add(new FixedAspectRatioPanel(2f / 1f, magentaPanelWithFixedAspectRatio) { Background = RGB.White }).Fill();

        // simulate resizing the window so we can see the fixed aspect ratio panel do its thing
        await resizablePanel.AnimateAsync(()=> LayoutRoot.Bounds, 3000, autoReverse: true);
        Stop();
    }
}

// Entry point for your application
public static class FixedAspectRatioPanelSampleProgram
{
    public static void Main() => new FixedAspectRatioPanelSample().Run();
}
//#EndSample

public class FixedAspectRatioPanelSampleRunner : IRecordableSample
{
    public string OutputPath => @"Containers\FixedAspectRatioPanelSample.gif";
    public int Width => 100;
    public int Height => 25;
    public ConsoleApp Define() => new FixedAspectRatioPanelSample();

}
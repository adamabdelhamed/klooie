//#Sample -Id ConsolePanelSample
using PowerArgs;
using klooie;
namespace klooie.Samples;

// Define your application
public class ConsolePanelSample : ConsoleApp
{
    protected override async Task Startup()
    {
        // every app comes with a root ConsolePanel called LayoutRoot
        ConsolePanel consolePanel = LayoutRoot;

        // by default the label is positioned at the top right (X == 0, Y == 0)
        var label = consolePanel.Add(new Label("Label that's manually positioned".ToGreen()));
        
        // we'll wait a second and then move the label to the right by 5 units
        await Task.Delay(2000);
        label.MoveTo(5, 0);

        // wait another second and then use layout helpers to position the label to the
        // right of the app with some padding while centering vertically
        await Task.Delay(2000);
        label.DockToRight(padding: 5).CenterVertically();

        await Task.Delay(2000);
        Stop();
    }
}

// Entry point for your application
public static class ConsolePanelSampleProgram
{
    public static void Main() => new ConsolePanelSample().Run();
}
//#EndSample


public class ConsolePanelSampleRunner : IRecordableSample
{
    public string OutputPath => @"ConsolePanel\ConsolePanelSample.gif";
    public int Width => 50;
    public int Height => 5;
    public ConsoleApp Define() => new ConsolePanelSample();
}
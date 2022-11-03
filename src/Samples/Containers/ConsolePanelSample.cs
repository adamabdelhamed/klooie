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

        consolePanel.Add(new Label("Docked right, centered vertically".ToGreen()))
            .DockToRight()
            .CenterVertically();

        consolePanel.Add(new Label("Docked left, centered vertically".ToGreen()))
            .DockToLeft()
            .CenterVertically();

        consolePanel.Add(new Label("Docked top, centered horizontally".ToGreen()))
          .DockToTop()
          .CenterHorizontally();

        consolePanel.Add(new Label("Docked bottom, centered horizontally".ToGreen()))
           .DockToBottom()
           .CenterHorizontally();

        consolePanel.Add(new Label("center both".ToOrange()))
            .CenterBoth();

        consolePanel.Add(new Label("Docked bottom left with padding".ToMagenta()))
           .DockToBottom(padding:2)
           .DockToLeft(padding: 4);

        consolePanel.Add(new Label("Docked bottom right with padding".ToMagenta()))
         .DockToBottom(padding: 2)
         .DockToRight(padding: 4);

        consolePanel.Add(new Label("Docked top left with padding".ToMagenta()))
           .DockToTop(padding: 2)
           .DockToLeft(padding: 4);

        consolePanel.Add(new Label("Docked top right with padding".ToMagenta()))
         .DockToTop(padding: 2)
         .DockToRight(padding: 4);

        await LayoutRoot.FadeIn(1000);
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
    public string OutputPath => @"Containers\ConsolePanelSample.gif";
    public int Width => 120;
    public int Height => 50;
    public ConsoleApp Define()
    {
        var ret = new ConsolePanelSample();
        ret.Invoke(async () =>
        {
            await Task.Delay(1000);
            ret.Stop();
        });
        return ret;
    }
}
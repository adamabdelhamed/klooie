//#Sample -Id StackPanelSample
using PowerArgs;
namespace klooie.Samples;

public class StackPanelSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var vStack = LayoutRoot.Add(new StackPanel() { Margin = 1, Background = RGB.Black, Orientation = Orientation.Vertical, Width = LayoutRoot.Width/2}).DockToLeft().FillVertically();
        var hStack = LayoutRoot.Add(new StackPanel() { Margin = 2, Background = RGB.White, Orientation = Orientation.Horizontal, Width = LayoutRoot.Width / 2 }).DockToRight().FillVertically();

        for(var i = 1; i <=vStack.Height/2; i++)
        {
            await vStack.Add(new Label($"Vertical label {i}")).FadeIn();
        }

        var random = new Random();
        var standardColors = RGB.ColorsToNames.Keys.ToArray();
        for (var i = 1; i <= vStack.Width/3; i++)
        {
            await hStack.Add(new Label($"H".ToConsoleString(standardColors[random.Next(0, standardColors.Length)])) { CompositionMode = CompositionMode.BlendBackground }).FadeIn();
        }

        await Task.Delay(1000);
        Stop();
    }
}

// Entry point for your application
public static class StackPanelSampleProgram
{
    public static void Main() => new StackPanelSample().Run();
}
//#EndSample

public class StackPanelSampleRunner : IRecordableSample
{
    public string OutputPath => @"Containers\StackPanelSample.gif";
    public int Width => 60;
    public int Height => 20;
    public ConsoleApp Define() => new StackPanelSample();

}
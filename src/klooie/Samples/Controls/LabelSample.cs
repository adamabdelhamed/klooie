//#Sample -Id LabelSample
using PowerArgs;
namespace klooie.Samples;

public class LabelSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var stack = LayoutRoot.Add(new StackPanel() { Orientation = Orientation.Vertical }).Fill();
        stack.Background = new RGB(50, 50, 50);
        stack.Add(new Label("Unstyled Text that uses control FG and BG") { Foreground = RGB.White, Background = RGB.DarkYellow });
        stack.Add(new Label("Red Text that uses the default background".ToRed()));
        stack.Add(new Label("Magenta Text that blends over the parent's background".ToMagenta()) { CompositionMode = CompositionMode.BlendBackground });
        stack.Add(new Label(ConsoleString.Parse("[Red]Multi [Green]Colored [Orange]Text [B=Cyan][Black] with custom [D] and blended BG")) { CompositionMode = CompositionMode.BlendBackground });
        await Task.Delay(100);
        Stop();
    }
}

// Entry point for your application
public static class LabelSampleProgram
{
    public static void Main() => new LabelSample().Run();
}
//#EndSample

public class LabelSampleRunner : IRecordableSample
{
    public string OutputPath => @"Controls\LabelSample.gif";
    public int Width => 60;
    public int Height => 4;
    public ConsoleApp Define() => new LabelSample();
 
}
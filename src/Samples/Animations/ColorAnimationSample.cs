//#Sample -Id ColorAnimationSample
using PowerArgs;
using klooie;
namespace klooie.Samples;

// Define your application
public class ColorAnimationSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var controlToAnimate = LayoutRoot.Add(new ConsoleControl() { Width = 10, Height = 5, Background = RGB.Orange }).CenterBoth();
        await Animator.AnimateAsync(controlToAnimate.Background, RGB.Green, 500, (RGB newColor) => controlToAnimate.Background = newColor, autoReverse: true);
        Stop();
    }
}

// Entry point for your application
public static class ColorAnimationSampleProgram
{
    public static void Main() => new ColorAnimationSample().Run();
}
//#EndSample


public class ColorAnimationSampleRunner : IRecordableSample
{
    public string OutputPath => @"Animations\ColorAnimationSample.gif";

    public int Width => 20;

    public int Height => 9;

    public ConsoleApp Define()
    {
        var ret = new ColorAnimationSample();
        return ret;
    }
}
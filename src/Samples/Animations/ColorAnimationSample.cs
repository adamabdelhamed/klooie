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

        await Animator.AnimateAsync(Animator.RGBAnimationState.Create(new List<KeyValuePair<RGB, RGB>>()
            {
                new KeyValuePair<RGB, RGB>(controlToAnimate.Background, RGB.Green),
            }, (RGB[] newColors) =>
            {
                // we're just animating one color so we access index 0
                controlToAnimate.Background = newColors[0];
            },
            autoReverse: true
        ));
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
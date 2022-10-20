//#Sample -Id AnimationSample
using PowerArgs;
using klooie;
namespace klooie.Samples;

// Define your application
public class AnimationSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var controlToAnimate = LayoutRoot.Add(new Label("Animated label".ToBlack(RGB.Orange))).CenterHorizontally();
        var destinationY = LayoutRoot.Bottom() - controlToAnimate.Height;
        var destination = new RectF(controlToAnimate.Left, destinationY, controlToAnimate.Width, controlToAnimate.Height);

        await controlToAnimate.AnimateAsync(new ConsoleControlAnimationOptions()
        {
            Destination = ()=> destination,
            Duration = 1000,
            AutoReverse = true,
            AutoReverseDelay = 500,
            EasingFunction = EasingFunctions.EaseInOut,
        });
        Stop();
    }
}

// Entry point for your application
public static class AnimationSampleProgram
{
    public static void Main() => new AnimationSample().Run();
}
//#EndSample


public class AnimationSampleRunner : IRecordableSample
{
    public string OutputPath => @"Animations\AnimationSample.gif";

    public int Width => 60;

    public int Height => 25;

    public ConsoleApp Define()
    {
        var ret = new AnimationSample();
        return ret;
    }
}
//#Sample -Id AnimationSample
using PowerArgs;
using klooie;
namespace klooie.Samples;

// Define your application
public class AnimationSample : ConsoleApp
{
    protected override async Task Startup()
    {
        await Animate(LayoutRoot.Add(new Label("Linear ease") { Y = 2 }), EasingFunctions.Linear);
        await Animate(LayoutRoot.Add(new Label("Ease in".ToOrange()) { Y = 4 }), EasingFunctions.EaseIn);
        await Animate(LayoutRoot.Add(new Label("Ease out".ToRed()) { Y = 6 }), EasingFunctions.EaseOut);
        await Animate(LayoutRoot.Add(new Label("Ease in & out".ToRed()) { Y = 8 }), EasingFunctions.EaseInOut);
        await Animate(LayoutRoot.Add(new Label("Ease overshoot".ToRed()) { Y = 10 }), EasingFunctions.EaseOverShootAndBounceBack);
        Stop();
    }

    private async Task Animate(Label l, EasingFunction ease)
    {
        var destination = new RectF(LayoutRoot.Right() - l.Width,l.Top , l.Width, l.Height);
        await l.FadeIn(1500);
        await l.AnimateAsync(new ConsoleControlAnimationOptions()
        {
            Destination = () => destination,
            Duration = 1500,
            AutoReverse = true,
            EasingFunction = ease,
        });
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
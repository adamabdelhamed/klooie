//#Sample -Id NumberAnimationSample
using PowerArgs;
using klooie;
namespace klooie.Samples;

// Define your application
public class NumberAnimationSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var start = 0f;
        var end = 100f;
        var number = start;
        var textFactory = () => ConsoleString.Parse($"[White]The number is [Black][B=Orange] {number} [D][White] and the animation will slow towards the end");
        var label = LayoutRoot.Add(new Label(textFactory())).CenterBoth();

        await Task.Delay(1000);
        await Animator.AnimateAsync(new FloatAnimationOptions()
        {
            // EasingFunctions.EaseOut makes the animation slow down at the end
            EasingFunction = EasingFunctions.EaseOut, 
            Duration = 4000,
            From = start,
            To = end,
            Setter = (currentAnimatedValue) =>
            {
                number = (float)Math.Round(currentAnimatedValue);
                label.Text = textFactory();
            }
        });
        Stop();
    }
}

// Entry point for your application
public static class NumberAnimationSampleProgram
{
    public static void Main() => new NumberAnimationSample().Run();
}
//#EndSample


public class NumberAnimationSampleRunner : IRecordableSample
{
    public string OutputPath => @"Animations\NumberAnimationSample.gif";

    public int Width => 100;

    public int Height => 5;

    public ConsoleApp Define()
    {
        var ret = new NumberAnimationSample();
        return ret;
    }
}
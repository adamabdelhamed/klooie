The types in this folder provide animation functionality for klooie applications.

**ConsoleControlAnimationOptions** let you animate the size and location of controls.

```cs
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

```
The sample above creates an application that looks like this.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Animations/AnimationSample.gif?raw=true)

**FloatAnimationOptions** let you animate a number.

```cs
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

```
The sample above creates an application that looks like this.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Animations/NumberAnimationSample.gif?raw=true)

**RGBAnimationOptions** let you animate colors.

```cs
using PowerArgs;
using klooie;
namespace klooie.Samples;

// Define your application
public class ColorAnimationSample : ConsoleApp
{
    protected override async Task Startup()
    {
        var controlToAnimate = LayoutRoot.Add(new ConsoleControl() { Width = 10, Height = 5, Background = RGB.Orange }).CenterBoth();

        await Animator.AnimateAsync(new RGBAnimationOptions()
        {
            // this is a list in case you want to synchronize multiple color animations
            Transitions = new List<KeyValuePair<RGB, RGB>>()
            {
                new KeyValuePair<RGB, RGB>(controlToAnimate.Background, RGB.Green),
            },
            OnColorsChanged = (RGB[] newColors) =>
            {
                // we're just animating one color so we access index 0
                controlToAnimate.Background = newColors[0];
            },
            Duration = 4000,
        });
        Stop();
    }
}

// Entry point for your application
public static class ColorAnimationSampleProgram
{
    public static void Main() => new ColorAnimationSample().Run();
}

```
The sample above creates an application that looks like this.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/klooie/Samples/Animations/ColorAnimationSample.gif?raw=true)

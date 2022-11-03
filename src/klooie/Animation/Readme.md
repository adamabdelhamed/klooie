The types in this folder provide animation functionality for klooie applications.

**ConsoleControlAnimationOptions** let you animate the size and location of controls.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/Animations/AnimationSample.gif?raw=true)
```cs
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

```

**FloatAnimationOptions** let you animate a number.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/Animations/NumberAnimationSample.gif?raw=true)
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

**RGBAnimationOptions** let you animate colors.

The code for this sample is shown below.

![sample image](https://github.com/adamabdelhamed/klooie/blob/main/src/Samples/Animations/ColorAnimationSample.gif?raw=true)
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
            Duration = 500,
            AutoReverse = true,
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

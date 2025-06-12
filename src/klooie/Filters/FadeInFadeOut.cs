namespace klooie;

public static class FadeEx
{
    public static async Task<IConsoleControlFilter> FadeIn(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, IDelayProvider delayProvider = null, RGB? bg = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = new FadeInFilter();
        if (bg.HasValue)
        {
            filter.BackgroundColor = bg.Value;
        }
        c.Filters.Add(filter);
        await Animator.AnimateAsync(new FloatAnimationOptions()
        {
            From = 0,
            To = percentage,
            Duration = duration,
            DelayProvider = delayProvider,
            EasingFunction = (p) => easingFunction(p),
            Setter = p =>
            {
                filter.Percentage = p;
            }
        });
        return filter;
    }
    public static IConsoleControlFilter FadeInSync(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, IDelayProvider delayProvider = null, RGB? bg = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = new FadeInFilter();
        if (bg.HasValue)
        {
            filter.BackgroundColor = bg.Value;
        }
        c.Filters.Add(filter);
        Animator.AnimateSync(new FloatAnimationOptions()
        {
            From = 0,
            To = percentage,
            Duration = duration,
            DelayProvider = delayProvider,
            EasingFunction = (p) => easingFunction(p),
            Setter = p =>
            {
                filter.Percentage = p;
            }
        });
        return filter;
    }

    public static async Task<IConsoleControlFilter> FadeOut(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, IDelayProvider delayProvider = null, RGB? bg = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = new FadeOutFilter();
        if (bg.HasValue)
        {
            filter.BackgroundColor = bg.Value;
        }
        c.Filters.Add(filter);
        await Animator.AnimateAsync(new FloatAnimationOptions()
        {
            From = 0,
            To = percentage,
            Duration = duration,
            DelayProvider = delayProvider,
            EasingFunction = (p) => easingFunction(p),
            Setter = p =>
            {
                filter.Percentage = p;
            }
        });
        return filter;
    }
    public static IConsoleControlFilter FadeOutSync(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, IDelayProvider delayProvider = null, RGB? bg = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = new FadeOutFilter();
        if (bg.HasValue)
        {
            filter.BackgroundColor = bg.Value;
        }
        c.Filters.Add(filter);
        Animator.AnimateSync(new FloatAnimationOptions()
        {
            From = 0,
            To = percentage,
            Duration = duration,
            DelayProvider = delayProvider,
            EasingFunction = (p) => easingFunction(p),
            Setter = p =>
            {
                filter.Percentage = p;
            }
        });
        return filter;
    }
}

internal sealed class FadeOutFilter : IConsoleControlFilter
{
    public float Percentage { get; set; }

    public RGB BackgroundColor { get; set; } = RGB.Black;

    /// <summary>
    /// The control to filter
    /// </summary>
    public ConsoleControl Control { get; set; }

    public void Filter(ConsoleBitmap bitmap)
    {
        bool blendBackgroundFade = Control.Background == ConsoleString.DefaultBackgroundColor && Control.CompositionMode == CompositionMode.BlendBackground;
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                var pixel = bitmap.GetPixel(x, y);

                bitmap.SetPixel(x, y, new ConsoleCharacter(pixel.Value, pixel.ForegroundColor.ToOther(BackgroundColor, Percentage),
                    (blendBackgroundFade && pixel.BackgroundColor == ConsoleString.DefaultBackgroundColor) ? ConsoleString.DefaultBackgroundColor : pixel.BackgroundColor.ToOther(BackgroundColor, Percentage)));

            }
        }
    }
}

internal sealed class FadeInFilter : IConsoleControlFilter
{
    public RGB BackgroundColor { get; set; } = RGB.Black;
    public float Percentage { get; set; }

    /// <summary>
    /// The control to filter
    /// </summary>
    public ConsoleControl Control { get; set; }

    public void Filter(ConsoleBitmap bitmap)
    {
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                var pixel = bitmap.GetPixel(x, y);
                bitmap.SetPixel(x, y, new ConsoleCharacter(pixel.Value, BackgroundColor.ToOther(pixel.ForegroundColor, Percentage),
                    BackgroundColor.ToOther(pixel.BackgroundColor, Percentage)));
            }
        }
    }
}
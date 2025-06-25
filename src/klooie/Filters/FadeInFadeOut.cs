namespace klooie;

public static class FadeEx
{
    public static async Task FadeIn(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, IDelayProvider delayProvider = null, RGB? bg = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = FadeInFilter.Create(c, bg);

        c.Filters.Add(filter);
        await Animator.AnimateAsync(0,percentage, duration, filter, static (state, percentage) =>  state.Percentage = percentage, easingFunction, delayProvider);
        filter.Dispose();
    }
    public static void FadeInSync(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, IDelayProvider delayProvider = null, RGB? bg = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = FadeInFilter.Create(c, bg);
        c.Filters.Add(filter);
        Animator.Animate(0, percentage, duration, filter, static (state, percentage) => state.Percentage = percentage, easingFunction, delayProvider);
        ConsoleApp.Current.Scheduler.DelayThenDisposeAllDependencies(duration, DelayState.Create(filter));
    }

    public static async Task FadeOut(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, IDelayProvider delayProvider = null, RGB? bg = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = FadeOutFilter.Create(c, bg);
        c.Filters.Add(filter);
        await Animator.AnimateAsync(0, percentage, duration, filter, static (state, percentage) => state.Percentage = percentage, easingFunction, delayProvider);
        filter.Dispose();
    }
    public static IConsoleControlFilter FadeOutSync(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, IDelayProvider delayProvider = null, RGB? bg = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = FadeOutFilter.Create(c, bg);
        c.Filters.Add(filter);
        Animator.Animate(0, percentage, duration, filter, static (state, percentage) => state.Percentage = percentage, easingFunction, delayProvider);
        ConsoleApp.Current.Scheduler.DelayThenDisposeAllDependencies(duration, DelayState.Create(filter));
        return filter;
    }
}

internal sealed class FadeOutFilter : Recyclable, IConsoleControlFilter
{
    public float Percentage { get; set; }

    public RGB BackgroundColor { get; set; } = RGB.Black;

    /// <summary>
    /// The control to filter
    /// </summary>
    public ConsoleControl Control { get; set; }

    private int controlLease;

    private static LazyPool<FadeOutFilter> pool = new LazyPool<FadeOutFilter>(() => new FadeOutFilter());

    private FadeOutFilter() { }

    private void Construct(ConsoleControl control, RGB? backgroundColor = null)
    {
        Control = control ?? throw new ArgumentNullException(nameof(control));
        controlLease = Control.Lease;
        if (backgroundColor.HasValue)
        {
            BackgroundColor = backgroundColor.Value;
        }
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        if (Control.IsStillValid(controlLease))
        {
            Control.IsVisible = false;
            Control.Filters.Remove(this);
        }
        Control = null;
        controlLease = 0;
        BackgroundColor = RGB.Black;
    }

    public static FadeOutFilter Create(ConsoleControl control, RGB? backgroundColor = null)
    {
        var ret = pool.Value.Rent();
        ret.Construct(control, backgroundColor);
        return ret;
    }

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

internal sealed class FadeInFilter : Recyclable, IConsoleControlFilter
{
    public RGB BackgroundColor { get; set; } = RGB.Black;
    public float Percentage { get; set; }

    private static LazyPool<FadeInFilter> pool = new LazyPool<FadeInFilter>(() => new FadeInFilter());

    private int controlLease;
    private FadeInFilter() { }

    private void Construct(ConsoleControl control, RGB? backgroundColor = null)
    {
        Control = control ?? throw new ArgumentNullException(nameof(control));
        controlLease = Control.Lease;
        if (backgroundColor.HasValue)
        {
            BackgroundColor = backgroundColor.Value;
        }
    }

    public static FadeInFilter Create(ConsoleControl control, RGB? backgroundColor = null)
    {
        var ret = pool.Value.Rent();
        ret.Construct(control, backgroundColor);
        return ret;
    }

    protected override void OnReturn()
    {
        base.OnReturn();
        if (Control.IsStillValid(controlLease))
        {
            Control.Filters.Remove(this);
        }
        Control = null;

        BackgroundColor = RGB.Black;
    }

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
                bitmap.SetPixel(x, y, new ConsoleCharacter(pixel.Value, BackgroundColor.ToOther(pixel.ForegroundColor, Percentage),
                    (blendBackgroundFade && pixel.BackgroundColor == ConsoleString.DefaultBackgroundColor) ? ConsoleString.DefaultBackgroundColor : BackgroundColor.ToOther(pixel.BackgroundColor, Percentage)));
            }
        }
    }
}
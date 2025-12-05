using klooie.Gaming;

namespace klooie;

public static class FadeEx
{
    public static async Task<IConsoleControlFilter> FadeIn(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, PauseManager pauseManager = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = FadeInFilter.Create(c);

        c.Filters.Add(filter);
        await Animator.AnimateAsync(0,percentage, duration, filter, static (state, percentage) =>  state.Percentage = percentage, easingFunction, pauseManager);
        if (percentage == 1)
        {
            filter.Dispose();
        }
        return filter;
    }
    public static IConsoleControlFilter FadeInSync(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, PauseManager pauseManager = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = FadeInFilter.Create(c);
        c.Filters.Add(filter);
        c.IsVisible = true;
        Animator.Animate(0, percentage, duration, filter, static (state, percentage) => state.Percentage = percentage, easingFunction, pauseManager);
        if (percentage == 1)
        {
            (pauseManager?.Scheduler ?? ConsoleApp.Current.Scheduler).DelayThenDisposeAllDependencies(duration, DelayState.Create(filter));
        }
        return filter;
    }

    public static async Task<IConsoleControlFilter> FadeOut(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, PauseManager pauseManager = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = FadeOutFilter.Create(c);
        c.Filters.Add(filter);
        await Animator.AnimateAsync(0, percentage, duration, filter, static (state, percentage) => state.Percentage = percentage, easingFunction, pauseManager);
        if(percentage == 1)
        {
            filter.Dispose();
        }
        return filter;
    }
    public static IConsoleControlFilter FadeOutSync(this ConsoleControl c, float duration = 500, EasingFunction easingFunction = null, float percentage = 1, PauseManager pauseManager = null)
    {
        easingFunction = easingFunction ?? EasingFunctions.Linear;
        var filter = FadeOutFilter.Create(c);
        c.Filters.Add(filter);
        Animator.Animate(0, percentage, duration, filter, static (state, percentage) => state.Percentage = percentage, easingFunction, pauseManager);
        if (percentage == 1)
        {
            (pauseManager?.Scheduler ?? ConsoleApp.Current.Scheduler).DelayThenDisposeAllDependencies(duration, DelayState.Create(filter));
        }
        return filter;
    }
}

internal sealed class FadeOutFilter : Recyclable, IConsoleControlFilter
{
    public float Percentage { get; set; }
    public ConsoleBitmap ParentBitmap { get; set; }

    /// <summary>
    /// The control to filter
    /// </summary>
    public ConsoleControl Control { get; set; }

    private static LazyPool<FadeOutFilter> pool = new LazyPool<FadeOutFilter>(() => new FadeOutFilter());

    private FadeOutFilter() { }

    private void Construct(ConsoleControl control)
    {
        Control = control ?? throw new ArgumentNullException(nameof(control));
        this.OnDisposed(LeaseHelper.TrackOwnerRelationship(this, Control), static lease =>
        {
            if(lease.IsRecyclableValid)
            {
                lease.Recyclable.IsVisible = false;
                lease.Recyclable.Filters.Remove(lease.Owner);
            }
            lease.Dispose();
        });
    }

    public static FadeOutFilter Create(ConsoleControl control)
    {
        var ret = pool.Value.Rent();
        ret.Construct(control);
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
                RGB effectiveBg = pixel.BackgroundColor;

                if (ParentBitmap != null)
                {
                    var loc = Control.Parent.Transform(Control);
                    int px = loc.X;
                    int py = loc.Y;
                    effectiveBg = ParentBitmap.IsInBounds(px, py) ? ParentBitmap.GetPixel(px, py).BackgroundColor : effectiveBg;
                }
                bitmap.SetPixel(x, y, new ConsoleCharacter(pixel.Value, pixel.ForegroundColor.ToOther(effectiveBg, Percentage), (blendBackgroundFade && pixel.BackgroundColor == ConsoleString.DefaultBackgroundColor) ? ConsoleString.DefaultBackgroundColor : pixel.BackgroundColor.ToOther(effectiveBg, Percentage)));
            }
        }
    }
}

internal sealed class FadeInFilter : Recyclable, IConsoleControlFilter
{
    public float Percentage { get; set; }

    private static LazyPool<FadeInFilter> pool = new LazyPool<FadeInFilter>(() => new FadeInFilter());

    private int controlLease;
    private FadeInFilter() { }

    private void Construct(ConsoleControl control)
    {
        Control = control ?? throw new ArgumentNullException(nameof(control));
        controlLease = Control.Lease;
    }

    public static FadeInFilter Create(ConsoleControl control)
    {
        var ret = pool.Value.Rent();
        ret.Construct(control);
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
    }

    /// <summary>
    /// The control to filter
    /// </summary>
    public ConsoleControl Control { get; set; }
    public ConsoleBitmap ParentBitmap { get; set; }
    public void Filter(ConsoleBitmap bitmap)
    {
        bool blendBackgroundFade = Control.Background == ConsoleString.DefaultBackgroundColor && Control.CompositionMode == CompositionMode.BlendBackground;
        for (var x = 0; x < bitmap.Width; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                var pixel = bitmap.GetPixel(x, y);
                RGB effectiveBg = pixel.BackgroundColor;

                if (ParentBitmap != null)
                {
                    var loc = Control.Parent.Transform(Control);
                    int px = loc.X;
                    int py = loc.Y;
                    effectiveBg = ParentBitmap.IsInBounds(px, py) ? ParentBitmap.GetPixel(px, py).BackgroundColor : effectiveBg;
                }
                bitmap.SetPixel(x, y, new ConsoleCharacter(pixel.Value, effectiveBg.ToOther(pixel.ForegroundColor, Percentage),
                    (blendBackgroundFade && pixel.BackgroundColor == ConsoleString.DefaultBackgroundColor) ? ConsoleString.DefaultBackgroundColor : effectiveBg.ToOther(pixel.BackgroundColor, Percentage)));
            }
        }
    }
}
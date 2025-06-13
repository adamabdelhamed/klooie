namespace klooie;

public abstract class CommonAnimationOptions
{
    public int TargetFramesPerSecond { get; set; } = Animator.DeafultTargetFramesPerSecond;
    public EasingFunction EasingFunction { get; set; } = EasingFunctions.Linear;

    /// <summary>
    /// The duration of the animation in milliseconds
    /// </summary>
    public double Duration { get; set; } = 500;

    /// <summary>
    /// The provider to use for delaying between animation frames
    /// </summary>
    public IDelayProvider DelayProvider { get; set; }

    /// <summary>
    /// If true then the animation will automatically reverse itself when done
    /// </summary>
    public bool AutoReverse { get; set; }

    /// <summary>
    /// When specified, the animation will loop until this lifetime completes
    /// </summary>
    public ILifetime Loop { get; set; }

    /// <summary>
    /// If auto reverse is enabled, this is the pause, in milliseconds, after the forward animation
    /// finishes, to wait before reversing
    /// </summary>
    public float AutoReverseDelay { get; set; } = 0;

    /// <summary>
    /// A callback that indicates that we should end the animation early
    /// </summary>
    public Func<bool> IsCancelled { get; set; }

    /// <summary>
    /// A callback that indicates that the animation should pause
    /// </summary>
    public Func<bool> IsPaused { get; set; }

    private async Task<bool> HandlePause()
    {
        var ret = false;
        while (IsPaused != null && IsPaused.Invoke())
        {
            ret = true;
            await Task.Yield();
        }
        return ret;
    }

    internal async Task YieldAsync()
    {
        if (await HandlePause() == false)
        {
            await Task.Yield();
        }
    }

    public async Task DelayAsync(TimeSpan ts)
    {
        if (await HandlePause() == false)
        {
            await DelayProvider.Delay(ts);
        }
    }
}

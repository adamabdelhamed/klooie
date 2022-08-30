namespace klooie;

/// <summary>
/// An abstraction for time delay so that we can have a consistent delay API across wall clock time and Time simulation time
/// </summary>
public interface IDelayProvider
{
    /// <summary>
    /// Delays for the given time
    /// </summary>
    /// <param name="ms">milliseconds</param>
    /// <returns>an async task</returns>
    Task Delay(double ms);

    /// <summary>
    /// Delays for the given time
    /// </summary>
    /// <param name="timeout">the delay time</param>
    /// <returns>an async task</returns>
    Task Delay(TimeSpan timeout);
}

/// <summary>
/// An implementation of IDelayProvider that is based on wall clock time
/// </summary>
public class WallClockDelayProvider : IDelayProvider
{
    /// <summary>
    /// Delays for the given time
    /// </summary>
    /// <param name="ms">milliseconds</param>
    /// <returns>an async task</returns>
    public Task Delay(double ms) => Task.Delay(TimeSpan.FromMilliseconds(ms));

    /// <summary>
    /// Delays for the given time
    /// </summary>
    /// <param name="timeout">the delay time</param>
    /// <returns>an async task</returns>
    public Task Delay(TimeSpan timeout) => Task.Delay(timeout);

    /// <summary>
    /// Yields immidiately
    /// </summary>
    /// <returns>an async task</returns>
    public async Task YieldAsync() => await Task.Yield();
}

public class NonDelayProvider : IDelayProvider
{
    public Task Delay(double ms) => Task.CompletedTask;
    public Task Delay(TimeSpan timeout) => Task.CompletedTask;
    public Task YieldAsync() => Task.CompletedTask;
}

public static class IDelayProviderEx
{
    private static Random r = new Random();
    public static Task DelayOrYield(this IDelayProvider provider, float ms) => DelayOrYield(provider, TimeSpan.FromMilliseconds(ms));
    public static async Task DelayOrYield(this IDelayProvider provider, TimeSpan delay)
    {
        if (delay == TimeSpan.Zero)
        {
            await Task.Yield();
        }
        else
        { 
            await provider.Delay(delay);
        }
    }

    public static async Task DelayFuzzy(this IDelayProvider provider, float ms, float maxPercentageDelta = .1f)
    {
        var effectiveDelay = r.Next(ConsoleMath.Round(ms * (1 - maxPercentageDelta)), ConsoleMath.Round(ms * (1 + maxPercentageDelta)));
        await provider.Delay(effectiveDelay);
    }

    public static async Task DelayFuzzy(this IDelayProvider provider, TimeSpan amount, float maxPercentageDelta = .1f)
    {
        var effectiveDelay = r.Next(ConsoleMath.Round(amount.TotalMilliseconds * (1 - maxPercentageDelta)), ConsoleMath.Round(amount.TotalMilliseconds * (1 + maxPercentageDelta)));
        await provider.Delay(effectiveDelay);
    }

    public static async Task ConditionalTask(this IDelayProvider delayProvider, Func<bool> condition, float evalFrequency)
    {
        while (condition() == false)
        {
            await delayProvider.Delay(evalFrequency);
        }
    }
}

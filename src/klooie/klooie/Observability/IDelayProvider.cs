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
public sealed class WallClockDelayProvider : IDelayProvider
{
    /// <summary>
    /// Delays for the given time
    /// </summary>
    /// <param name="ms">milliseconds</param>
    /// <returns>an async task</returns>
    public Task Delay(double ms)
    {
        if (double.IsNaN(ms)) throw new ArgumentException("Delay time is not a number");
        if (ms == 0) throw new ArgumentException("Delay time cannot be zero");
        return Task.Delay(TimeSpan.FromMilliseconds(ms));
    }

    /// <summary>
    /// Delays for the given time
    /// </summary>
    /// <param name="timeout">the delay time</param>
    /// <returns>an async task</returns>
    public Task Delay(TimeSpan timeout) => timeout > TimeSpan.Zero ? Task.Delay(timeout) : throw new ArgumentException("timeout must be > 0");

    /// <summary>
    /// Yields immidiately
    /// </summary>
    /// <returns>an async task</returns>
    public async Task YieldAsync() => await Task.Yield();
}

/// <summary>
/// Extension methods for IDelayProvider
/// </summary>
public static class IDelayProviderEx
{
    private static Random r = new Random();

    /// <summary>
    /// Delays if the amount is > 0 or yields
    /// </summary>
    /// <param name="provider">the delay provider</param>
    /// <param name="ms">the delay amount</param>
    /// <returns>a task</returns>
    public static Task DelayOrYield(this IDelayProvider provider, float ms) => DelayOrYield(provider, TimeSpan.FromMilliseconds(ms));

    /// <summary>
    /// Delays if the amount is > 0 or yields
    /// </summary>
    /// <param name="provider">the delay provider</param>
    /// <param name="delay">the delay amount</param>
    /// <returns>a task</returns>
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

    /// <summary>
    /// Delays for some time close to the amount given
    /// </summary>
    /// <param name="provider">the delay provider</param>
    /// <param name="ms">the delay amount</param>
    /// <param name="maxPercentageDelta">the amount to fuzz, between 0 and 1</param>
    /// <returns>a task</returns>
    public static async Task DelayFuzzy(this IDelayProvider provider, double ms, float maxPercentageDelta = .1f)
    {
        if (maxPercentageDelta < 0 || maxPercentageDelta > 1) throw new ArgumentException($"{nameof(maxPercentageDelta)} must be >= 0 and < 1");
        var effectiveDelay = r.Next(ConsoleMath.Round(ms * (1 - maxPercentageDelta)), ConsoleMath.Round(ms * (1 + maxPercentageDelta)));
        await provider.Delay(effectiveDelay);
    }

    /// <summary>
    /// Delays for some time close to the amount given
    /// </summary>
    /// <param name="provider">the delay provider</param>
    /// <param name="amount">the delay amount</param>
    /// <param name="maxPercentageDelta">the amount to fuzz, between 0 and 1</param>
    /// <returns>a task</returns>
    public static Task DelayFuzzy(this IDelayProvider provider, TimeSpan amount, float maxPercentageDelta = .1f)
        => DelayFuzzy(provider, amount.TotalSeconds, maxPercentageDelta);
}

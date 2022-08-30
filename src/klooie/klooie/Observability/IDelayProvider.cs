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
    public Task Delay(double ms) => ms > 0 ? Task.Delay(TimeSpan.FromMilliseconds(ms)) : throw new ArgumentException("ms must be > 0");

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

public class NonDelayProvider : IDelayProvider
{
    public Task Delay(double ms) => ms > 0 ? Task.CompletedTask : throw new ArgumentException("ms must be > 0");
    public Task Delay(TimeSpan timeout) => timeout > TimeSpan.Zero ? Task.CompletedTask : throw new ArgumentException("timeout must be > 0");
    public Task YieldAsync() => Task.CompletedTask;
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
    public static async Task DelayFuzzy(this IDelayProvider provider, float ms, float maxPercentageDelta = .1f)
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
    public static async Task DelayFuzzy(this IDelayProvider provider, TimeSpan amount, float maxPercentageDelta = .1f)
    {
        if (maxPercentageDelta < 0 || maxPercentageDelta > 1) throw new ArgumentException($"{nameof(maxPercentageDelta)} must be >= 0 and < 1");
        var effectiveDelay = r.Next(ConsoleMath.Round(amount.TotalMilliseconds * (1 - maxPercentageDelta)), ConsoleMath.Round(amount.TotalMilliseconds * (1 + maxPercentageDelta)));
        await provider.Delay(effectiveDelay);
    }

    /// <summary>
    /// Creates a task that completes once the given condition is true
    /// </summary>
    /// <param name="delayProvider">the delay provider</param>
    /// <param name="condition">the condition</param>
    /// <param name="evalFrequency">how frequently to evalaute</param>
    /// <returns></returns>
    public static async Task ConditionalTask(this IDelayProvider delayProvider, Func<bool> condition, float evalFrequency)
    {
        while (condition() == false)
        {
            await delayProvider.Delay(evalFrequency);
        }
    }
}

using PowerArgs;
namespace klooie.Gaming;
internal class PauseDelayProvider : IDelayProvider
{
    private PauseManager manager;
    public PauseDelayProvider(PauseManager manager) => this.manager = manager;

    public Task DelayAsync(Func<bool> condition, TimeSpan? timeout = null, TimeSpan? evalFrequency = null)
    {
        var conditionTask = ConsoleApp.Current.InvokeAsync(async () =>
        {
            while (!condition())
            {
                await (evalFrequency.HasValue ? Task.Delay(evalFrequency.Value) : Yield());
            }
        });

        return timeout.HasValue ? TaskEx.WhenAny(conditionTask, DelayAsync(timeout.Value)) : conditionTask;
    }

    public Task DelayFuzzyAsync(float ms, double maxDeltaPercentage = 0.1) => DelayAsync(ms);
    public Task DelayAsync(double ms) => manager.Delay((float)ms);
    public Task DelayAsync(TimeSpan timeout) => manager.Delay(timeout.Milliseconds);
    public Task DelayAsync(Event ev, TimeSpan? timeout = null, TimeSpan? evalFrequency = null) =>
        timeout.HasValue ? TaskEx.WhenAny(ev.CreateNextFireLifetime().AsTask(), DelayAsync(timeout.Value)) : ev.CreateNextFireLifetime().AsTask();

    public async Task<bool> TryDelayAsync(Func<bool> condition, TimeSpan? timeout = null, TimeSpan? evalFrequency = null)
    {
        await DelayAsync(condition, timeout, evalFrequency);
        return condition();
    }

    private async Task Yield() => await Task.Yield();
}

namespace klooie.Gaming;
internal class PauseDelayProvider : IDelayProvider
{
    private PauseManager manager;
    public PauseDelayProvider(PauseManager manager) => this.manager = manager;
    public Task DelayFuzzyAsync(float ms, double maxDeltaPercentage = 0.1) => Delay(ms);
    public Task Delay(double ms) => manager.Delay((float)ms);
    public Task Delay(TimeSpan timeout) => manager.Delay(timeout.TotalMilliseconds);
 

    private async Task Yield() => await Task.Yield();
}

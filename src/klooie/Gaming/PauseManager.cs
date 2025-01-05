using System.Diagnostics;
namespace klooie.Gaming;
public sealed class PauseManager : IDelayProvider
{
    private Lifetime? pauseLifetime;
    private bool isPaused;
    public Event<ILifetimeManager> OnPaused { get; private set; } = new Event<ILifetimeManager>();

    public bool IsPaused
    {
        get => isPaused;
        set
        {
            if (value == isPaused) return;
            isPaused = value;

            if(isPaused)
            {
                pauseLifetime = new Lifetime();
                OnPaused.Fire(pauseLifetime);
            }
            else
            {
                pauseLifetime?.Dispose();
                pauseLifetime = null;
            }
        }
    }

    public Task Delay(double ms) => Delay((float)ms);
    public Task Delay(TimeSpan span) => Delay(span.TotalMilliseconds);
    public async Task Delay(float ms)
    {
        if (float.IsNaN(ms))
            throw new ArgumentException("Delay time is not a number");
        if (ms <= 0)
            throw new ArgumentException("Delay time must be greater than zero");

        // Convert delay to ticks
        long delayTicks = (long)(ms * Stopwatch.Frequency / 1000); // Convert ms to ticks

        // Get the starting timestamp
        long startTicks = Stopwatch.GetTimestamp();

        // Calculate the target end time
        long endTicks = startTicks + delayTicks;

        while (Stopwatch.GetTimestamp() < endTicks)
        {
            while (isPaused)
            {
                // While paused, update the endTicks to account for the time spent paused
                await Task.Yield();
                startTicks = Stopwatch.GetTimestamp();
                endTicks = startTicks + delayTicks;
            }
            await Task.Yield();
        }
    }
}

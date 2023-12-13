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
        if (float.IsNaN(ms)) throw new ArgumentException("Delay time is not a number");
        if (ms == 0) throw new ArgumentException("Delay time cannot be zero");
        while (isPaused)
        {
            await Task.Yield();
        }
        var sw = Stopwatch.StartNew();
        while(sw.ElapsedMilliseconds < ms)
        {
            if(isPaused && sw.IsRunning)
            {
                sw.Stop();
            }
            else if(isPaused == false && sw.IsRunning == false)
            {
                sw.Start();
            }
            await Task.Yield();
        }
    }
}

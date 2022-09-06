using System.Diagnostics;
using PowerArgs;
namespace klooie.Gaming;
internal class PauseManager
{
    private Lifetime? pauseLifetime;
    private PauseState state;
    public IDelayProvider DelayProvider { get; init; } 
    public Event<ILifetimeManager> OnPaused { get; private set; } = new Event<ILifetimeManager>();

    public PauseState State
    {
        get => state;
        set
        {
            if (value == state) return;
            state = value;

            if(state == PauseState.Paused)
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

    public PauseManager()
    {
        State = PauseState.Running;
        DelayProvider = new PauseDelayProvider(this);
    }

    public Task Delay(double ms) => Delay((float)ms);
    public Task Delay(TimeSpan span) => Delay(span.TotalMilliseconds);
    public async Task Delay(float ms)
    {
        if (float.IsNaN(ms)) throw new ArgumentException("Delay time is not a number");
        if (ms == 0) throw new ArgumentException("Delay time cannot be zero");
        while (state == PauseState.Paused)
        {
            await Task.Yield();
        }
        var sw = Stopwatch.StartNew();
        while(sw.ElapsedMilliseconds < ms)
        {
            if(state == PauseState.Paused && sw.IsRunning)
            {
                sw.Stop();
            }
            else if(state == PauseState.Running && sw.IsRunning == false)
            {
                sw.Start();
            }
            await Task.Yield();
        }
    }

    public enum PauseState
    {
        Paused,
        Running
    }
}

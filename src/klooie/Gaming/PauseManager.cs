namespace klooie.Gaming;
public sealed class PauseManager : IDelayProvider
{
    private Lifetime? pauseLifetime;
    public Event<ILifetimeManager> OnPaused { get; private set; } = new Event<ILifetimeManager>();

    public bool IsPaused
    {
        get => Game.Current.InnerLoopAPIs.IsPaused;
        set
        {
            if (value == IsPaused) return;

            if (value)
            {
                Game.Current.InnerLoopAPIs.Pause();
                pauseLifetime = new Lifetime();
                OnPaused.Fire(pauseLifetime);
            }
            else
            {
                Game.Current.InnerLoopAPIs.Resume();
                pauseLifetime?.Dispose();
                pauseLifetime = null;
            }
        }
    }

    public Task Delay(double ms) => Delay((float)ms);
    public Task Delay(TimeSpan span) => Delay(span.TotalMilliseconds);
    public Task Delay(float ms)
    {
        if (float.IsNaN(ms)) throw new ArgumentException("Delay time is not a number");
        if (ms <= 0) throw new ArgumentException("Delay time must be greater than zero");
        var tcs = new TaskCompletionSource();
        ConsoleApp.Current.InnerLoopAPIs.Delay(ms, tcs, SetResult);
        return tcs.Task;
    }

    private static void SetResult(object obj) => (obj as TaskCompletionSource).SetResult();
}
